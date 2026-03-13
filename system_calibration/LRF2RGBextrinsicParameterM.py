# -*- coding: utf-8 -*-
"""
Indirect calibration of extrinsic parameters of cross-modal sensors：Single-point laser ranging module（LRF） -> RGB camera

Python 3.8 / OpenCV 4.x / SciPy

INPUT：
  1) calibration_inner.ini     # Camera internal parameters and distortion
  2) laser_observations.csv    # image_path,u,v
  3) Board dimensions：board_rows, board_cols（Interior corner point），square_size

OUTPUT：
  External reference matrix M(4x4)、re-projection error statistics
"""

import os
import re
import cv2
import math
import argparse
import numpy as np
import pandas as pd
from dataclasses import dataclass
from scipy.optimize import least_squares

# ---------- I/O: read intrinsics ----------
def read_calib_ini(path_ini: str):
    if not os.path.isfile(path_ini):
        raise FileNotFoundError(f"The internal document does not exist: {path_ini}")
    pat = re.compile(r'^\s*([A-Za-z0-9_]+)\s*=\s*([\-+eE0-9\.]+)\s*$')
    kv = {}
    with open(path_ini, 'r', encoding='utf-8') as f:
        for line in f:
            m = pat.match(line.strip())
            if m:
                kv[m.group(1).lower()] = float(m.group(2))

    for r in ['fx','fy','cx','cy']:
        if r not in kv:
            raise ValueError(f"Insufficient internal information {r}")
    K = np.array([[kv['fx'], 0.,        kv['cx']],
                  [0.,        kv['fy'], kv['cy']],
                  [0.,        0.,        1. ]], dtype=np.float64)
    dist = [kv[k] for k in ['k1','k2','p1','p2','k3','k4','k5','k6'] if k in kv]
    dist = np.array(dist, dtype=np.float64).reshape(-1,1) if len(dist)>0 else np.zeros((5,1),dtype=np.float64)
    return K, dist

# ---------- chessboard utilities ----------
@dataclass
class BoardSpec:
    rows: int
    cols: int
    square_size: float

def make_board_object_points(spec: BoardSpec):
    objp = np.zeros((spec.rows*spec.cols, 3), np.float64)
    objp[:, :2] = np.mgrid[0:spec.cols, 0:spec.rows].T.reshape(-1, 2)
    objp[:, :2] *= spec.square_size
    return objp  # (N,3)

def detect_board_pose(image_bgr, K, dist, spec: BoardSpec):
    gray = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2GRAY)
    pattern_size = (spec.cols, spec.rows)
    flags = cv2.CALIB_CB_ADAPTIVE_THRESH + cv2.CALIB_CB_NORMALIZE_IMAGE
    found, corners = cv2.findChessboardCorners(gray, pattern_size, flags=flags)
    if not found:
        return False, None, None

    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 100, 1e-6)
    corners = cv2.cornerSubPix(gray, corners, (11,11), (-1,-1), criteria)

    objp = make_board_object_points(spec)  # (N,3)
    # PnP
    success, rvec, tvec = cv2.solvePnP(objp, corners, K, dist, flags=cv2.SOLVEPNP_ITERATIVE)
    if not success:
        return False, None, None
    return True, rvec, tvec

def plane_from_board_pose(rvec, tvec):
    R, _ = cv2.Rodrigues(rvec)
    n = R @ np.array([0.,0.,1.], dtype=np.float64)
    t = tvec.reshape(3,)
    delta = - n.dot(t)
    norm = np.linalg.norm(n)
    if norm > 0:
        n = n / norm
        delta = delta / norm
    return n, delta

# ---------- model & optimizer ----------
def rodrigues_to_R(rvec3):
    R, _ = cv2.Rodrigues(rvec3.reshape(3,1))
    return R

def project_cam_points(K, dist, Xc):
    # Xc: (3,) in camera coordinates
    obj = Xc.reshape(1,1,3).astype(np.float64)
    zero = np.zeros((3,1), np.float64)
    uv, _ = cv2.projectPoints(obj, zero, zero, K, dist)
    return uv.reshape(2,)

def residuals_rt(x, K, dist, d_lrf, planes, uv_obs):
    r = x[:3]
    t = x[3:6]
    R = rodrigues_to_R(r)
    t = t.reshape(3,)
    Rd = R @ d_lrf.reshape(3,)

    res = []
    for (n, delta), uv in zip(planes, uv_obs):
        denom = float(n.dot(Rd))
        if abs(denom) < 1e-10:
            res.extend([1e3, 1e3])
            continue
        lam = - (n.dot(t) + float(delta)) / denom
        Pc = t + lam * Rd
        if Pc[2] <= 1e-6:
            res.extend([5e2, 5e2]); continue
        uv_hat = project_cam_points(K, dist, Pc)
        res.extend((uv_hat - uv).tolist())
    return np.array(res, dtype=np.float64)

def optimize_extrinsic(K, dist, d_lrf, planes, uv_obs, x0=None):
    if x0 is None:
        x0 = np.zeros(6, dtype=np.float64)  # rvec=[0,0,0], t=[0,0,0]
    ls = least_squares(
        fun=residuals_rt, x0=x0,
        args=(K, dist, d_lrf, planes, uv_obs),
        method='trf', loss='huber', f_scale=3.0, max_nfev=200
    )
    return ls

def rt_to_M(r, t):
    R = rodrigues_to_R(r)
    M = np.eye(4, dtype=np.float64)
    M[:3,:3] = R
    M[:3, 3] = t.reshape(3,)
    return M

# ---------- utility: color sampling ----------
def sample_bilinear_color(img_bgr, u, v):
    h, w = img_bgr.shape[:2]
    if not (0 <= u < w-1 and 0 <= v < h-1):
        uu, vv = int(round(u)), int(round(v))
        if 0 <= uu < w and 0 <= vv < h:
            b,g,r = img_bgr[vv, uu].tolist()
            return (int(r), int(g), int(b))
        return None
    x0, y0 = int(math.floor(u)), int(math.floor(v))
    dx, dy = u - x0, v - y0
    p00 = img_bgr[y0,     x0    ].astype(np.float64)
    p10 = img_bgr[y0,     x0 + 1].astype(np.float64)
    p01 = img_bgr[y0 + 1, x0    ].astype(np.float64)
    p11 = img_bgr[y0 + 1, x0 + 1].astype(np.float64)
    top = (1-dx)*p00 + dx*p10
    bot = (1-dx)*p01 + dx*p11
    val = (1-dy)*top + dy*bot
    b,g,r = val.tolist()
    return (int(round(r)), int(round(g)), int(round(b)))

def get_rgb_color_for_point(point_lrf, image_bgr, M, K, dist):
    Xl = np.asarray(point_lrf, dtype=np.float64).reshape(3,1)
    Rc = M[:3,:3]; tc = M[:3,3].reshape(3,1)
    Xc = Rc @ Xl + tc
    if Xc[2,0] <= 1e-6:
        return None, (np.nan, np.nan), False
    uv, _ = cv2.projectPoints(Xc.reshape(1,1,3), np.zeros((3,1)), np.zeros((3,1)), K, dist)
    u, v = float(uv[0,0,0]), float(uv[0,0,1])
    color = sample_bilinear_color(image_bgr, u, v)
    ok = color is not None
    return color, (u, v), ok

# ---------- main pipeline ----------
def main(args):
    K0, dist0 = read_calib_ini(args.calib_ini)

    use_ud = args.undistort
    if use_ud:
        img0 = cv2.imread(pd.read_csv(args.obs_csv)['image_path'].iloc[0], cv2.IMREAD_COLOR)
        h0, w0 = img0.shape[:2]
        Kp, roi = cv2.getOptimalNewCameraMatrix(K0, dist0, (w0,h0), alpha=0)
        map1, map2 = cv2.initUndistortRectifyMap(K0, dist0, R=None, newCameraMatrix=Kp,
                                                 size=(w0,h0), m1type=cv2.CV_32FC1)
        K = Kp
        dist = np.zeros((5,1), np.float64)
    else:
        K, dist = K0, dist0

    print("[INFO] K=\n", K)
    print("[INFO] dist(len={}): {}".format(dist.size, dist.ravel()))

    df = pd.read_csv(args.obs_csv)
    if not set(['image_path','u','v']).issubset(df.columns):
        raise ValueError("laser_observations.csv should include the following columns: image_path,u,v")

    spec = BoardSpec(rows=args.board_rows, cols=args.board_cols, square_size=args.square_size)

    planes, uv_list, used_imgs = [], [], []
    n_total, n_ok = 0, 0
    for _, row in df.iterrows():
        img_path = str(row['image_path'])

        uv = np.array([[ [float(row['u']), float(row['v'])] ]], dtype=np.float64)
        if use_ud:
            uv_ud = cv2.undistortPoints(uv, K, dist, P=K)
            uv_list.append(uv_ud.reshape(2,))
        else:
            uv_list.append(uv.reshape(2,))

        n_total += 1
        if not os.path.isfile(img_path):
            print(f"[WARN] Missing image: {img_path}，skip")
            continue

        img = cv2.imread(img_path, cv2.IMREAD_COLOR)
        if use_ud:
            img_ud = cv2.remap(img, map1, map2, interpolation=cv2.INTER_LINEAR)
            img_for_board = img_ud
        else:
            img_for_board = img

        ok, rvec_cb, tvec_cb = detect_board_pose(img_for_board, K, dist, spec)

        if not ok:
            print(f"[WARN] No chessboard was detected: {img_path}，skip")
            continue

        n, delta = plane_from_board_pose(rvec_cb, tvec_cb)
        planes.append((n, delta))
        used_imgs.append(img_path)
        n_ok += 1

    if len(planes) < 5:
        raise RuntimeError(f"Insufficient number of valid samples（{len(planes)}）, should ≥5")

    planes = np.array(planes, dtype=object).tolist()
    uv_obs = np.array(uv_list, dtype=np.float64)

    print(f"[INFO] Total sample size {n_total} items. Success {n_ok}. Start optimizing ...")

    d_lrf = np.array([args.d_lrf_x, args.d_lrf_y, args.d_lrf_z], dtype=np.float64)
    nrm = np.linalg.norm(d_lrf)
    if nrm == 0:
        raise ValueError("d_lrf cannot be the zero vector.")
    d_lrf /= nrm

    res = optimize_extrinsic(K, dist, d_lrf, planes, uv_obs, x0=None)
    r_opt = res.x[:3]; t_opt = res.x[3:6]
    M = rt_to_M(r_opt, t_opt)

    errs = residuals_rt(res.x, K, dist, d_lrf, planes, uv_obs).reshape(-1,2)
    per_pt = np.linalg.norm(errs, axis=1)
    rmse = math.sqrt(np.mean(per_pt**2))
    med  = float(np.median(per_pt))
    inlier = np.mean(per_pt < 2.0)
    print("\n===== Result（LRF→CAM）=====")
    R,_ = cv2.Rodrigues(r_opt)
    print("R =\n", R)
    print("t =\n", t_opt)
    print("M =\n", M)
    print("\n===== error statistics =====")
    print(f"Sample size: {len(per_pt)}")
    print(f"RMSE: {rmse:.3f} px")
    print(f"Median: {med:.3f} px")
    print(f"Inlier@2px: {inlier*100:.1f}%")

    os.makedirs(os.path.dirname(args.out_txt) or ".", exist_ok=True)
    np.savetxt(args.out_txt, M, fmt="%.10f")
    print(f"[OK]: {args.out_txt}")


def _project_cam_point(K, dist, Xc, model='plumb_bob'):
    Xc = np.asarray(Xc, dtype=np.float64).reshape(1, 1, 3)
    r0 = np.zeros((3, 1), np.float64)
    t0 = np.zeros((3, 1), np.float64)
    if model == 'fisheye':
        uv, _ = cv2.fisheye.projectPoints(Xc, r0, t0, K, dist.reshape(-1, 1))
    else:
        uv, _ = cv2.projectPoints(Xc, r0, t0, K, dist)
    return float(uv[0, 0, 0]), float(uv[0, 0, 1])


def _sample_bilinear_rgb(img_bgr, u, v):
    h, w = img_bgr.shape[:2]
    if not (0 <= u < w - 1 and 0 <= v < h - 1):
        uu, vv = int(round(u)), int(round(v))
        if 0 <= uu < w and 0 <= vv < h:
            b, g, r = img_bgr[vv, uu].tolist()
            return (int(r), int(g), int(b))
        return None
    x0, y0 = int(math.floor(u)), int(math.floor(v))
    dx, dy = u - x0, v - y0
    p00 = img_bgr[y0, x0].astype(np.float64)
    p10 = img_bgr[y0, x0 + 1].astype(np.float64)
    p01 = img_bgr[y0 + 1, x0].astype(np.float64)
    p11 = img_bgr[y0 + 1, x0 + 1].astype(np.float64)
    top = (1 - dx) * p00 + dx * p10
    bot = (1 - dx) * p01 + dx * p11
    val = (1 - dy) * top + dy * bot
    b, g, r = val.tolist()
    return (int(round(r)), int(round(g)), int(round(b)))


def query_rgb_at_lrf_point(x, y, z, img_bgr, M, K, dist, model='plumb_bob'):
    point_lrf = [x, y, z]
    Xl = np.asarray(point_lrf, dtype=np.float64).reshape(3, 1)
    R = M[:3, :3];
    t = M[:3, 3].reshape(3, 1)
    Xc = R @ Xl + t
    Z = float(Xc[2, 0])
    if Z <= 1e-9:
        return (np.nan, np.nan), None, False

    u0, v0 = _project_cam_point(K, dist, Xc.reshape(3, ), model=model)
    du = 0.0015*z*z - 0.8819*z + 126.8
    dv = -0.0166*z*z + 4.8466*z - 450.17
    u = u0 + du
    v = v0 + dv

    color_rgb = _sample_bilinear_rgb(img_bgr, u, v)
    ok = color_rgb is not None

    return (u, v), color_rgb, ok


def fix_M_for_lrf_origin(M, offset_L):
    M = np.asarray(M, dtype=np.float64)
    d = np.asarray(offset_L, dtype=np.float64).reshape(3,)
    R = M[:3, :3]
    t = M[:3, 3]
    t_new = t + R @ d
    M_new = M.copy()
    M_new[:3, 3] = t_new
    return M_new


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--calib_ini",   type=str, required=True, help="Camera internal parameters ini")
    ap.add_argument("--obs_csv",     type=str, required=True, help="image_path,u,v")
    ap.add_argument("--board_rows",  type=int, required=True, help="")
    ap.add_argument("--board_cols",  type=int, required=True, help="")
    ap.add_argument("--square_size", type=float, required=True, help="")
    ap.add_argument("--out_txt",     type=str, default="M_lrf_to_cam.txt", help="Output external parameters txt")
    ap.add_argument("--d_lrf_x",     type=float, default=0.0, help="LRF beam direction x (LRF system)")
    ap.add_argument("--d_lrf_y",     type=float, default=0.0, help="LRF beam direction y (LRF system)")
    ap.add_argument("--d_lrf_z",     type=float, default=1.0, help="LRF beam direction z (LRF system)")
    ap.add_argument("--undistort", action="store_true",
                    help="Unify the image and the pixel of the light spot, then correct the distortion and optimize it.")
    args = ap.parse_args()
    main(args)
