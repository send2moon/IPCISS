#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NIR → RGB transformation matrix H estimation

Estimate a SINGLE global NIR→RGB homography H across a batch of image pairs by
aggregating feature correspondences from all pairs and running one RANSAC.

Saves rich visualizations and diagnostics:
- Per-pair:
  * preprocessed inputs
  * raw good-match lines
  * global-H inlier/outlier match lines
  * warped overlay (NIR→RGB) and side-by-side
- Global:
  * nir2rgb_h.json  (H + global/per-pair metrics + settings)
  * global_correspondences.csv (each match with error and inlier flag wrt global H)
  * optional tiled gallery of overlays

USAGE
  # pairs.csv columns: id,rgb,nir
  python RGBNIRHomographyH.py \
    --list pairs.csv \
    --outalignimgs out_global_h \
    --feature orb \
    --preproc grad \
    --ransac-reproj 3.0 \
    --nir-ref ref.jpg \
    --max-per-pair 800 \
    --overlay-alpha 0.5 \
    --gallery

  # Optional: refine the global H by ECC using one representative pair (e.g., id='ref1')
  python calibrate_rgb_nir_homography_global_v3.py --list pairs.csv --outalignimgs outalignimgs --ecc-ref-id ref1 --ecc

Notes
- Histogram matching (--nir-ref) is used only to improve feature matching;
  original NIR is used for warps/overlays to reflect true appearance.
- For extremely dark NIRs, try --preproc grad.
"""
import argparse, csv, json, math, os, sys
from pathlib import Path
import numpy as np
import cv2

def ensure_dir(p):
    Path(p).mkdir(parents=True, exist_ok=True)

def imread_gray(path):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        raise FileNotFoundError(path)
    if img.ndim == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    return img

def hist_match_gray(source_gray, reference_gray):
    if reference_gray is None:
        return source_gray
    s = source_gray.ravel(); r = reference_gray.ravel()
    s_vals, bin_idx, s_counts = np.unique(s, return_inverse=True, return_counts=True)
    r_vals, r_counts = np.unique(r, return_counts=True)
    s_quantiles = np.cumsum(s_counts).astype(np.float64) / s.size
    r_quantiles = np.cumsum(r_counts).astype(np.float64) / r.size
    interp_r_vals = np.interp(s_quantiles, r_quantiles, r_vals)
    return interp_r_vals[bin_idx].reshape(source_gray.shape).astype(np.uint8)

def preproc(gray, mode="none"):
    if mode == "none":
        return gray
    if mode == "clahe":
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8,8))
        return clahe.apply(gray)
    if mode == "grad":
        g = cv2.GaussianBlur(gray, (0,0), 1.2)
        gx = cv2.Sobel(g, cv2.CV_32F, 1, 0, ksize=3)
        gy = cv2.Sobel(g, cv2.CV_32F, 0, 1, ksize=3)
        mag = cv2.magnitude(gx, gy)
        mag = cv2.normalize(mag, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
        return mag
    return gray

def build_feature(name="orb"):
    name = name.lower()
    if name == "orb":
        return cv2.ORB_create(6000)
    if name == "akaze":
        return cv2.AKAZE_create()
    if name == "sift":
        if not hasattr(cv2, "SIFT_create"):
            raise RuntimeError("SIFT not available in your OpenCV build")
        return cv2.SIFT_create(nfeatures=8000)
    raise ValueError("Unknown feature: "+name)

def ratio_test_knn_match(desc1, desc2, normType, k=2, ratio=0.75):
    bf = cv2.BFMatcher(normType, crossCheck=False)
    mknn = bf.knnMatch(desc1, desc2, k=k)
    good = []
    for m in mknn:
        if len(m) < 2: 
            continue
        a, b = m
        if a.distance < ratio * b.distance:
            good.append(a)
    return good

def draw_matches_lines(img1, kps1, img2, kps2, matches, inlier_mask=None, max_draw=2000):
    h1,w1 = img1.shape[:2]
    h2,w2 = img2.shape[:2]
    H = max(h1,h2); W = w1 + w2
    canvas = np.zeros((H, W, 3), dtype=np.uint8)
    a = cv2.cvtColor(img1, cv2.COLOR_GRAY2BGR) if img1.ndim==2 else img1.copy()
    b = cv2.cvtColor(img2, cv2.COLOR_GRAY2BGR) if img2.ndim==2 else img2.copy()
    canvas[:h1,:w1] = a
    canvas[:h2,w1:w1+w2] = b

    step = max(1, len(matches)//max_draw)
    for i, m in enumerate(matches[::step]):
        pt1 = tuple(np.int32(kps1[m.queryIdx].pt))
        pt2 = tuple(np.int32(kps2[m.trainIdx].pt))
        c = (0,255,0)
        if inlier_mask is not None:
            is_in = bool(inlier_mask[i*step])
            c = (0,255,0) if is_in else (0,0,255)
        x2 = pt2[0] + w1; y2 = pt2[1]
        cv2.circle(canvas, pt1, 3, c, -1, lineType=cv2.LINE_AA)
        cv2.circle(canvas, (x2,y2), 3, c, -1, lineType=cv2.LINE_AA)
        cv2.line(canvas, pt1, (x2,y2), c, 1, lineType=cv2.LINE_AA)
    return canvas

def warp_overlay(rgb, nir, H, alpha=0.5):
    h,w = rgb.shape[:2]
    nir_w = cv2.warpPerspective(nir, H, (w,h), flags=cv2.INTER_LINEAR)
    nir_color = cv2.cvtColor(nir_w, cv2.COLOR_GRAY2BGR) if nir_w.ndim==2 else nir_w
    rgb_c = cv2.cvtColor(rgb, cv2.COLOR_GRAY2BGR) if rgb.ndim==2 else rgb
    blend = cv2.addWeighted(rgb_c, alpha, nir_color, 1-alpha, 0)
    side = np.hstack([rgb_c, nir_color, blend])
    return blend, side

def gather_matches(rgb_path, nir_path, out_dir, fe, pre, nir_ref=None, max_per_pair=None, idname="pair"):
    rgb0 = imread_gray(rgb_path); nir0 = imread_gray(nir_path)
    nir_match = hist_match_gray(nir0, nir_ref) if nir_ref is not None else nir0
    rgb_p = preproc(rgb0, pre); nir_p = preproc(nir_match, pre)

    cv2.imwrite(str(Path(out_dir)/f"{idname}_rgb_p.png"), rgb_p)
    cv2.imwrite(str(Path(out_dir)/f"{idname}_nir_p.png"), nir_p)

    k1,d1 = fe.detectAndCompute(rgb_p, None)
    k2,d2 = fe.detectAndCompute(nir_p, None)
    if d1 is None or d2 is None or len(k1)<8 or len(k2)<8:
        return [], [], k1, k2, rgb0, nir0, rgb_p, nir_p

    norm = cv2.NORM_HAMMING if isinstance(fe, cv2.ORB) or isinstance(fe, cv2.AKAZE) else cv2.NORM_L2
    good = ratio_test_knn_match(d1, d2, normType=norm, k=2, ratio=0.75)
    if len(good) < 8:
        return [], [], k1, k2, rgb0, nir0, rgb_p, nir_p

    # Save raw match lines
    raw_canvas = draw_matches_lines(rgb_p, k1, nir_p, k2, good, inlier_mask=None)
    cv2.imwrite(str(Path(out_dir)/f"{idname}_matches_raw.png"), raw_canvas)

    # Build src/dst lists
    src = np.float32([k2[m.trainIdx].pt for m in good])  # NIR
    dst = np.float32([k1[m.queryIdx].pt for m in good])  # RGB

    if (max_per_pair is not None) and (len(src) > max_per_pair):
        # uniform subsample to keep coverage
        idx = np.linspace(0, len(src)-1, max_per_pair).astype(int)
        src = src[idx]; dst = dst[idx]; good = [good[i] for i in idx]

    return src, dst, k1, k2, rgb0, nir0, rgb_p, nir_p

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", required=True, help="CSV columns: id,rgb,nir")
    ap.add_argument("--outalignimgs", required=True, help="Output folder")
    ap.add_argument("--feature", default="orb", choices=["orb","sift","akaze"])
    ap.add_argument("--preproc", default="grad", choices=["none","clahe","grad"])
    ap.add_argument("--ransac-reproj", type=float, default=3.0)
    ap.add_argument("--nir-ref", default=None, help="Reference gray for NIR histogram matching (optional)")
    ap.add_argument("--max-per-pair", type=int, default=1000, help="Max matches to keep per pair")
    ap.add_argument("--overlay-alpha", type=float, default=0.5, help="Alpha for overlay")
    ap.add_argument("--gallery", action="store_true", help="Save a tiled gallery of a few overlays")
    ap.add_argument("--ecc", action="store_true", help="Refine global H by ECC using --ecc-ref-id")
    ap.add_argument("--ecc-ref-id", default=None, help="ID (from CSV) to use for ECC refinement")
    args = ap.parse_args()

    ensure_dir(args.out)
    fe = build_feature(args.feature)

    nir_ref = imread_gray(args.nir_ref) if args.nir_ref else None

    # Read list
    rows = []
    with open(args.list, "r", encoding="utf-8-sig") as f:
        rd = csv.DictReader(f)
        for r in rd:
            rows.append(r)
    if not rows:
        print("Empty list.", file=sys.stderr); sys.exit(2)

    # Accumulate correspondences
    all_src = []; all_dst = []; per = {}
    cache_imgs = {}
    for r in rows:
        rid = r.get("id","pair")
        rgb = r["rgb"]; nir = r["nir"]
        print(f"[{rid}] extracting matches...")
        src, dst, k1, k2, rgb0, nir0, rgb_p, nir_p = gather_matches(rgb, nir, args.out, fe, args.preproc, nir_ref, args.max_per_pair, rid)
        per[rid] = {
            "matches": int(len(src)),
            "rgb": rgb, "nir": nir
        }
        cache_imgs[rid] = (rgb0, nir0, rgb_p, nir_p, k1, k2, src, dst)
        if len(src) > 0:
            all_src.append(src); all_dst.append(dst)
    if not all_src:
        print("No matches from any pair.", file=sys.stderr); sys.exit(3)

    all_src = np.vstack(all_src).reshape(-1,1,2)
    all_dst = np.vstack(all_dst).reshape(-1,1,2)
    print(f"[GLOBAL] total matches: {len(all_src)}")

    # Global RANSAC for H (NIR->RGB)
    H, mask = cv2.findHomography(all_src, all_dst, cv2.RANSAC, ransacReprojThreshold=float(args.ransac_reproj))
    if H is None:
        print("Global findHomography failed", file=sys.stderr); sys.exit(4)
    inliers_total = int(mask.sum())
    total = int(len(all_src))
    print(f"[GLOBAL] inliers: {inliers_total}/{total} (ratio={inliers_total/max(1,total):.3f})")

    # Optional ECC refinement using a chosen reference pair
    ecc_rho = None
    if args.ecc and args.ecc_ref_id and args.ecc_ref_id in cache_imgs:
        try:
            rgb0, nir0, *_ = cache_imgs[args.ecc_ref_id]
            r = rgb0.astype(np.float32)/255.0
            n = nir0.astype(np.float32)/255.0
            maxw = 1280
            scale = 1.0
            if r.shape[1] > maxw:
                scale = maxw / r.shape[1]
                r = cv2.resize(r, (0,0), fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
                n = cv2.resize(n, (0,0), fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
            H_init = H.copy().astype(np.float32)
            if scale != 1.0:
                S = np.array([[scale,0,0],[0,scale,0],[0,0,1]],dtype=np.float32)
                S_inv = np.array([[1/scale,0,0],[0,1/scale,0],[0,0,1]],dtype=np.float32)
                H_init = (S @ H_init @ S_inv).astype(np.float32)
            warp_mode = cv2.MOTION_HOMOGRAPHY
            criteria = (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 150, 1e-6)
            H_ecc = H_init.copy()
            ecc_rho, H_ecc = cv2.findTransformECC(r, n, H_ecc, warp_mode, criteria, None, 1)
            if scale != 1.0:
                H = (S_inv @ H_ecc @ S).astype(np.float64)
            else:
                H = H_ecc.astype(np.float64)
            print(f"[ECC] rho={ecc_rho:.6f}")
        except cv2.error:
            ecc_rho = None

    # Per-pair inlier/outlier masks w.r.t. GLOBAL H and visualizations
    rows_out = []
    for rid, data in per.items():
        rgb0, nir0, rgb_p, nir_p, k1, k2, src, dst = cache_imgs[rid]
        # compute errors wrt global H
        if len(src)==0:
            per[rid]["inliers"] = 0; per[rid]["inlier_ratio"] = 0.0
            continue
        src_h = cv2.convertPointsToHomogeneous(src.reshape(-1,1,2)).reshape(-1,3).T  # 3xN
        pr = (H @ src_h).T
        pr = (pr[:,:2] / pr[:,2:3]).reshape(-1,2)
        err = np.linalg.norm(pr - dst.reshape(-1,2), axis=1)
        inl = (err <= float(args.ransac_reproj)).astype(np.uint8)
        per[rid]["inliers"] = int(inl.sum())
        per[rid]["inlier_ratio"] = float(per[rid]["inliers"]/max(1,per[rid]["matches"]))

        # Save inlier/outlier match lines
        canvas = draw_matches_lines(rgb_p, k1, nir_p, k2,
                                    [type('m', (), {'queryIdx':0,'trainIdx':0})]*len(src),  # dummy list; we will bypass indices
                                    inlier_mask=inl.tolist())
        # The above hack won't map indices; instead, draw using points to be exact:
        # Make a manual canvas:
        h1,w1 = rgb_p.shape[:2]; h2,w2 = nir_p.shape[:2]
        Hh = max(h1,h2); Ww = w1 + w2
        canvas = np.zeros((Hh, Ww, 3), dtype=np.uint8)
        a = cv2.cvtColor(rgb_p, cv2.COLOR_GRAY2BGR) if rgb_p.ndim==2 else rgb_p.copy()
        b = cv2.cvtColor(nir_p, cv2.COLOR_GRAY2BGR) if nir_p.ndim==2 else nir_p.copy()
        canvas[:h1,:w1] = a; canvas[:h2,w1:w1+w2] = b
        for (x_r,y_r),(x_n,y_n), flag in zip(dst.reshape(-1,2), src.reshape(-1,2), inl.tolist()):
            c = (0,255,0) if flag==1 else (0,0,255)
            pt1 = (int(round(x_r)), int(round(y_r)))
            pt2 = (int(round(x_n+w1)), int(round(y_n)))
            cv2.circle(canvas, pt1, 3, c, -1, lineType=cv2.LINE_AA)
            cv2.circle(canvas, pt2, 3, c, -1, lineType=cv2.LINE_AA)
            cv2.line(canvas, pt1, pt2, c, 1, lineType=cv2.LINE_AA)
        cv2.imwrite(str(Path(args.out)/f"{rid}_matches_global_inliers.png"), canvas)

        # Save overlays using GLOBAL H
        blend, side = warp_overlay(rgb0, nir0, H, alpha=float(args.overlay_alpha))
        cv2.imwrite(str(Path(args.out)/f"{rid}_warp_overlay_global.png"), blend)
        cv2.imwrite(str(Path(args.out)/f"{rid}_warp_side_by_side_global.png"), side)

        # Append to global correspondences table
        for (xn,yn),(xr,yr),e,flag in zip(src.reshape(-1,2), dst.reshape(-1,2), err.tolist(), inl.tolist()):
            rows_out.append([rid, xn, yn, xr, yr, e, flag])

    # Save global correspondences CSV
    with open(Path(args.out)/"global_correspondences.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["id","u_nir","v_nir","u_rgb","v_rgb","reproj_err_px","inlier_flag"])
        wr.writerows(rows_out)

    # Save JSON
    result = {
        "H": H.reshape(-1).tolist(),
        "ransac_reproj": float(args.ransac_reproj),
        "feature": args.feature,
        "preproc": args.preproc,
        "nir_ref_used": bool(args.nir_ref is not None),
        "total_matches": int(total),
        "total_inliers": int(inliers_total),
        "inlier_ratio": float(inliers_total/max(1,total)),
        "ecc_rho": float(ecc_rho) if ecc_rho is not None else None,
        "per_pair": per
    }
    with open(Path(args.out)/"nir2rgb_h.json", "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)
    print("[OK] Saved global H to nir2rgb_h.json")

    # Optional gallery (tile a few overlays)
    if args.gallery:
        ids = list(per.keys())[:6]
        tiles = []
        for rid in ids:
            rgb0, nir0, *_ = cache_imgs[rid]
            blend, _ = warp_overlay(rgb0, nir0, H, alpha=float(args.overlay_alpha))
            tiles.append(blend)
        if tiles:
            # make a simple 3x2 grid (pad if needed)
            while len(tiles) < 6:
                tiles.append(np.zeros_like(tiles[0]))
            r1 = np.hstack(tiles[:3])
            r2 = np.hstack(tiles[3:6])
            grid = np.vstack([r1, r2])
            cv2.imwrite(str(Path(args.out)/"global_overlay_gallery.png"), grid)

if __name__ == "__main__":
    main()
