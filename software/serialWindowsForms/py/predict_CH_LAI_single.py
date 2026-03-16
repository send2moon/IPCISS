#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
单样本多模态预测：输入一个 PLY 点云路径和一张 RGB 图像路径，
同时提取点云结构特征 + RGB 植被指数统计特征，
再调用 CH.joblib 和 LAI.joblib 模型分别预测 CH 与 LAI。

示例：
python predict_CH_LAI_single.py \
  --ply_path "/data/sample001.ply" \
  --rgb_path "/data/sample001.jpg" \
  --ch_model "/data/models/CH.joblib" \
  --lai_model "/data/models/LAI.joblib" \
  --save_features "/data/sample001_features.xlsx"

依赖：
  pip install open3d opencv-python pandas numpy openpyxl joblib
"""

from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
from typing import Any

import cv2
import joblib
import numpy as np
import pandas as pd

try:
    import open3d as o3d
except Exception as e:
    raise SystemExit("Open3D is required. Install with: pip install open3d") from e


EPS = 1e-6


# =========================
# RGB 指数特征
# =========================
def safe_div(a: np.ndarray, b: np.ndarray, eps: float = EPS) -> np.ndarray:
    return a / (b + eps)


def compute_indices_rgb(R: np.ndarray, G: np.ndarray, B: np.ndarray) -> dict[str, np.ndarray]:
    ExG = 2.0 * G - R - B
    ExR = 1.4 * R - G
    ExGR = ExG - ExR

    NGRDI = safe_div(G - R, G + R)
    NGBDI = safe_div(G - B, G + B)
    GLI = safe_div(2.0 * G - R - B, 2.0 * G + R + B)
    VARI = safe_div(G - R, G + R - B)
    RGBVI = safe_div(G * G - R * B, G * G + R * B)
    MExG = 1.262 * G - 0.884 * R - 0.311 * B
    TGI = -0.5 * (190.0 * (R - G) - 120.0 * (R - B))
    RGRI = safe_div(R, G)
    GRR = safe_div(G, R)

    return {
        "ExG": ExG,
        "ExR": ExR,
        "ExGR": ExGR,
        "NGRDI": NGRDI,
        "NGBDI": NGBDI,
        "GLI": GLI,
        "VARI": VARI,
        "RGBVI": RGBVI,
        "MExG": MExG,
        "TGI": TGI,
        "RGRI": RGRI,
        "GRR": GRR,
    }


def vegetation_mask_exg_otsu(exg: np.ndarray) -> np.ndarray:
    exg_norm = exg - np.nanmin(exg)
    denom = np.nanmax(exg_norm) - np.nanmin(exg_norm)
    exg_u8 = (255.0 * exg_norm / (denom + EPS)).astype(np.uint8)
    _, thr = cv2.threshold(exg_u8, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    return thr.astype(bool)


def vegetation_mask_exg_fixed(exg: np.ndarray, thr: float = 0.0) -> np.ndarray:
    return exg > thr


def read_rgb_image(path: str | Path, resize_max: int = 0) -> tuple[np.ndarray, np.ndarray, np.ndarray, int, int]:
    img_bgr = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if img_bgr is None:
        raise ValueError(f"Cannot read image: {path}")

    h, w = img_bgr.shape[:2]
    if resize_max and max(h, w) > resize_max:
        scale = resize_max / max(h, w)
        new_w = int(round(w * scale))
        new_h = int(round(h * scale))
        img_bgr = cv2.resize(img_bgr, (new_w, new_h), interpolation=cv2.INTER_AREA)
        h, w = img_bgr.shape[:2]

    img_rgb = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2RGB).astype(np.float32) / 255.0
    R, G, B = img_rgb[..., 0], img_rgb[..., 1], img_rgb[..., 2]
    return R, G, B, h, w


def stats_on_mask(arr: np.ndarray, mask: np.ndarray | None) -> dict[str, float]:
    x = arr.reshape(-1) if mask is None else arr[mask].reshape(-1)
    x = x[np.isfinite(x)]
    if x.size == 0:
        return {"mean": np.nan, "std": np.nan, "p50": np.nan, "p90": np.nan}
    return {
        "mean": float(np.mean(x)),
        "std": float(np.std(x)),
        "p50": float(np.percentile(x, 50)),
        "p90": float(np.percentile(x, 90)),
    }


def extract_rgb_features(
    rgb_path: str | Path,
    mask_mode: str = "exg_otsu",
    exg_thr: float = 0.0,
    resize_max: int = 0,
) -> dict[str, float | str]:
    R, G, B, h, w = read_rgb_image(rgb_path, resize_max=resize_max)
    idx_maps = compute_indices_rgb(R, G, B)

    mask = None
    if mask_mode == "exg_otsu":
        mask = vegetation_mask_exg_otsu(idx_maps["ExG"])
    elif mask_mode == "exg_fixed":
        mask = vegetation_mask_exg_fixed(idx_maps["ExG"], thr=exg_thr)
    elif mask_mode == "none":
        mask = None
    else:
        raise ValueError(f"Unsupported mask_mode: {mask_mode}")

    feats: dict[str, float | str] = {
        "rgb_path": str(Path(rgb_path).resolve()),
        "rgb_file_name": Path(rgb_path).name,
        "rgb_file_stem": Path(rgb_path).stem,
        "height_px": int(h),
        "width_px": int(w),
        "mask_mode": mask_mode,
        "veg_fraction": float(np.mean(mask)) if mask is not None else np.nan,
        "R_mean": float(np.mean(R[mask]) if mask is not None else np.mean(R)),
        "G_mean": float(np.mean(G[mask]) if mask is not None else np.mean(G)),
        "B_mean": float(np.mean(B[mask]) if mask is not None else np.mean(B)),
    }

    for name, arr in idx_maps.items():
        st = stats_on_mask(arr, mask)
        feats[f"{name}_mean"] = st["mean"]
        feats[f"{name}_std"] = st["std"]
        feats[f"{name}_p50"] = st["p50"]
        feats[f"{name}_p90"] = st["p90"]

    return feats


# =========================
# 点云结构特征
# =========================
def robust_ground_z0(z: np.ndarray) -> float:
    if z.size == 0:
        return 0.0
    p1 = np.percentile(z, 1.0)
    p5 = np.percentile(z, 5.0)
    low = z[z <= p5]
    if low.size == 0:
        return float(p1)
    return float(np.mean(low[(low >= p1 - 1.0) & (low <= p5 + 1.0)]))


def basic_geometry(points: np.ndarray) -> dict[str, float | int]:
    n = len(points)
    centroid = points.mean(axis=0)
    minb = points.min(axis=0)
    maxb = points.max(axis=0)
    extent = maxb - minb
    vol = float(extent[0] * extent[1] * extent[2])
    density = float(n) / vol if vol > 0 else float("nan")
    z = points[:, 2]
    return {
        "num_points": int(n),
        "centroid_x": float(centroid[0]),
        "centroid_y": float(centroid[1]),
        "centroid_z": float(centroid[2]),
        "aabb_min_x": float(minb[0]),
        "aabb_min_y": float(minb[1]),
        "aabb_min_z": float(minb[2]),
        "aabb_dx": float(extent[0]),
        "aabb_dy": float(extent[1]),
        "aabb_dz": float(extent[2]),
        "aabb_volume": vol,
        "density_aabb": density,
        "z_raw_min": float(z.min()),
        "z_raw_max": float(z.max()),
        "z_raw_range": float(z.max() - z.min()),
        "z_raw_std": float(np.std(z)),
    }


def global_pca_features(points: np.ndarray, eps: float = 1e-12) -> dict[str, float]:
    mu = points.mean(axis=0)
    X = points - mu
    if len(points) < 3:
        l1 = l2 = l3 = 0.0
    else:
        cov = (X.T @ X) / max(len(points), 1)
        w, _ = np.linalg.eigh(cov)
        l1, l2, l3 = w[::-1]
    s = max(l1 + l2 + l3, eps)
    ln = np.array([l1, l2, l3]) / s
    return {
        "eig_l1": float(l1),
        "eig_l2": float(l2),
        "eig_l3": float(l3),
        "linearity": float((l1 - l2) / (l1 + eps)),
        "planarity": float((l2 - l3) / (l1 + eps)),
        "sphericity": float(l3 / (l1 + eps)),
        "anisotropy": float((l1 - l3) / (l1 + eps)),
        "omnivariance": float(max(l1 * l2 * l3, 0.0)) ** (1.0 / 3.0),
        "eigenentropy": float(-(ln * np.log(ln + eps)).sum()),
        "curvature_change": float(l3 / s),
    }


def height_metrics(zp: np.ndarray) -> dict[str, float]:
    if zp.size == 0:
        return {"H50": np.nan, "H90": np.nan, "H95": np.nan, "Hmax": np.nan, "sigma_H": np.nan, "HVR": np.nan}
    H50 = float(np.percentile(zp, 50))
    H90 = float(np.percentile(zp, 90))
    H95 = float(np.percentile(zp, 95))
    Hmax = float(np.max(zp))
    sigma = float(np.std(zp))
    hvr = float((H95 - H50) / (H95 + 1e-12)) if np.isfinite(H95) and np.isfinite(H50) else float("nan")
    return {"H50": H50, "H90": H90, "H95": H95, "Hmax": Hmax, "sigma_H": sigma, "HVR": hvr}


def stratification_metrics(zp: np.ndarray, dz: float) -> dict[str, float]:
    if zp.size == 0:
        return {
            "FHD": np.nan,
            "PAD_centroid_z": np.nan,
            "PAD_peak_z": np.nan,
            "PAD_width_std": np.nan,
            "frac_low": np.nan,
            "frac_mid": np.nan,
            "frac_top": np.nan,
        }
    Hmax = float(np.max(zp))
    if not np.isfinite(Hmax) or Hmax <= 0 or dz <= 0:
        return {
            "FHD": np.nan,
            "PAD_centroid_z": np.nan,
            "PAD_peak_z": np.nan,
            "PAD_width_std": np.nan,
            "frac_low": np.nan,
            "frac_mid": np.nan,
            "frac_top": np.nan,
        }
    nbins = max(int(math.ceil(Hmax / dz)), 1)
    edges = np.linspace(0.0, nbins * dz, nbins + 1)
    hist, _ = np.histogram(zp, bins=edges)
    total = hist.sum()
    if total == 0:
        return {
            "FHD": np.nan,
            "PAD_centroid_z": np.nan,
            "PAD_peak_z": np.nan,
            "PAD_width_std": np.nan,
            "frac_low": np.nan,
            "frac_mid": np.nan,
            "frac_top": np.nan,
        }
    p = hist / total
    centers = (edges[:-1] + edges[1:]) / 2.0
    FHD = float(-(p[p > 0] * np.log(p[p > 0])).sum())
    z_centroid = float((centers * p).sum())
    z_peak = float(centers[np.argmax(p)])
    width = float(np.std(zp))
    rel = zp / (Hmax + 1e-12)
    return {
        "FHD": FHD,
        "PAD_centroid_z": z_centroid,
        "PAD_peak_z": z_peak,
        "PAD_width_std": width,
        "frac_low": float(np.mean(rel < 0.33)),
        "frac_mid": float(np.mean((rel >= 0.33) & (rel < 0.66))),
        "frac_top": float(np.mean(rel >= 0.66)),
    }


def make_grid_indices(x: np.ndarray, y: np.ndarray, origin_xy: np.ndarray, g: float):
    ix = np.floor((x - origin_xy[0]) / g).astype(np.int64)
    iy = np.floor((y - origin_xy[1]) / g).astype(np.int64)
    return ix, iy


def canopy_cover(points_xy: np.ndarray, zp: np.ndarray, grid_size: float, hmin: float) -> dict[str, float | int]:
    x = points_xy[:, 0]
    y = points_xy[:, 1]
    minxy = np.array([x.min(), y.min()])
    maxxy = np.array([x.max(), y.max()])
    nx = int(math.ceil((maxxy[0] - minxy[0]) / grid_size))
    ny = int(math.ceil((maxxy[1] - minxy[1]) / grid_size))
    if nx <= 0 or ny <= 0:
        return {"CC_local": np.nan, "CC_aabb": np.nan}
    ix, iy = make_grid_indices(x, y, minxy, grid_size)
    key = ix.astype(np.int64) * (10**9) + iy.astype(np.int64)
    chm = pd.DataFrame({"key": key, "zp": zp}).groupby("key")["zp"].max()
    obs_total = chm.shape[0]
    cc_local = float((chm > hmin).mean()) if obs_total > 0 else float("nan")
    cc_aabb = float((chm > hmin).sum() / (nx * ny))
    return {"CC_local": cc_local, "CC_aabb": cc_aabb, "nx": nx, "ny": ny}


def porosity_voxel(points_xy: np.ndarray, zp: np.ndarray, voxel: float, hmin: float) -> dict[str, float | int]:
    if voxel <= 0 or points_xy.shape[0] == 0:
        return {
            "porosity_mean": np.nan,
            "porosity_median": np.nan,
            "porosity_q25": np.nan,
            "porosity_q75": np.nan,
            "occupancy_mean": np.nan,
            "columns": 0,
        }
    x = points_xy[:, 0]
    y = points_xy[:, 1]
    x0, y0 = float(x.min()), float(y.min())
    ix = np.floor((x - x0) / voxel).astype(np.int64)
    iy = np.floor((y - y0) / voxel).astype(np.int64)
    key_xy = ix * (10**9) + iy
    chm = pd.DataFrame({"key": key_xy, "zp": zp}).groupby("key")["zp"].max()
    S = chm[chm > hmin]
    if S.empty:
        return {
            "porosity_mean": np.nan,
            "porosity_median": np.nan,
            "porosity_q25": np.nan,
            "porosity_q75": np.nan,
            "occupancy_mean": np.nan,
            "columns": 0,
        }
    mask_veg = zp > hmin
    if not np.any(mask_veg):
        return {
            "porosity_mean": np.nan,
            "porosity_median": np.nan,
            "porosity_q25": np.nan,
            "porosity_q75": np.nan,
            "occupancy_mean": np.nan,
            "columns": 0,
        }
    iz = np.floor((zp[mask_veg] - hmin) / voxel).astype(np.int64)
    key_xy_veg = ix[mask_veg] * (10**9) + iy[mask_veg]
    nunique_iz = pd.DataFrame({"key": key_xy_veg, "iz": iz}).groupby("key")["iz"].nunique()
    data = pd.DataFrame({"CHM": S})
    data["N_occ"] = nunique_iz.reindex(data.index, fill_value=0)
    data["N_tot"] = np.maximum(1, np.floor((data["CHM"] - hmin) / voxel).astype(np.int64) + 1)
    occ_ratio = (data["N_occ"] / data["N_tot"]).clip(0.0, 1.0).to_numpy()
    porosity = 1.0 - occ_ratio
    return {
        "porosity_mean": float(np.mean(porosity)),
        "porosity_median": float(np.median(porosity)),
        "porosity_q25": float(np.quantile(porosity, 0.25)),
        "porosity_q75": float(np.quantile(porosity, 0.75)),
        "occupancy_mean": float(np.mean(occ_ratio)),
        "columns": int(len(porosity)),
    }


def surface_normal_metrics(
    pcd: o3d.geometry.PointCloud,
    knn: int,
    ds_voxel: float,
    tilt_bins=(30.0, 60.0),
) -> dict[str, float | int]:
    q = pcd
    if ds_voxel and ds_voxel > 0:
        q = pcd.voxel_down_sample(ds_voxel)
    if len(q.points) < max(3, knn):
        return {
            "normal_tilt_mean_deg": np.nan,
            "normal_tilt_std_deg": np.nan,
            "normal_frac_vert": np.nan,
            "normal_frac_oblique": np.nan,
            "normal_frac_horiz": np.nan,
            "normal_n_points": int(len(q.points)),
        }
    q.estimate_normals(search_param=o3d.geometry.KDTreeSearchParamKNN(knn=knn))
    n = np.asarray(q.normals)
    nz_abs = np.clip(np.abs(n[:, 2]), 0.0, 1.0)
    tilt = np.degrees(np.arccos(nz_abs))
    b1, b2 = tilt_bins
    return {
        "normal_tilt_mean_deg": float(np.mean(tilt)),
        "normal_tilt_std_deg": float(np.std(tilt)),
        "normal_frac_vert": float(np.mean(tilt < b1)),
        "normal_frac_oblique": float(np.mean((tilt >= b1) & (tilt < b2))),
        "normal_frac_horiz": float(np.mean(tilt >= b2)),
        "normal_n_points": int(len(q.points)),
    }


def read_ply(path: str | Path) -> o3d.geometry.PointCloud:
    pcd = o3d.io.read_point_cloud(str(path))
    if len(pcd.points) == 0:
        raise ValueError(f"Empty point cloud: {path}")
    pts = np.asarray(pcd.points)
    mask = np.isfinite(pts).all(axis=1)
    if not mask.all():
        pts = pts[mask]
        pcd = o3d.geometry.PointCloud(o3d.utility.Vector3dVector(pts))
    return pcd


def extract_ply_features(
    ply_path: str | Path,
    grid_size: float = 0.03,
    voxel_porosity: float = 0.03,
    height_bin: float = 0.05,
    hmin: float = 0.03,
    knn_normals: int = 30,
    normal_ds_voxel: float = 0.03,
) -> dict[str, float | int | str]:
    pcd = read_ply(ply_path)
    pts = np.asarray(pcd.points)

    feats: dict[str, float | int | str] = {}
    feats.update(basic_geometry(pts))
    feats.update(global_pca_features(pts))

    z0 = robust_ground_z0(pts[:, 2])
    zp = pts[:, 2] - z0
    feats.update(height_metrics(zp))
    feats.update(stratification_metrics(zp, dz=height_bin))

    cc = canopy_cover(pts[:, :2], zp, grid_size=grid_size, hmin=hmin)
    feats.update({"CC_local": cc["CC_local"], "CC_aabb": cc["CC_aabb"]})

    poro = porosity_voxel(pts[:, :2], zp, voxel=voxel_porosity, hmin=hmin)
    feats.update(
        {
            "porosity_mean": poro["porosity_mean"],
            "porosity_median": poro["porosity_median"],
            "porosity_q25": poro["porosity_q25"],
            "porosity_q75": poro["porosity_q75"],
            "occupancy_mean": poro["occupancy_mean"],
            "porosity_columns": poro["columns"],
        }
    )

    feats.update(surface_normal_metrics(pcd, knn=knn_normals, ds_voxel=normal_ds_voxel))

    feats["ply_path"] = str(Path(ply_path).resolve())
    feats["ply_file_name"] = Path(ply_path).name
    feats["ply_file_stem"] = Path(ply_path).stem
    feats["z0_ground_ref"] = float(z0)
    return feats


# =========================
# 模型特征对齐与预测
# =========================
def try_get_feature_names(model_obj: Any) -> list[str] | None:
    candidates = []

    if hasattr(model_obj, "feature_names_in_"):
        names = getattr(model_obj, "feature_names_in_")
        try:
            return [str(x) for x in names]
        except Exception:
            pass

    for attr in [
        "feature_names",
        "feature_names_",
        "selected_features",
        "selected_features_",
        "columns",
        "column_names",
        "input_features",
        "predictor_columns",
        "x_columns",
        "X_columns",
    ]:
        if hasattr(model_obj, attr):
            candidates.append(getattr(model_obj, attr))
        if isinstance(model_obj, dict) and attr in model_obj:
            candidates.append(model_obj[attr])

    for item in candidates:
        if item is None:
            continue
        if isinstance(item, (list, tuple, np.ndarray, pd.Index)):
            vals = [str(x) for x in list(item)]
            if vals:
                return vals

    if hasattr(model_obj, "named_steps"):
        for _, step in model_obj.named_steps.items():
            names = try_get_feature_names(step)
            if names:
                return names

    if hasattr(model_obj, "steps"):
        for _, step in model_obj.steps:
            names = try_get_feature_names(step)
            if names:
                return names

    return None


def unwrap_model(model_obj: Any) -> Any:
    if isinstance(model_obj, dict):
        for key in ["model", "estimator", "best_model", "pipeline", "clf", "regressor"]:
            if key in model_obj:
                return model_obj[key]
    return model_obj


def load_model_and_feature_names(model_path: str | Path) -> tuple[Any, list[str] | None, Any]:
    raw_obj = joblib.load(model_path)
    model = unwrap_model(raw_obj)
    feature_names = try_get_feature_names(raw_obj)
    if feature_names is None:
        feature_names = try_get_feature_names(model)
    return model, feature_names, raw_obj


def coerce_numeric_feature_df(feature_dict: dict[str, Any]) -> pd.DataFrame:
    row = {}
    for k, v in feature_dict.items():
        if isinstance(v, (int, float, np.integer, np.floating)):
            row[k] = float(v)
    return pd.DataFrame([row])


def align_features_for_model(
    feature_df: pd.DataFrame,
    required_features: list[str] | None,
    fill_value: float = 0.0,
    on_missing: str = "zero",
) -> tuple[pd.DataFrame, list[str], list[str]]:
    available = list(feature_df.columns)

    if not required_features:
        X = feature_df.copy()
        return X, [], available

    missing = [c for c in required_features if c not in feature_df.columns]
    if missing and on_missing == "error":
        raise KeyError(f"模型所需特征缺失: {missing}")

    X = feature_df.reindex(columns=required_features)
    X = X.apply(pd.to_numeric, errors="coerce")
    X = X.replace([np.inf, -np.inf], np.nan)
    if on_missing == "zero":
        X = X.fillna(fill_value)
    elif on_missing == "nan":
        pass
    else:
        raise ValueError(f"Unsupported on_missing: {on_missing}")

    used = [c for c in required_features if c in available]
    return X, missing, used


def predict_with_model(
    model_path: str | Path,
    all_features: dict[str, Any],
    fill_value: float = 0.0,
    on_missing: str = "zero",
) -> dict[str, Any]:
    model, feature_names, raw_obj = load_model_and_feature_names(model_path)
    feature_df = coerce_numeric_feature_df(all_features)
    X, missing, used = align_features_for_model(
        feature_df=feature_df,
        required_features=feature_names,
        fill_value=fill_value,
        on_missing=on_missing,
    )

    if hasattr(model, "predict"):
        pred = model.predict(X)
    else:
        raise TypeError(f"对象不支持 predict(): {type(model)}")

    pred_value = float(np.asarray(pred).reshape(-1)[0])
    return {
        "model_path": str(Path(model_path).resolve()),
        "model_type": type(model).__name__,
        "prediction": pred_value,
        "required_feature_count": None if feature_names is None else len(feature_names),
        "used_feature_count": len(used),
        "missing_features": missing,
        "used_features": used,
        "raw_loaded_type": type(raw_obj).__name__,
    }


# =========================
# 总流程
# =========================
def extract_all_multimodal_features(
    ply_path: str | Path,
    rgb_path: str | Path,
    mask_mode: str = "exg_otsu",
    exg_thr: float = 0.0,
    resize_max: int = 0,
    grid_size: float = 0.03,
    voxel_porosity: float = 0.03,
    height_bin: float = 0.05,
    hmin: float = 0.03,
    knn_normals: int = 30,
    normal_ds_voxel: float = 0.03,
) -> dict[str, Any]:
    ply_feats = extract_ply_features(
        ply_path=ply_path,
        grid_size=grid_size,
        voxel_porosity=voxel_porosity,
        height_bin=height_bin,
        hmin=hmin,
        knn_normals=knn_normals,
        normal_ds_voxel=normal_ds_voxel,
    )
    rgb_feats = extract_rgb_features(
        rgb_path=rgb_path,
        mask_mode=mask_mode,
        exg_thr=exg_thr,
        resize_max=resize_max,
    )

    sample_id = Path(ply_path).stem
    feats: dict[str, Any] = {"sample_id": sample_id}
    feats.update(ply_feats)
    feats.update(rgb_feats)
    return feats


def predict_single_sample(
    ply_path: str | Path,
    rgb_path: str | Path,
    ch_model_path: str | Path,
    lai_model_path: str | Path,
    mask_mode: str = "exg_otsu",
    exg_thr: float = 0.0,
    resize_max: int = 0,
    grid_size: float = 0.03,
    voxel_porosity: float = 0.03,
    height_bin: float = 0.05,
    hmin: float = 0.03,
    knn_normals: int = 30,
    normal_ds_voxel: float = 0.03,
    fill_value: float = 0.0,
    on_missing: str = "zero",
) -> dict[str, Any]:
    features = extract_all_multimodal_features(
        ply_path=ply_path,
        rgb_path=rgb_path,
        mask_mode=mask_mode,
        exg_thr=exg_thr,
        resize_max=resize_max,
        grid_size=grid_size,
        voxel_porosity=voxel_porosity,
        height_bin=height_bin,
        hmin=hmin,
        knn_normals=knn_normals,
        normal_ds_voxel=normal_ds_voxel,
    )

    ch_res = predict_with_model(ch_model_path, features, fill_value=fill_value, on_missing=on_missing)
    lai_res = predict_with_model(lai_model_path, features, fill_value=fill_value, on_missing=on_missing)

    return {
        "sample_id": features["sample_id"],
        "ply_path": str(Path(ply_path).resolve()),
        "rgb_path": str(Path(rgb_path).resolve()),
        "CH": ch_res["prediction"],
        "LAI": lai_res["prediction"],
        "feature_count_total": int(coerce_numeric_feature_df(features).shape[1]),
        "CH_model_info": ch_res,
        "LAI_model_info": lai_res,
        "all_features": features,
    }


def save_features_table(features: dict[str, Any], out_path: str | Path) -> None:
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    df_all = pd.DataFrame([features])
    df_num = coerce_numeric_feature_df(features)
    with pd.ExcelWriter(out_path, engine="openpyxl") as writer:
        df_all.to_excel(writer, index=False, sheet_name="all_features")
        df_num.to_excel(writer, index=False, sheet_name="numeric_features")


# =========================
# CLI
# =========================
def build_argparser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description="输入一个 PLY + 一张 RGB 图，直接预测 CH 和 LAI")
    ap.add_argument("--ply_path", type=str, required=True, help="单个 .ply 点云路径")
    ap.add_argument("--rgb_path", type=str, required=True, help="单张 RGB 图像路径")
    ap.add_argument("--ch_model", type=str, required=True, help="CH.joblib 路径")
    ap.add_argument("--lai_model", type=str, required=True, help="LAI.joblib 路径")

    ap.add_argument("--mask_mode", type=str, default="exg_otsu", choices=["none", "exg_otsu", "exg_fixed"])
    ap.add_argument("--exg_thr", type=float, default=0.0)
    ap.add_argument("--resize_max", type=int, default=0)

    ap.add_argument("--grid_size", type=float, default=0.03)
    ap.add_argument("--voxel_porosity", type=float, default=0.03)
    ap.add_argument("--height_bin", type=float, default=0.05)
    ap.add_argument("--hmin", type=float, default=0.03)
    ap.add_argument("--knn_normals", type=int, default=30)
    ap.add_argument("--normal_ds_voxel", type=float, default=0.03)

    ap.add_argument("--fill_value", type=float, default=0.0, help="缺失特征填充值")
    ap.add_argument("--on_missing", type=str, default="zero", choices=["zero", "nan", "error"], help="模型需要但当前缺失特征的处理方式")

    ap.add_argument("--save_features", type=str, default="", help="可选：保存提取特征到 xlsx")
    ap.add_argument("--save_json", type=str, default="", help="可选：保存预测结果到 json")
    return ap


def main() -> None:
    args = build_argparser().parse_args()

    result = predict_single_sample(
        ply_path=args.ply_path,
        rgb_path=args.rgb_path,
        ch_model_path=args.ch_model,
        lai_model_path=args.lai_model,
        mask_mode=args.mask_mode,
        exg_thr=args.exg_thr,
        resize_max=args.resize_max,
        grid_size=args.grid_size,
        voxel_porosity=args.voxel_porosity,
        height_bin=args.height_bin,
        hmin=args.hmin,
        knn_normals=args.knn_normals,
        normal_ds_voxel=args.normal_ds_voxel,
        fill_value=args.fill_value,
        on_missing=args.on_missing,
    )

    print("=" * 60)
    print(f"sample_id : {result['sample_id']}")
    print(f"PLY       : {result['ply_path']}")
    print(f"RGB       : {result['rgb_path']}")
    print(f"CH        : {result['CH']:.6f}")
    print(f"LAI       : {result['LAI']:.6f}")
    print("=" * 60)

    if result["CH_model_info"]["missing_features"]:
        print("[WARN] CH 模型缺失特征:")
        print(result["CH_model_info"]["missing_features"])
    if result["LAI_model_info"]["missing_features"]:
        print("[WARN] LAI 模型缺失特征:")
        print(result["LAI_model_info"]["missing_features"])

    if args.save_features:
        save_features_table(result["all_features"], args.save_features)
        print(f"[OK] 特征已保存: {Path(args.save_features).resolve()}")

    if args.save_json:
        out_path = Path(args.save_json)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        serializable = dict(result)
        serializable["all_features"] = {k: (float(v) if isinstance(v, (np.integer, np.floating)) else v) for k, v in result["all_features"].items()}
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(serializable, f, ensure_ascii=False, indent=2)
        print(f"[OK] 结果已保存: {out_path.resolve()}")


if __name__ == "__main__":
    main()
