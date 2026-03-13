#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Zhang's method (Zhengyou Zhang) camera intrinsic calibration using a flat chessboard.

Usage example:
    python zhang_calibration.py \
        --images "./calib_images/*.jpg" \
        --pattern_cols 9 --pattern_rows 6 \   # 代码中传入的是内角点，并按 (列, 行) 的顺序
        --square_size 25.0 \
        --outalignimgs calib_result.json \
        --debug_dir ./calib_debug --rational \
        --ini_out calibration.ini --ini_int

Notes
-----
- pattern_cols/pattern_rows are the number of *inner* corners of the chessboard
  (e.g., a board with 10x7 squares has 9x6 inner corners).
- square_size is the physical size of one chessboard square (mm, cm, or any unit).
- Provide 10–20 images taken at diverse angles/distances with a *flat* board.
- Implements Zhang's planar homography-based method via OpenCV's `calibrateCamera`.
"""
from __future__ import annotations
import argparse
import glob
import json
import math
import os
from dataclasses import dataclass
from typing import List, Tuple
import cv2
import numpy as np
import re


@dataclass
class CalibrationConfig:
    pattern_size: Tuple[int, int]  # (cols, rows) of inner corners
    square_size: float  # physical size per square
    rational: bool  # use CALIB_RATIONAL_MODEL


def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(
        description="Camera intrinsic calibration (Zhang) using chessboard images.")
    ap.add_argument("--images", type=str, required=True,
                    help="Glob pattern to images (e.g., './imgs/*.jpg' or 'C:/data/*/*.png')")
    ap.add_argument("--pattern_cols", type=int, required=True,
                    help="Number of inner corners along columns (e.g., 9)")
    ap.add_argument("--pattern_rows", type=int, required=True,
                    help="Number of inner corners along rows (e.g., 6)")
    ap.add_argument("--square_size", type=float, required=True,
                    help="Physical size of one square (e.g., 25.0 for 25 mm)")
    ap.add_argument("--out", type=str, default="calib_result.json",
                    help="Output JSON file to save calibration results")
    ap.add_argument("--debug_dir", type=str, default=None,
                    help="Optional folder to save corner overlays and undistortion demos")
    ap.add_argument("--rational", action="store_true",
                    help="Use CV_CALIB_RATIONAL_MODEL (k4..k6)")
    ap.add_argument("--show", action="store_true",
                    help="Show detection overlays interactively during processing")
    ap.add_argument("--ini_out", type=str, default="calibration.ini",
                    help="Path to write intrinsics INI with fx, fy, cx, cy")
    ap.add_argument("--ini_int", action="store_true",
                    help="Round fx, fy, cx, cy to integers in INI (e.g., fx=1200)")
    args = ap.parse_args()

    if args.pattern_cols <= 2 or args.pattern_rows <= 2:
        ap.error("pattern_cols and pattern_rows must be > 2 (inner corners)")
    return args


def collect_image_paths(pattern: str) -> List[str]:
    # Support recursive glob if ** is used
    paths = glob.glob(pattern, recursive=True)
    # Sort naturally by name for reproducibility
    paths = sorted(paths)
    # Filter images by common extensions if someone passed a directory glob
    valid_ext = {".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"}
    paths = [p for p in paths if os.path.splitext(p)[1].lower() in valid_ext]
    return paths


def build_object_points(pattern_size: Tuple[int, int], square_size: float) -> np.ndarray:
    cols, rows = pattern_size
    objp = np.zeros((rows * cols, 3), np.float32)
    # Create (x, y, z=0) grid in the chessboard plane; x along columns, y along rows
    grid = np.mgrid[0:cols, 0:rows].T.reshape(-1, 2)
    objp[:, :2] = grid * square_size
    return objp


def find_corners(gray: np.ndarray, pattern_size: Tuple[int, int]) -> Tuple[bool, np.ndarray | None]:
    """Try SB detector first (OpenCV >=4.5), fallback to classic method. Returns (ok, corners)."""
    cols, rows = pattern_size
    pattern = (cols, rows)

    # Newer, more robust detector if available
    if hasattr(cv2, "findChessboardCornersSB"):
        ret, corners = cv2.findChessboardCornersSB(gray, pattern, flags=0)
        if ret and corners is not None:
            # Ensure shape (N,1,2) for downstream APIs
            if corners.ndim == 2:
                corners = corners.reshape(-1, 1, 2)
            return True, corners.astype(np.float32)

    # Fallback: classic + subpixel refinement
    flags = cv2.CALIB_CB_ADAPTIVE_THRESH | cv2.CALIB_CB_NORMALIZE_IMAGE
    ret, corners = cv2.findChessboardCorners(gray, pattern, flags)
    if not ret:
        return False, None

    # Sub-pixel refinement
    term = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 100, 1e-4)
    corners = cv2.cornerSubPix(gray, corners, (11, 11), (-1, -1), term)
    return True, corners


def per_view_reprojection_errors(
        objpoints: List[np.ndarray],
        imgpoints: List[np.ndarray],
        rvecs: List[np.ndarray],
        tvecs: List[np.ndarray],
        K: np.ndarray,
        dist: np.ndarray,
) -> Tuple[List[float], float, float]:
    errs = []
    for objp, imgp, rvec, tvec in zip(objpoints, imgpoints, rvecs, tvecs):
        proj, _ = cv2.projectPoints(objp, rvec, tvec, K, dist)
        proj = proj.reshape(-1, 2)
        err = np.sqrt(np.mean(np.sum((proj - imgp.reshape(-1, 2)) ** 2, axis=1)))
        errs.append(float(err))
    mean_err = float(np.mean(errs)) if errs else float("nan")
    rms_err = float(np.sqrt(np.mean(np.square(errs)))) if errs else float("nan")
    return errs, mean_err, rms_err


def save_results(
        out_path: str,
        image_size: Tuple[int, int],
        cfg: CalibrationConfig,
        K: np.ndarray,
        dist: np.ndarray,
        rvecs: List[np.ndarray],
        tvecs: List[np.ndarray],
        per_view_errs: List[float],
        mean_err: float,
        rms_err: float,
        flags: int,
):
    result = {
        "image_size": [int(image_size[0]), int(image_size[1])],
        "pattern_cols": int(cfg.pattern_size[0]),
        "pattern_rows": int(cfg.pattern_size[1]),
        "square_size": float(cfg.square_size),
        "camera_matrix": K.tolist(),
        "dist_coeffs": dist.reshape(-1).tolist(),
        "reproj_error_per_view": per_view_errs,
        "reproj_error_mean": mean_err,
        "reproj_error_rms": rms_err,
        "flags": int(flags),
        # Save extrinsics if needed downstream
        "rvecs": [r.reshape(-1).tolist() for r in rvecs],
        "tvecs": [t.reshape(-1).tolist() for t in tvecs],
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)


def maybe_save_debug(
        debug_dir: str | None,
        img_path: str,
        image: np.ndarray,
        pattern_size: Tuple[int, int],
        corners: np.ndarray | None,
        success: bool,
):
    if not debug_dir:
        return
    os.makedirs(debug_dir, exist_ok=True)
    vis = image.copy()
    if success and corners is not None:
        cv2.drawChessboardCorners(vis, pattern_size, corners, success)
    base = os.path.splitext(os.path.basename(img_path))[0]
    out_path = os.path.join(debug_dir, f"{base}_corners.jpg")
    cv2.imwrite(out_path, vis)


def undistort_demo(debug_dir: str, K: np.ndarray, dist: np.ndarray, image_path: str):
    os.makedirs(debug_dir, exist_ok=True)
    img = cv2.imread(image_path, cv2.IMREAD_COLOR)
    if img is None:
        return
    h, w = img.shape[:2]
    newK, roi = cv2.getOptimalNewCameraMatrix(K, dist, (w, h), alpha=0.0)
    und = cv2.undistort(img, K, dist, None, newK)
    out_path = os.path.join(debug_dir, "undistorted_sample.jpg")
    cv2.imwrite(out_path, und)


def main():
    args = parse_args()
    pattern_size = (args.pattern_cols, args.pattern_rows)
    cfg = CalibrationConfig(pattern_size=pattern_size, square_size=args.square_size, rational=bool(args.rational))

    img_paths = collect_image_paths(args.images)
    if len(img_paths) == 0:
        raise SystemExit(f"No images matched pattern: {args.images}")

    objpoints: List[np.ndarray] = []
    imgpoints: List[np.ndarray] = []
    image_size = None

    objp_template = build_object_points(cfg.pattern_size, cfg.square_size)

    print(f"Found {len(img_paths)} images. Detecting corners...")

    for i, p in enumerate(img_paths, 1):
        img = cv2.imread(p, cv2.IMREAD_COLOR)
        if img is None:
            print(f"[WARN] Failed to read image: {p}")
            continue
        if image_size is None:
            image_size = (img.shape[1], img.shape[0])  # (w, h)
        elif image_size != (img.shape[1], img.shape[0]):
            print(f"[WARN] Skipping {p}: image size differs from first image {image_size}")
            continue

        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        ok, corners = find_corners(gray, cfg.pattern_size)

        if ok and corners is not None:
            objpoints.append(objp_template.copy())
            imgpoints.append(corners)
            print(f"  [{i:02d}/{len(img_paths)}] OK - {os.path.basename(p)}")
        else:
            print(f"  [{i:02d}/{len(img_paths)}] FAIL - {os.path.basename(p)}")

        # debug overlay
        maybe_save_debug(args.debug_dir, p, img, cfg.pattern_size, corners, ok)

        if args.show:
            vis = img.copy()
            if ok and corners is not None:
                cv2.drawChessboardCorners(vis, cfg.pattern_size, corners, ok)
            cv2.imshow("corners", vis)
            key = cv2.waitKey(200) & 0xFF
            if key == 27:  # ESC to break
                break

    if args.show:
        cv2.destroyAllWindows()

    if len(objpoints) < 5:
        raise SystemExit("Not enough valid detections. Need at least ~5 views of the board.")

    # Calibration flags
    flags = 0
    if cfg.rational:
        flags |= cv2.CALIB_RATIONAL_MODEL

    print("\nCalibrating...")
    ret, K, dist, rvecs, tvecs = cv2.calibrateCamera(
        objectPoints=objpoints,
        imagePoints=imgpoints,
        imageSize=image_size,
        cameraMatrix=None,
        distCoeffs=None,
        flags=flags,
        criteria=(cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 200, 1e-8),
    )

    print(f"RMS reprojection error (OpenCV): {ret:.6f} px")
    per_view_errs, mean_err, rms_err = per_view_reprojection_errors(
        objpoints, imgpoints, rvecs, tvecs, K, dist)

    print("\n===== Intrinsics (K) =====")
    print(K)
    print("\n===== Distortion coeffs =====")
    print(dist.reshape(-1))
    print("\nPer-view reprojection errors (px):")
    for i, e in enumerate(per_view_errs):
        print(f"  view {i:02d}: {e:.6f}")
    print(f"Mean error: {mean_err:.6f} px | RMS of per-view errors: {rms_err:.6f} px")

    # Save results
    save_results(
        out_path=args.out,
        image_size=image_size,
        cfg=cfg,
        K=K,
        dist=dist,
        rvecs=rvecs,
        tvecs=tvecs,
        per_view_errs=per_view_errs,
        mean_err=mean_err,
        rms_err=rms_err,
        flags=flags,
    )
    print(f"Saved calibration to: {args.out}")

    ini_path = args.ini_out or "calibration_inner_dist.ini"
    write_intrinsics_ini(ini_path, K, dist, as_int=True)

    # Undistortion demo on the first successfully detected image
    if args.debug_dir:
        # Choose the first image that succeeded
        for p in img_paths:
            img = cv2.imread(p, cv2.IMREAD_COLOR)
            if img is None or (img.shape[1], img.shape[0]) != image_size:
                continue
            gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            ok, _ = find_corners(gray, cfg.pattern_size)
            if ok:
                undistort_demo(args.debug_dir, K, dist, p)
                print(f"Undistorted sample saved under: {args.debug_dir}")
                break


def write_intrinsics_ini(ini_path: str, K: np.ndarray, dist: np.ndarray, as_int: bool = False):
    fx, fy, cx, cy = float(K[0, 0]), float(K[1, 1]), float(K[0, 2]), float(K[1, 2])
    if as_int:
        fx, fy, cx, cy = int(round(fx)), int(round(fy)), int(round(cx)), int(round(cy))

    d = dist.reshape(-1).tolist() if dist is not None else []
    k1 = d[0] if len(d) > 0 else 0.0
    k2 = d[1] if len(d) > 1 else 0.0
    p1 = d[2] if len(d) > 2 else 0.0
    p2 = d[3] if len(d) > 3 else 0.0
    k3 = d[4] if len(d) > 4 else 0.0
    k4 = d[5] if len(d) > 5 else 0.0
    k5 = d[6] if len(d) > 6 else 0.0
    k6 = d[7] if len(d) > 7 else 0.0

    with open(ini_path, "w", encoding="utf-8") as f:
        f.write(
            f"fx={fx}\n"
            f"fy={fy}\n"
            f"cx={cx}\n"
            f"cy={cy}\n"
            f"k1={k1}\n"
            f"k2={k2}\n"
            f"p1={p1}\n"
            f"p2={p2}\n"
            f"k3={k3}\n"
            f"k4={k4}\n"
            f"k5={k5}\n"
            f"k6={k6}\n"
        )
    print(f"Saved intrinsics INI to: {ini_path}")


if __name__ == "__main__":
    main()
