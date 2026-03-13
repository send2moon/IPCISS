# -*- coding: utf-8 -*-
'''
Color correction：cubic polynomial (RGB->RGB) + Ridge regularization + 3D LUT(33^3) three-linear interpolation application

Sequence of color block numbers：
  Top left =1, go from top to bottom, then from left to right (column-major order), bottom right = 24
  The grid is 6 rows by 4 columns.

Color Truth Table：colortrue.xlsx (id, R, G, B（0-255）)
'''

import os
import glob
import argparse
import numpy as np
import pandas as pd
import cv2

ROWS, COLS = 6, 4


def read_true_colors_xlsx(xlsx_path: str) -> np.ndarray:
    df = pd.read_excel(xlsx_path)
    need = ["id", "R", "G", "B"]
    for c in need:
        if c not in df.columns:
            raise ValueError(f"colortrue.xlsx Missing columns {c}，Need columns：{need}")
    df = df.sort_values("id")
    if len(df) != 24:
        print(f"[WARN] Number of truth table rows={len(df)}")
    return df[["R", "G", "B"]].to_numpy(dtype=np.float64)


def id_from_rc(r, c, order_mode="col"):
    if order_mode == "col":
        return c * ROWS + r + 1
    return r * COLS + c + 1


def rc_from_id(pid, order_mode="col"):
    pid = int(pid)
    pid0 = pid - 1
    if order_mode == "col":
        c = pid0 // ROWS
        r = pid0 % ROWS
        return r, c
    r = pid0 // COLS
    c = pid0 % COLS
    return r, c


def order_quad_points(pts_xy: np.ndarray) -> np.ndarray:
    pts = pts_xy.astype(np.float32)
    s = pts.sum(axis=1)
    d = np.diff(pts, axis=1).reshape(-1)
    tl = pts[np.argmin(s)]
    br = pts[np.argmax(s)]
    tr = pts[np.argmin(d)]
    bl = pts[np.argmax(d)]
    return np.stack([tl, tr, br, bl], axis=0)


def refine_corners(gray, corners_xy):
    c = corners_xy.astype(np.float32).reshape(-1, 1, 2)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 1e-4)
    cv2.cornerSubPix(gray, c, winSize=(7, 7), zeroZone=(-1, -1), criteria=criteria)
    return c.reshape(-1, 2)


def rect_iou(a, b) -> float:
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ax2, ay2 = ax + aw, ay + ah
    bx2, by2 = bx + bw, by + bh
    ix1, iy1 = max(ax, bx), max(ay, by)
    ix2, iy2 = min(ax2, bx2), min(ay2, by2)
    iw, ih = max(0, ix2 - ix1), max(0, iy2 - iy1)
    inter = iw * ih
    if inter <= 0:
        return 0.0
    union = aw * ah + bw * bh - inter
    return float(inter / union)


def nms_rects(rects, scores, iou_th=0.6):
    idx = np.argsort(scores)[::-1].tolist()
    keep = []
    while idx:
        i = idx.pop(0)
        keep.append(i)
        idx = [j for j in idx if rect_iou(rects[i], rects[j]) < iou_th]
    return keep


def kmeans_1d(x, k, iters=50):
    x = np.asarray(x, dtype=np.float64).reshape(-1)
    if len(x) < k:
        raise ValueError("kmeans_1d: not enough points")
    init_p = (np.arange(k) * 100.0 / k) + 50.0 / k
    centers = np.percentile(x, init_p)
    for _ in range(iters):
        dist = np.abs(x[:, None] - centers[None, :])
        lab = dist.argmin(axis=1)
        new_centers = centers.copy()
        for i in range(k):
            sel = x[lab == i]
            if len(sel) > 0:
                new_centers[i] = sel.mean()
            else:
                new_centers[i] = x[np.random.randint(0, len(x))]
        if np.allclose(new_centers, centers):
            break
        centers = new_centers
    return centers


def detect_colorchecker_quad(img_bgr, expected_aspect=4/6, aspect_tol=0.22, min_area_ratio=0.02):
    h, w = img_bgr.shape[:2]
    gray = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blur, 50, 150)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=1)
    edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=1)

    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    img_area = float(h * w)

    best, best_score = None, -1e18
    for cnt in contours:
        area = cv2.contourArea(cnt)
        if area < img_area * min_area_ratio:
            continue
        peri = cv2.arcLength(cnt, True)
        approx = cv2.approxPolyDP(cnt, 0.02 * peri, True)
        if len(approx) != 4:
            continue
        quad = order_quad_points(approx.reshape(-1, 2))

        widthA = np.linalg.norm(quad[2] - quad[3])
        widthB = np.linalg.norm(quad[1] - quad[0])
        heightA = np.linalg.norm(quad[1] - quad[2])
        heightB = np.linalg.norm(quad[0] - quad[3])
        ww = (widthA + widthB) / 2.0
        hh = (heightA + heightB) / 2.0
        if hh < 1e-6:
            continue
        aspect = ww / hh
        if abs(aspect - expected_aspect) > aspect_tol:
            continue

        score = area - (abs(aspect - expected_aspect) * area * 2.0)
        if score > best_score:
            best_score, best = score, quad

    if best is None:
        raise RuntimeError("No color card quadrilateral frame was detected (automatic detection failed)")

    best = refine_corners(cv2.cvtColor(img_bgr, cv2.COLOR_BGR2GRAY), best)
    return order_quad_points(best)


def manual_pick_quad(img_bgr, window_name="Pick 4 corners: TL->TR->BR->BL"):
    show = img_bgr.copy()
    pts = []
    RED, WHITE, GREEN = (0, 0, 255), (255, 255, 255), (0, 255, 0)

    def redraw():
        nonlocal show
        show = img_bgr.copy()
        for i, (x, y) in enumerate(pts):
            xi, yi = int(x), int(y)
            cv2.circle(show, (xi, yi), 8, WHITE, 2, cv2.LINE_AA)
            cv2.circle(show, (xi, yi), 6, RED, -1, cv2.LINE_AA)
            label = f"{i+1}"
            cv2.putText(show, label, (xi + 10, yi - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, WHITE, 4, cv2.LINE_AA)
            cv2.putText(show, label, (xi + 10, yi - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, RED, 2, cv2.LINE_AA)
        if len(pts) >= 2:
            for i in range(len(pts) - 1):
                p1 = (int(pts[i][0]), int(pts[i][1]))
                p2 = (int(pts[i+1][0]), int(pts[i+1][1]))
                cv2.line(show, p1, p2, RED, 2, cv2.LINE_AA)
        if len(pts) == 4:
            poly = np.array(pts, dtype=np.int32).reshape(-1, 1, 2)
            cv2.polylines(show, [poly], True, RED, 2, cv2.LINE_AA)
        tip = "Click: 1=TL 2=TR 3=BR 4=BL | Enter=OK | Z/Backspace=Undo | Esc=Cancel"
        cv2.putText(show, tip, (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 0), 4, cv2.LINE_AA)
        cv2.putText(show, tip, (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, GREEN, 2, cv2.LINE_AA)

    def on_mouse(event, x, y, flags, param):
        nonlocal pts
        if event == cv2.EVENT_LBUTTONDOWN and len(pts) < 4:
            pts.append((float(x), float(y)))
            redraw()

    cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
    cv2.setMouseCallback(window_name, on_mouse)
    redraw()
    while True:
        cv2.imshow(window_name, show)
        key = cv2.waitKey(20) & 0xFF
        if key == 27:
            cv2.destroyWindow(window_name)
            raise RuntimeError("Manual picking canceled by user (Esc).")
        if key in (8, ord('z'), ord('Z')):
            if pts:
                pts.pop()
                redraw()
        if key in (13, 10):
            if len(pts) != 4:
                print(f"[WARN] Need 4 points, now {len(pts)}.")
            else:
                cv2.destroyWindow(window_name)
                return np.array(pts, dtype=np.float32)


def warp_perspective(img_bgr, quad_tltrbrbl, out_w=800, out_h=1200):
    src = quad_tltrbrbl.astype(np.float32)
    dst = np.array([[0, 0], [out_w - 1, 0], [out_w - 1, out_h - 1], [0, out_h - 1]], dtype=np.float32)
    M = cv2.getPerspectiveTransform(src, dst)
    return cv2.warpPerspective(img_bgr, M, (out_w, out_h), flags=cv2.INTER_LINEAR)


def detect_patch_rects(warped_bgr, iou_th=0.6):
    H, W = warped_bgr.shape[:2]
    total = float(H * W)
    gray = cv2.cvtColor(warped_bgr, cv2.COLOR_BGR2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)

    edges = cv2.Canny(blur, 40, 140)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=1)
    edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=2)

    contours, _ = cv2.findContours(edges, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

    min_area = total * 0.008
    max_area = total * 0.15

    cand_rects, cand_scores = [], []
    for cnt in contours:
        area = cv2.contourArea(cnt)
        if area < min_area or area > max_area:
            continue
        peri = cv2.arcLength(cnt, True)
        approx = cv2.approxPolyDP(cnt, 0.02 * peri, True)
        if len(approx) != 4:
            continue
        if not cv2.isContourConvex(approx):
            continue
        x, y, w, h = cv2.boundingRect(approx)
        ar = w / max(1, h)
        if not (0.75 <= ar <= 1.33):
            continue
        if w < W * 0.05 or h < H * 0.05:
            continue
        cand_rects.append((x, y, w, h))
        cand_scores.append(float(area))

    if len(cand_rects) == 0:
        return []

    keep = nms_rects(cand_rects, cand_scores, iou_th=iou_th)
    rects = [cand_rects[i] for i in keep]

    if len(rects) > 24:
        areas = np.array([r[2] * r[3] for r in rects], dtype=np.float64)
        med = np.median(areas)
        sel = [r for r, a in zip(rects, areas) if 0.5 * med <= a <= 1.8 * med]
        rects = sel if len(sel) >= 12 else rects
        rects = sorted(rects, key=lambda r: r[2] * r[3], reverse=True)[:24]

    return rects


def assign_rects_to_grid_partial(rects):
    if len(rects) == 0:
        return [[None for _ in range(COLS)] for __ in range(ROWS)]

    centers = np.array([[r[0] + r[2]/2.0, r[1] + r[3]/2.0] for r in rects], dtype=np.float64)
    cx, cy = centers[:, 0], centers[:, 1]

    col_centers = np.sort(kmeans_1d(cx, COLS)) if len(cx) >= COLS else np.sort(cx)
    row_centers = np.sort(kmeans_1d(cy, ROWS)) if len(cy) >= ROWS else np.sort(cy)

    grid = [[None for _ in range(COLS)] for __ in range(ROWS)]
    used = set()

    items = []
    for i, (x, y) in enumerate(centers):
        c = int(np.argmin(np.abs(col_centers - x)))
        r = int(np.argmin(np.abs(row_centers - y)))
        items.append((abs(col_centers[c] - x) + abs(row_centers[r] - y), i, r, c))
    items.sort(key=lambda t: t[0])

    for _, i, r, c in items:
        if (r, c) not in used and grid[r][c] is None:
            grid[r][c] = rects[i]
            used.add((r, c))
    return grid


def center50_box(rect):
    x, y, w, h = rect
    sx0 = int(x + 0.25 * w)
    sx1 = int(x + 0.75 * w)
    sy0 = int(y + 0.25 * h)
    sy1 = int(y + 0.75 * h)
    return sx0, sy0, max(1, sx1 - sx0), max(1, sy1 - sy0)


def sample_patch_rgb_center50(warped_bgr, rect, use_median=True):
    sx, sy, sw, sh = center50_box(rect)
    roi = warped_bgr[max(0, sy):min(warped_bgr.shape[0], sy + sh),
                     max(0, sx):min(warped_bgr.shape[1], sx + sw)]
    if roi.size == 0:
        return np.array([np.nan, np.nan, np.nan], dtype=np.float64)
    flat = roi.reshape(-1, 3).astype(np.float64)
    bgr = np.median(flat, axis=0) if use_median else flat.mean(axis=0)
    return bgr[::-1]  # RGB


def compute_avg_wh(grid_rects):
    ws, hs = [], []
    for r in range(ROWS):
        for c in range(COLS):
            rect = grid_rects[r][c]
            if rect is None:
                continue
            ws.append(rect[2])
            hs.append(rect[3])
    if len(ws) == 0:
        return None
    return float(np.mean(ws)), float(np.mean(hs))


def make_rect_from_center(cx, cy, w, h, W, H):
    x = int(round(cx - w / 2.0))
    y = int(round(cy - h / 2.0))
    x = max(0, min(W - 2, x))
    y = max(0, min(H - 2, y))
    w = int(round(w)); h = int(round(h))
    w = max(2, min(W - x, w))
    h = max(2, min(H - y, h))
    return (x, y, w, h)


def manual_fill_missing_patches(warped_bgr, grid_rects, manual_flags, order_mode="col"):
    H, W = warped_bgr.shape[:2]
    avg = compute_avg_wh(grid_rects)
    if avg is None:
        avg_w = W / (COLS + 1.5)
        avg_h = H / (ROWS + 1.5)
    else:
        avg_w, avg_h = avg

    missing = []
    for pid in range(1, ROWS * COLS + 1):
        r, c = rc_from_id(pid, order_mode=order_mode)
        if grid_rects[r][c] is None:
            missing.append((pid, r, c))
    if len(missing) == 0:
        return grid_rects, manual_flags

    manual_stack = []
    window = "Manual fill missing patches (click center)"
    RED, WHITE, GREEN = (0, 0, 255), (255, 255, 255), (0, 255, 0)

    def redraw():
        vis = warped_bgr.copy()

        for rr in range(ROWS):
            for cc in range(COLS):
                rect = grid_rects[rr][cc]
                if rect is None:
                    continue
                sx, sy, sw, sh = center50_box(rect)
                cv2.rectangle(vis, (sx, sy), (sx + sw, sy + sh), RED, 2, cv2.LINE_AA)
                if manual_flags[rr][cc]:
                    cx = sx + sw // 2
                    cy = sy + sh // 2
                    cv2.circle(vis, (cx, cy), 6, RED, -1, cv2.LINE_AA)

        remaining = [(pid, r, c) for (pid, r, c) in missing if grid_rects[r][c] is None]
        if remaining:
            pid, r, c = remaining[0]
            tip1 = f"Missing {len(remaining)}. Click CENTER for id={pid} (row={r}, col={c})."
        else:
            tip1 = "All missing patches filled. Press Enter/q to finish."
        tip2 = "Keys: Z/Backspace=Undo | Enter/q=Finish | Esc=Cancel"

        cv2.putText(vis, tip1, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 0), 4, cv2.LINE_AA)
        cv2.putText(vis, tip1, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, GREEN, 2, cv2.LINE_AA)
        cv2.putText(vis, tip2, (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 0), 4, cv2.LINE_AA)
        cv2.putText(vis, tip2, (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, WHITE, 2, cv2.LINE_AA)
        return vis

    def on_mouse(event, x, y, flags, param):
        if event != cv2.EVENT_LBUTTONDOWN:
            return
        remaining = [(pid, r, c) for (pid, r, c) in missing if grid_rects[r][c] is None]
        if not remaining:
            return
        pid, r, c = remaining[0]
        rect = make_rect_from_center(x, y, avg_w, avg_h, W, H)
        grid_rects[r][c] = rect
        manual_flags[r][c] = True
        manual_stack.append((pid, r, c))

    cv2.namedWindow(window, cv2.WINDOW_NORMAL)
    cv2.setMouseCallback(window, on_mouse)

    while True:
        vis = redraw()
        cv2.imshow(window, vis)
        key = cv2.waitKey(20) & 0xFF

        if key == 27:
            cv2.destroyWindow(window)
            raise RuntimeError("Manual fill canceled by user (Esc).")

        if key in (8, ord('z'), ord('Z')):
            if manual_stack:
                pid, r, c = manual_stack.pop()
                grid_rects[r][c] = None
                manual_flags[r][c] = False

        if key in (13, 10, ord('q'), ord('Q')):
            cv2.destroyWindow(window)
            return grid_rects, manual_flags


def build_records_from_grid(warped_bgr, grid_rects, manual_flags, order_mode="col"):
    records = []
    for r in range(ROWS):
        for c in range(COLS):
            rect = grid_rects[r][c]
            if rect is None:
                continue
            rgb = sample_patch_rgb_center50(warped_bgr, rect, use_median=True)
            pid = id_from_rc(r, c, order_mode=order_mode)
            x, y, w, h = rect
            sx, sy, sw, sh = center50_box(rect)
            records.append({
                "id": int(pid),
                "row": int(r), "col": int(c),
                "meas_R": float(rgb[0]), "meas_G": float(rgb[1]), "meas_B": float(rgb[2]),
                "x": int(x), "y": int(y), "w": int(w), "h": int(h),
                "sx": int(sx), "sy": int(sy), "sw": int(sw), "sh": int(sh),
                "source": "manual" if manual_flags[r][c] else "auto"
            })
    records.sort(key=lambda d: d["id"])
    return records


def visualize_center50(warped_bgr, records, save_path=None):
    vis = warped_bgr.copy()
    RED = (0, 0, 255)
    for rec in records:
        sx, sy, sw, sh = rec["sx"], rec["sy"], rec["sw"], rec["sh"]
        pid = rec["id"]
        rgb = (int(rec["meas_R"]), int(rec["meas_G"]), int(rec["meas_B"]))

        cv2.rectangle(vis, (sx, sy), (sx + sw, sy + sh), RED, 2, cv2.LINE_AA)
        if rec.get("source") == "manual":
            cx = sx + sw // 2
            cy = sy + sh // 2
            cv2.circle(vis, (cx, cy), 6, RED, -1, cv2.LINE_AA)

        txt = f"{pid}:({rgb[0]},{rgb[1]},{rgb[2]})"
        tx = sx + 4
        ty = sy + int(sh * 0.65)
        cv2.putText(vis, txt, (tx, ty),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 0, 0), 4, cv2.LINE_AA)
        cv2.putText(vis, txt, (tx, ty),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2, cv2.LINE_AA)

    if save_path:
        os.makedirs(os.path.dirname(save_path) or ".", exist_ok=True)
        cv2.imwrite(save_path, vis)
    return vis


def export_records(records, out_csv):
    df = pd.DataFrame(records)
    os.makedirs(os.path.dirname(out_csv) or ".", exist_ok=True)
    df.to_csv(out_csv, index=False, encoding="utf-8-sig")
    return df


def poly3_features(rgb01: np.ndarray) -> np.ndarray:
    R = rgb01[:, 0]
    G = rgb01[:, 1]
    B = rgb01[:, 2]

    R2, G2, B2 = R * R, G * G, B * B
    RG, RB, GB = R * G, R * B, G * B

    R3, G3, B3 = R2 * R, G2 * G, B2 * B

    R2G = R2 * G
    R2B = R2 * B
    G2R = G2 * R
    G2B = G2 * B
    B2R = B2 * R
    B2G = B2 * G
    RGB = R * G * B

    ones = np.ones_like(R)

    X = np.stack([
        ones,
        R, G, B,
        R2, G2, B2, RG, RB, GB,
        R3, G3, B3,
        R2G, R2B, G2R, G2B, B2R, B2G,
        RGB
    ], axis=1)
    return X.astype(np.float64)


def fit_poly3_ridge_from_records(records, true_rgb_24, ridge_lambda=1e-2):
    ids = [r["id"] for r in records]
    meas255 = np.array([[r["meas_R"], r["meas_G"], r["meas_B"]] for r in records], dtype=np.float64)
    true255 = np.array([true_rgb_24[i - 1] for i in ids], dtype=np.float64)

    meas01 = np.clip(meas255 / 255.0, 0.0, 1.0)
    true01 = np.clip(true255 / 255.0, 0.0, 1.0)

    X = poly3_features(meas01)  # (N,20)
    Y = true01                 # (N,3)

    # Ridge: (X^T X + lam*I)^{-1} X^T Y
    XtX = X.T @ X
    I = np.eye(X.shape[1], dtype=np.float64)
    I[0, 0] = 0.0  # 常数项不正则（更稳）
    A = XtX + ridge_lambda * I
    W = np.linalg.solve(A, X.T @ Y)  # (20,3)

    Y_hat = X @ W
    err01 = Y_hat - Y
    rmse01 = np.sqrt(np.mean(err01 ** 2, axis=0))
    rmse255 = rmse01 * 255.0
    total_rmse255 = float(np.sqrt(np.mean((err01 * 255.0) ** 2)))
    return W, rmse255, total_rmse255


def build_3d_lut_from_poly(W_poly, lut_size=33):
    S = int(lut_size)
    grid = np.linspace(0.0, 1.0, S, dtype=np.float64)

    rr, gg, bb = np.meshgrid(grid, grid, grid, indexing="ij")
    pts = np.stack([rr.reshape(-1), gg.reshape(-1), bb.reshape(-1)], axis=1)  # (S^3,3)

    X = poly3_features(pts)               # (S^3,20)
    out = X @ W_poly                      # (S^3,3)
    out = np.clip(out, 0.0, 1.0).astype(np.float32)
    lut = out.reshape(S, S, S, 3)
    return lut


def apply_3d_lut_trilinear(img_bgr, lut, chunk_pixels=500_000):
    S = lut.shape[0]
    img = img_bgr.astype(np.float32)
    rgb01 = img[..., ::-1] / 255.0  # (H,W,3)
    H, W = rgb01.shape[:2]
    flat = rgb01.reshape(-1, 3)

    out = np.empty_like(flat, dtype=np.float32)

    n = flat.shape[0]
    for start in range(0, n, chunk_pixels):
        end = min(n, start + chunk_pixels)
        p = flat[start:end]  # (M,3)

        x = np.clip(p[:, 0] * (S - 1), 0, S - 1)
        y = np.clip(p[:, 1] * (S - 1), 0, S - 1)
        z = np.clip(p[:, 2] * (S - 1), 0, S - 1)

        x0 = np.floor(x).astype(np.int32)
        y0 = np.floor(y).astype(np.int32)
        z0 = np.floor(z).astype(np.int32)
        x1 = np.minimum(x0 + 1, S - 1)
        y1 = np.minimum(y0 + 1, S - 1)
        z1 = np.minimum(z0 + 1, S - 1)

        xd = (x - x0).astype(np.float32)
        yd = (y - y0).astype(np.float32)
        zd = (z - z0).astype(np.float32)

        c000 = lut[x0, y0, z0]
        c100 = lut[x1, y0, z0]
        c010 = lut[x0, y1, z0]
        c110 = lut[x1, y1, z0]
        c001 = lut[x0, y0, z1]
        c101 = lut[x1, y0, z1]
        c011 = lut[x0, y1, z1]
        c111 = lut[x1, y1, z1]

        c00 = c000 * (1 - xd)[:, None] + c100 * xd[:, None]
        c10 = c010 * (1 - xd)[:, None] + c110 * xd[:, None]
        c01 = c001 * (1 - xd)[:, None] + c101 * xd[:, None]
        c11 = c011 * (1 - xd)[:, None] + c111 * xd[:, None]

        c0 = c00 * (1 - yd)[:, None] + c10 * yd[:, None]
        c1 = c01 * (1 - yd)[:, None] + c11 * yd[:, None]

        c = c0 * (1 - zd)[:, None] + c1 * zd[:, None]
        out[start:end] = c

    out_rgb = np.clip(out.reshape(H, W, 3) * 255.0, 0, 255).astype(np.uint8)
    return out_rgb[..., ::-1]  # RGB->BGR


def save_model_npz(path, W_poly, lut, meta: dict):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    np.savez(
        path,
        model_type="poly3_ridge_lut",
        W_poly=W_poly.astype(np.float64),
        lut=lut.astype(np.float32),
        **{k: np.array(v) for k, v in meta.items()}
    )


def load_model_npz(path):
    d = np.load(path, allow_pickle=True)
    model_type = str(d["model_type"])
    W_poly = d["W_poly"]
    lut = d["lut"]
    return model_type, W_poly, lut, d


def correct_images_in_folder(input_dir, output_dir, lut, recursive=False,
                             patterns=("*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.bmp"),
                             suffix="_color_corrected"):
    input_dir = os.path.abspath(input_dir)
    output_dir = os.path.abspath(output_dir)
    os.makedirs(output_dir, exist_ok=True)

    if recursive:
        files = []
        for root, _, _ in os.walk(input_dir):
            for p in patterns:
                files.extend(glob.glob(os.path.join(root, p)))
    else:
        files = []
        for p in patterns:
            files.extend(glob.glob(os.path.join(input_dir, p)))

    n_ok = 0
    for fp in files:
        img = cv2.imread(fp, cv2.IMREAD_COLOR)
        if img is None:
            print("[WARN] Skip unreadable:", fp)
            continue

        out = apply_3d_lut_trilinear(img, lut)

        rel = os.path.relpath(fp, input_dir)
        out_path = os.path.join(output_dir, rel)
        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        base, ext = os.path.splitext(out_path)
        out_path = base + suffix + ext

        cv2.imwrite(out_path, out)
        n_ok += 1
        if n_ok % 20 == 0:
            print(f"[INFO] Processed {n_ok} images ...")

    print(f"[DONE] Batch corrected {n_ok} images -> {output_dir}")


def main():
    ap = argparse.ArgumentParser()

    ap.add_argument("--colortrue_xlsx", type=str, default="", help="Color Truth Table colortrue.xlsx（id,R,G,B）")
    ap.add_argument("--image_with_checker", type=str, default="", help="")
    ap.add_argument("--save_model", type=str, default="calib_poly3_lut.npz", help="Save the high-precision model npz")
    ap.add_argument("--debug_dir", type=str, default="", help="")

    ap.add_argument("--out_w", type=int, default=800, help="Perspective correction output width")
    ap.add_argument("--out_h", type=int, default=1200, help="Perspective correction output height")
    ap.add_argument("--min_area_ratio", type=float, default=0.02, help="Automatically detect the minimum area proportion of the outer frame")
    ap.add_argument("--aspect_tol", type=float, default=0.22, help="Frame width-to-height tolerance")
    ap.add_argument("--order_mode", type=str, default="col", choices=["col", "row"],
                    help="col=From top to bottom and then from left to right")

    ap.add_argument("--ridge_lambda", type=float, default=1e-2, help="Ridge Regular strength（1e-2）")
    ap.add_argument("--lut_size", type=int, default=33, help="3D LUT size（33）")
    ap.add_argument("--min_fit_patches", type=int, default=12,
                    help="The minimum number of color blocks that can fit a high-order model")

    ap.add_argument("--load_model", type=str, default="", help="Load the high-precision model npz")
    ap.add_argument("--image_in", type=str, default="", help="A single image to be corrected")
    ap.add_argument("--image_out", type=str, default="", help="Single image output (Save as new image)")

    ap.add_argument("--input_dir", type=str, default="", help="Batch input folder")
    ap.add_argument("--out_dir", type=str, default="", help="Batch output folder")
    ap.add_argument("--recursive", action="store_true", help="Batch recursive subfolders")
    ap.add_argument("--suffix", type=str, default="_color_corrected", help="Batch output of file name suffixes")

    args = ap.parse_args()

    if args.image_with_checker:
        if not args.colortrue_xlsx:
            raise ValueError("Calibration requires --colortrue_xlsx")

        true_rgb = read_true_colors_xlsx(args.colortrue_xlsx)

        img = cv2.imread(args.image_with_checker, cv2.IMREAD_COLOR)
        if img is None:
            raise FileNotFoundError(args.image_with_checker)

        try:
            quad = detect_colorchecker_quad(
                img, expected_aspect=4/6, aspect_tol=args.aspect_tol, min_area_ratio=args.min_area_ratio
            )
            print("[INFO] Auto quad detection success.")
        except Exception as e:
            print(f"[WARN] Auto quad detection failed: {e}")
            print("[INFO] Switch to manual corner picking...")
            quad = manual_pick_quad(img, window_name="Pick 4 corners: TL->TR->BR->BL")

        warped = warp_perspective(img, quad, out_w=args.out_w, out_h=args.out_h)

        rects = detect_patch_rects(warped, iou_th=0.6)
        grid_rects = assign_rects_to_grid_partial(rects)
        manual_flags = [[False for _ in range(COLS)] for __ in range(ROWS)]

        n_auto = sum(1 for r in range(ROWS) for c in range(COLS) if grid_rects[r][c] is not None)
        if n_auto < ROWS * COLS:
            print(f"[WARN] Auto detected/assigned patches = {n_auto}/24. Enter manual fill mode...")
            grid_rects, manual_flags = manual_fill_missing_patches(
                warped, grid_rects, manual_flags, order_mode=args.order_mode
            )

        records = build_records_from_grid(warped, grid_rects, manual_flags, order_mode=args.order_mode)
        n_final = len(records)
        print(f"[INFO] Final records = {n_final}/24 (auto+manual).")

        if n_final < args.min_fit_patches:
            print(f"[WARN] Number of effective color blocks {n_final} < {args.min_fit_patches}. The model may be unstable three times.")

        W_poly, rmse3_255, total_rmse255 = fit_poly3_ridge_from_records(
            records, true_rgb, ridge_lambda=float(args.ridge_lambda)
        )
        print(f"[INFO] Poly3+Ridge RMSE (R,G,B) = {rmse3_255}, total RMSE = {total_rmse255:.3f} (in 0-255)")

        lut = build_3d_lut_from_poly(W_poly, lut_size=int(args.lut_size))
        save_model_npz(
            args.save_model,
            W_poly=W_poly,
            lut=lut,
            meta={
                "ridge_lambda": float(args.ridge_lambda),
                "lut_size": int(args.lut_size),
                "rmse_R": float(rmse3_255[0]),
                "rmse_G": float(rmse3_255[1]),
                "rmse_B": float(rmse3_255[2]),
                "rmse_total": float(total_rmse255),
                "n_records": int(n_final),
            }
        )
        print(f"[OK] Saved high-precision model: {args.save_model}")

        if args.debug_dir:
            os.makedirs(args.debug_dir, exist_ok=True)

            dbg = img.copy()
            q = quad.astype(int)
            cv2.polylines(dbg, [q.reshape(-1, 1, 2)], True, (0, 255, 0), 2)
            cv2.imwrite(os.path.join(args.debug_dir, "detected_quad.png"), dbg)
            cv2.imwrite(os.path.join(args.debug_dir, "warped.png"), warped)

            vis_path = os.path.join(args.debug_dir, "patches_center50_annotated.png")
            visualize_center50(warped, records, save_path=vis_path)

            csv_path = os.path.join(args.debug_dir, "measured_patches_rgb.csv")
            export_records(records, csv_path)

            print(f"[INFO] Debug saved to: {args.debug_dir}")

    if args.load_model:
        model_type, W_poly, lut, meta = load_model_npz(args.load_model)
        print(f"[INFO] Loaded model: {args.load_model}, type={model_type}, lut_size={lut.shape[0]}")

        if args.image_in:
            img2 = cv2.imread(args.image_in, cv2.IMREAD_COLOR)
            if img2 is None:
                raise FileNotFoundError(args.image_in)

            out = apply_3d_lut_trilinear(img2, lut)

            if not args.image_out:
                base, ext = os.path.splitext(args.image_in)
                args.image_out = base + "_color_corrected" + ext

            os.makedirs(os.path.dirname(args.image_out) or ".", exist_ok=True)
            cv2.imwrite(args.image_out, out)
            print(f"[OK] Saved corrected image: {args.image_out}")

        if args.input_dir:
            if not args.out_dir:
                raise ValueError("Batch correction requires providing simultaneously --input_dir and --out_dir")
            correct_images_in_folder(
                args.input_dir, args.out_dir, lut,
                recursive=args.recursive,
                suffix=args.suffix
            )

    if (not args.image_with_checker) and (not args.load_model):
        print("[INFO] Unspecified task.")


if __name__ == "__main__":
    main()
