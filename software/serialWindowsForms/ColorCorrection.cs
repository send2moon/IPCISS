using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MathNet.Numerics.LinearAlgebra;


namespace ColorCalibrationDemo
{
    public struct Rgb24
    {
        public byte R;
        public byte G;
        public byte B;

        public Rgb24(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public class CalibrationOptions
    {
        public int OutW { get; set; } = 800;
        public int OutH { get; set; } = 1200;
        public double MinAreaRatio { get; set; } = 0.02;
        public double AspectTol { get; set; } = 0.22;
        public string OrderMode { get; set; } = "col";
        public double RidgeLambda { get; set; } = 1e-2;
        public int LutSize { get; set; } = 33;
        public int MinFitPatches { get; set; } = 12;
    }

    public static class ColorCalibrationApi73
    {
        private const int ROWS = 6;
        private const int COLS = 4;

        public static int BuildCalibrationFileFromChecker(
            Mat checkerBgr,
            string colorTrueXlsxPath,
            string saveNpzPath,
            Point2f[] manualCorners = null,
            CalibrationOptions options = null)
        {
            if (checkerBgr == null || checkerBgr.Empty())
                throw new ArgumentException("checkerBgr is null");
            if (checkerBgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("checkerBgr must be CV_8UC3");
            if (!File.Exists(colorTrueXlsxPath))
                throw new FileNotFoundException("Can't find colortrue.xlsx", colorTrueXlsxPath);

            if (options == null)
                options = new CalibrationOptions();

            double[,] trueRgb24 = ReadTrueColorsXlsx(colorTrueXlsxPath);

            Point2f[] quad = null;

            if (manualCorners != null && manualCorners.Length == 4)
            {
                quad = OrderQuadPoints(manualCorners);
            }
            else
            {
                try
                {
                    quad = DetectColorCheckerQuad(
                        checkerBgr,
                        4.0 / 6.0,
                        options.AspectTol,
                        options.MinAreaRatio);
                    Console.WriteLine("[INFO] Auto quad detection success.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WARN] Auto quad detection failed: " + ex.Message);
                    Console.WriteLine("[INFO] Switch to manual corner picking...");
                    quad = ManualPickQuad(checkerBgr, "Pick 4 corners: TL->TR->BR->BL");
                }
            }

            using (Mat warped = WarpPerspective(checkerBgr, quad, options.OutW, options.OutH))
            {
                List<Rect> rects = DetectPatchRects(warped, 0.6);
                Rect?[,] grid = AssignRectsToGridPartial(rects);
                bool[,] manualFlags = new bool[ROWS, COLS];

                int nAuto = CountAssignedPatches(grid);
                if (nAuto < ROWS * COLS)
                {
                    Console.WriteLine(string.Format(
                        "[WARN] Auto detected/assigned patches = {0}/24. Enter manual fill mode...", nAuto));
                    ManualFillMissingPatches(warped, grid, manualFlags, options.OrderMode);
                }

                List<PatchRecord> records = BuildRecordsFromGrid(warped, grid, manualFlags, options.OrderMode);
                int nFinal = records.Count;
                Console.WriteLine(string.Format("[INFO] Final records = {0}/24 (auto+manual).", nFinal));
                if (nFinal < options.MinFitPatches)
                {
                    return 0;
                }

                FitResult fit = FitPoly3RidgeFromRecords(records, trueRgb24, options.RidgeLambda);
                float[,,,] lut = Build3DLutFromPoly(fit.WPoly, options.LutSize);

                SaveModelNpz(
                    saveNpzPath,
                    fit.WPoly,
                    lut,
                    options.RidgeLambda,
                    options.LutSize,
                    fit.RmseRgb255[0],
                    fit.RmseRgb255[1],
                    fit.RmseRgb255[2],
                    fit.TotalRmse255,
                    records.Count);

                return 1;
            }
        }

        public static Rgb24 CorrectPixelFromMat(Mat imgBgr, int x, int y, string calibrationNpzPath)
        {
            if (imgBgr == null || imgBgr.Empty())
                throw new ArgumentException("imgBgr is null");
            if (imgBgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("imgBgr must be CV_8UC3");
            if (x < 0 || x >= imgBgr.Cols || y < 0 || y >= imgBgr.Rows)
                throw new ArgumentOutOfRangeException("Pixel coordinate out of bounds");
            if (!File.Exists(calibrationNpzPath))
                throw new FileNotFoundException("Cannot find the correction file", calibrationNpzPath);

            int lutSize;
            float[] lut = LoadLutFromNpz(calibrationNpzPath, out lutSize);

            Vec3b bgr = imgBgr.At<Vec3b>(y, x);
            float r = bgr.Item2 / 255.0f;
            float g = bgr.Item1 / 255.0f;
            float b = bgr.Item0 / 255.0f;

            RgbFloat corrected = SampleTrilinear(lut, lutSize, r, g, b);

            return new Rgb24(
                ToByteTruncate(corrected.R * 255.0f),
                ToByteTruncate(corrected.G * 255.0f),
                ToByteTruncate(corrected.B * 255.0f));
        }

        public static Mat CorrectImageFromMat(Mat imgBgr, string calibrationNpzPath)
        {
            if (imgBgr == null || imgBgr.Empty())
                throw new ArgumentException("imgBgr is null");
            if (imgBgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("imgBgr must be CV_8UC3");
            if (!File.Exists(calibrationNpzPath))
                throw new FileNotFoundException("Cannot find the correction file", calibrationNpzPath);

            int lutSize;
            float[] lut = LoadLutFromNpz(calibrationNpzPath, out lutSize);

            Mat dst = new Mat(imgBgr.Rows, imgBgr.Cols, MatType.CV_8UC3);

            for (int y = 0; y < imgBgr.Rows; y++)
            {
                for (int x = 0; x < imgBgr.Cols; x++)
                {
                    Vec3b bgr = imgBgr.At<Vec3b>(y, x);
                    float r = bgr.Item2 / 255.0f;
                    float g = bgr.Item1 / 255.0f;
                    float b = bgr.Item0 / 255.0f;
                    RgbFloat corrected = SampleTrilinear(lut, lutSize, r, g, b);
                    byte rr = ToByteTruncate(corrected.R * 255.0f);
                    byte gg = ToByteTruncate(corrected.G * 255.0f);
                    byte bb = ToByteTruncate(corrected.B * 255.0f);
                    dst.Set(y, x, new Vec3b(bb, gg, rr));
                }
            }

            return dst;
        }

        private struct RgbFloat
        {
            public float R;
            public float G;
            public float B;

            public RgbFloat(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        private class PatchRecord
        {
            public int Id { get; set; }
            public int Row { get; set; }
            public int Col { get; set; }
            public double MeasR { get; set; }
            public double MeasG { get; set; }
            public double MeasB { get; set; }
            public Rect Rect { get; set; }
            public bool IsManual { get; set; }

        }

        private class FitResult
        {
            public double[,] WPoly { get; set; }
            public double[] RmseRgb255 { get; set; }
            public double TotalRmse255 { get; set; }
        }

        private class MissingPatchInfo
        {
            public int Pid;
            public int Row;
            public int Col;
        }

        private class ManualPickState
        {
            public Mat WarpedBgr;
            public Rect?[,] GridRects;
            public bool[,] ManualFlags;
            public List<MissingPatchInfo> Missing;
            public Stack<MissingPatchInfo> ManualStack;
            public double AvgW;
            public double AvgH;
            public string OrderMode;
        }

        private static int CountAssignedPatches(Rect?[,] gridRects)
        {
            int n = 0;
            for (int r = 0; r < ROWS; r++)
                for (int c = 0; c < COLS; c++)
                    if (gridRects[r, c].HasValue) n++;
            return n;
        }

        private class ManualQuadState
        {
            public Mat Image;
            public Mat Show;
            public List<Point2f> Points = new List<Point2f>();
            public string WindowName;
        }

        private static void ManualFillMissingPatches(
            Mat warpedBgr,
            Rect?[,] gridRects,
            bool[,] manualFlags,
            string orderMode)
        {
            int H = warpedBgr.Rows;
            int W = warpedBgr.Cols;

            double avgW, avgH;
            if (!ComputeAvgWh(gridRects, out avgW, out avgH))
            {
                avgW = W / (COLS + 1.5);
                avgH = H / (ROWS + 1.5);
            }

            List<MissingPatchInfo> missing = new List<MissingPatchInfo>();
            for (int pid = 1; pid <= ROWS * COLS; pid++)
            {
                int r, c;
                RcFromId(pid, orderMode, out r, out c);
                if (!gridRects[r, c].HasValue)
                {
                    MissingPatchInfo info = new MissingPatchInfo();
                    info.Pid = pid;
                    info.Row = r;
                    info.Col = c;
                    missing.Add(info);
                }
            }

            if (missing.Count == 0)
                return;

            ManualPickState state = new ManualPickState();
            state.WarpedBgr = warpedBgr;
            state.GridRects = gridRects;
            state.ManualFlags = manualFlags;
            state.Missing = missing;
            state.ManualStack = new Stack<MissingPatchInfo>();
            state.AvgW = avgW;
            state.AvgH = avgH;
            state.OrderMode = orderMode;

            string window = "Manual fill missing patches (click center)";
            Cv2.NamedWindow(window, OpenCvSharp.WindowMode.Normal);
            Cv2.SetMouseCallback(window, (ev, x, y, flags, userdata) =>
            {
                if (ev != OpenCvSharp.MouseEvent.LButtonDown)
                    return;

                MissingPatchInfo next = GetNextMissing(state);
                if (next == null)
                    return;

                Rect rect = MakeRectFromCenter(
                    x, y,
                    state.AvgW, state.AvgH,
                    state.WarpedBgr.Cols, state.WarpedBgr.Rows);

                state.GridRects[next.Row, next.Col] = rect;
                state.ManualFlags[next.Row, next.Col] = true;
                state.ManualStack.Push(next);
            });

            while (true)
            {
                using (Mat vis = DrawManualFillView(state))
                {
                    Cv2.ImShow(window, vis);
                }

                int key = Cv2.WaitKey(20) & 0xFF;

                if (key == 27)
                {
                    Cv2.DestroyWindow(window);
                    throw new InvalidOperationException("Manual fill canceled by user (Esc).");
                }

                if (key == 8 || key == 'z' || key == 'Z')
                {
                    if (state.ManualStack.Count > 0)
                    {
                        MissingPatchInfo last = state.ManualStack.Pop();
                        state.GridRects[last.Row, last.Col] = null;
                        state.ManualFlags[last.Row, last.Col] = false;
                    }
                }

                if (key == 13 || key == 10 || key == 'q' || key == 'Q')
                {
                    Cv2.DestroyWindow(window);
                    return;
                }
            }
        }

        private static MissingPatchInfo GetNextMissing(ManualPickState state)
        {
            for (int i = 0; i < state.Missing.Count; i++)
            {
                MissingPatchInfo info = state.Missing[i];
                if (!state.GridRects[info.Row, info.Col].HasValue)
                    return info;
            }
            return null;
        }

        private static Mat DrawManualFillView(ManualPickState state)
        {
            Mat vis = state.WarpedBgr.Clone();

            Scalar RED = new Scalar(0, 0, 255);
            Scalar WHITE = new Scalar(255, 255, 255);
            Scalar GREEN = new Scalar(0, 255, 0);
            Scalar BLACK = new Scalar(0, 0, 0);

            for (int rr = 0; rr < ROWS; rr++)
            {
                for (int cc = 0; cc < COLS; cc++)
                {
                    if (!state.GridRects[rr, cc].HasValue)
                        continue;

                    Rect rect = state.GridRects[rr, cc].Value;
                    Rect center = Center50Box(rect);

                    Cv2.Rectangle(
                        vis,
                        new Point(center.X, center.Y),
                        new Point(center.X + center.Width, center.Y + center.Height),
                        RED, 2, LineTypes.AntiAlias);

                    if (state.ManualFlags[rr, cc])
                    {
                        int cx = center.X + center.Width / 2;
                        int cy = center.Y + center.Height / 2;
                        Cv2.Circle(vis, new Point(cx, cy), 6, RED, -1, LineTypes.AntiAlias);
                    }
                }
            }

            MissingPatchInfo next = GetNextMissing(state);
            string tip1;
            if (next != null)
            {
                int remain = 0;
                for (int i = 0; i < state.Missing.Count; i++)
                {
                    MissingPatchInfo info = state.Missing[i];
                    if (!state.GridRects[info.Row, info.Col].HasValue)
                        remain++;
                }

                tip1 = string.Format(
                    "Missing {0}. Click CENTER for id={1} (row={2}, col={3}).",
                    remain, next.Pid, next.Row, next.Col);
            }
            else
            {
                tip1 = "All missing patches filled. Press Enter/q to finish.";
            }

            string tip2 = "Keys: Z/Backspace=Undo | Enter/q=Finish | Esc=Cancel";

            Cv2.PutText(vis, tip1, new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, BLACK, 4, LineTypes.AntiAlias);
            Cv2.PutText(vis, tip1, new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, GREEN, 2, LineTypes.AntiAlias);

            Cv2.PutText(vis, tip2, new Point(10, 60),
                HersheyFonts.HersheySimplex, 0.7, BLACK, 4, LineTypes.AntiAlias);
            Cv2.PutText(vis, tip2, new Point(10, 60),
                HersheyFonts.HersheySimplex, 0.7, WHITE, 2, LineTypes.AntiAlias);

            return vis;
        }

        private static bool ComputeAvgWh(Rect?[,] gridRects, out double avgW, out double avgH)
        {
            List<double> ws = new List<double>();
            List<double> hs = new List<double>();

            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    if (!gridRects[r, c].HasValue)
                        continue;

                    Rect rect = gridRects[r, c].Value;
                    ws.Add(rect.Width);
                    hs.Add(rect.Height);
                }
            }

            if (ws.Count == 0)
            {
                avgW = 0;
                avgH = 0;
                return false;
            }

            avgW = ws.Average();
            avgH = hs.Average();
            return true;
        }

        private static Rect MakeRectFromCenter(
            double cx, double cy,
            double w, double h,
            int W, int H)
        {
            int x = (int)Math.Round(cx - w / 2.0);
            int y = (int)Math.Round(cy - h / 2.0);

            x = Math.Max(0, Math.Min(W - 2, x));
            y = Math.Max(0, Math.Min(H - 2, y));

            int iw = (int)Math.Round(w);
            int ih = (int)Math.Round(h);

            iw = Math.Max(2, Math.Min(W - x, iw));
            ih = Math.Max(2, Math.Min(H - y, ih));

            return new Rect(x, y, iw, ih);
        }

        private static void RcFromId(int pid, string orderMode, out int r, out int c)
        {
            int pid0 = pid - 1;
            if (string.Equals(orderMode, "col", StringComparison.OrdinalIgnoreCase))
            {
                c = pid0 / ROWS;
                r = pid0 % ROWS;
            }
            else
            {
                r = pid0 / COLS;
                c = pid0 % COLS;
            }
        }

        private static double[,] ReadTrueColorsXlsx(string xlsxPath)
        {
            using (var wb = new XLWorkbook(xlsxPath))
            {
                var ws = wb.Worksheets.First();

                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in ws.Row(1).CellsUsed())
                {
                    string name = cell.GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        headerMap[name] = cell.Address.ColumnNumber;
                }

                string[] needs = new string[] { "id", "R", "G", "B" };
                foreach (string need in needs)
                {
                    if (!headerMap.ContainsKey(need))
                        throw new InvalidDataException("colortrue.xlsx needs to include columns: id, R, G, B");
                }

                var rows = new List<Tuple<int, double, double, double>>();
                int lastRow = ws.LastRowUsed().RowNumber();

                for (int i = 2; i <= lastRow; i++)
                {
                    var row = ws.Row(i);
                    if (row.IsEmpty()) continue;

                    int id = row.Cell(headerMap["id"]).GetValue<int>();
                    double r = row.Cell(headerMap["R"]).GetValue<double>();
                    double g = row.Cell(headerMap["G"]).GetValue<double>();
                    double b = row.Cell(headerMap["B"]).GetValue<double>();
                    rows.Add(Tuple.Create(id, r, g, b));
                }

                rows = rows.OrderBy(t => t.Item1).ToList();

                double[,] result = new double[rows.Count, 3];
                for (int i = 0; i < rows.Count; i++)
                {
                    result[i, 0] = rows[i].Item2;
                    result[i, 1] = rows[i].Item3;
                    result[i, 2] = rows[i].Item4;
                }

                return result;
            }
        }

        private static void RedrawManualPickQuad(ManualQuadState state)
        {
            if (state.Show != null)
                state.Show.Dispose();

            state.Show = state.Image.Clone();

            Scalar RED = new Scalar(0, 0, 255);
            Scalar WHITE = new Scalar(255, 255, 255);
            Scalar GREEN = new Scalar(0, 255, 0);
            Scalar BLACK = new Scalar(0, 0, 0);

            for (int i = 0; i < state.Points.Count; i++)
            {
                int xi = (int)state.Points[i].X;
                int yi = (int)state.Points[i].Y;

                Cv2.Circle(state.Show, new Point(xi, yi), 8, WHITE, 2, LineTypes.AntiAlias);
                Cv2.Circle(state.Show, new Point(xi, yi), 6, RED, -1, LineTypes.AntiAlias);

                string label = (i + 1).ToString();
                Cv2.PutText(state.Show, label, new Point(xi + 10, yi - 10),
                    HersheyFonts.HersheySimplex, 0.9, WHITE, 4, LineTypes.AntiAlias);
                Cv2.PutText(state.Show, label, new Point(xi + 10, yi - 10),
                    HersheyFonts.HersheySimplex, 0.9, RED, 2, LineTypes.AntiAlias);
            }

            if (state.Points.Count >= 2)
            {
                for (int i = 0; i < state.Points.Count - 1; i++)
                {
                    Point p1 = new Point((int)state.Points[i].X, (int)state.Points[i].Y);
                    Point p2 = new Point((int)state.Points[i + 1].X, (int)state.Points[i + 1].Y);
                    Cv2.Line(state.Show, p1, p2, RED, 2, LineTypes.AntiAlias);
                }
            }

            if (state.Points.Count == 4)
            {
                Point[] poly = state.Points
                    .Select(p => new Point((int)p.X, (int)p.Y))
                    .ToArray();

                Cv2.Polylines(state.Show, new[] { poly }, true, RED, 2, LineTypes.AntiAlias);
            }

            string tip = "Click: 1=TL 2=TR 3=BR 4=BL | Enter=OK | Z/Backspace=Undo | Esc=Cancel";

            Cv2.PutText(state.Show, tip, new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, BLACK, 4, LineTypes.AntiAlias);
            Cv2.PutText(state.Show, tip, new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, GREEN, 2, LineTypes.AntiAlias);
        }

        private static Point2f[] ManualPickQuad(Mat imgBgr, string windowName)
        {
            ManualQuadState state = new ManualQuadState();
            state.Image = imgBgr;
            state.Show = imgBgr.Clone();
            state.WindowName = windowName;

            Cv2.NamedWindow(windowName, OpenCvSharp.WindowMode.Normal);
            Cv2.SetMouseCallback(windowName, (ev, x, y, flags, userdata) =>
            {
                if (ev == OpenCvSharp.MouseEvent.LButtonDown && state.Points.Count < 4)
                {
                    state.Points.Add(new Point2f((float)x, (float)y));
                    RedrawManualPickQuad(state);
                }
            });

            RedrawManualPickQuad(state);

            while (true)
            {
                Cv2.ImShow(windowName, state.Show);
                int key = Cv2.WaitKey(20) & 0xFF;

                if (key == 27)
                {
                    Cv2.DestroyWindow(windowName);
                    state.Show.Dispose();
                    throw new InvalidOperationException("Manual picking canceled by user (Esc).");
                }

                if (key == 8 || key == 'z' || key == 'Z')
                {
                    if (state.Points.Count > 0)
                    {
                        state.Points.RemoveAt(state.Points.Count - 1);
                        RedrawManualPickQuad(state);
                    }
                }

                if (key == 13 || key == 10)
                {
                    if (state.Points.Count != 4)
                    {
                        Console.WriteLine(string.Format("[WARN] Need 4 points, now {0}.", state.Points.Count));
                    }
                    else
                    {
                        Point2f[] pts = state.Points.ToArray();
                        Cv2.DestroyWindow(windowName);
                        state.Show.Dispose();
                        return pts;
                    }
                }
            }
        }

        private static Point2f[] DetectColorCheckerQuad(
            Mat imgBgr, double expectedAspect, double aspectTol, double minAreaRatio)
        {
            int h = imgBgr.Rows;
            int w = imgBgr.Cols;

            using (var gray = new Mat())
            using (var blur = new Mat())
            using (var edges = new Mat())
            using (var morphed = new Mat())
            using (var kernel3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)))
            using (var kernel5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5)))
            {
                Cv2.CvtColor(imgBgr, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(gray, blur, new Size(5, 5), 0);
                Cv2.Canny(blur, edges, 50, 150);

                Cv2.Dilate(edges, edges, kernel3, iterations: 1);
                Cv2.MorphologyEx(edges, morphed, MorphTypes.Close, kernel5, iterations: 1);

                Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(
                    morphed,
                    out contours,
                    out hierarchy,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple);

                double imgArea = h * w;
                Point2f[] best = null;
                double bestScore = double.NegativeInfinity;

                foreach (var cnt in contours)
                {
                    double area = Cv2.ContourArea(cnt);
                    if (area < imgArea * minAreaRatio) continue;

                    double peri = Cv2.ArcLength(cnt, true);
                    Point[] approx = Cv2.ApproxPolyDP(cnt, 0.02 * peri, true);
                    if (approx.Length != 4) continue;

                    Point2f[] quad = OrderQuadPoints(approx.Select(p => new Point2f(p.X, p.Y)).ToArray());

                    double widthA = Norm(quad[2], quad[3]);
                    double widthB = Norm(quad[1], quad[0]);
                    double heightA = Norm(quad[1], quad[2]);
                    double heightB = Norm(quad[0], quad[3]);

                    double ww = (widthA + widthB) / 2.0;
                    double hh = (heightA + heightB) / 2.0;
                    if (hh < 1e-6) continue;

                    double aspect = ww / hh;
                    if (Math.Abs(aspect - expectedAspect) > aspectTol) continue;

                    double score = area - (Math.Abs(aspect - expectedAspect) * area * 2.0);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = quad;
                    }
                }

                if (best == null)
                    throw new InvalidOperationException("Automatic color card frame detection failed");

                using (var gray2 = new Mat())
                {
                    Cv2.CvtColor(imgBgr, gray2, ColorConversionCodes.BGR2GRAY);
                    best = RefineCorners(gray2, best);
                }

                return OrderQuadPoints(best);
            }
        }

        private static Point2f[] OrderQuadPoints(Point2f[] pts)
        {
            if (pts == null || pts.Length != 4)
                throw new ArgumentException("The four corner points must be 4 in number.");

            var s = pts.Select(p => p.X + p.Y).ToArray();
            var d = pts.Select(p => p.X - p.Y).ToArray();

            Point2f tl = pts[Array.IndexOf(s, s.Min())];
            Point2f br = pts[Array.IndexOf(s, s.Max())];
            Point2f tr = pts[Array.IndexOf(d, d.Max())];
            Point2f bl = pts[Array.IndexOf(d, d.Min())];

            return new Point2f[] { tl, tr, br, bl };
        }

        private static Point2f[] RefineCorners(Mat gray, Point2f[] corners)
        {
            Point2f[] c = (Point2f[])corners.Clone();
            Cv2.CornerSubPix(
                gray,
                c,
                new Size(7, 7),
                new Size(-1, -1),
                new TermCriteria(CriteriaType.Eps | CriteriaType.MaxIter, 50, 1e-4));
            return c;
        }

        private static double Norm(Point2f a, Point2f b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Mat WarpPerspective(Mat imgBgr, Point2f[] quad, int outW, int outH)
        {
            Point2f[] dst = new Point2f[]
            {
                new Point2f(0, 0),
                new Point2f(outW - 1, 0),
                new Point2f(outW - 1, outH - 1),
                new Point2f(0, outH - 1)
            };

            Mat m = Cv2.GetPerspectiveTransform(quad, dst);
            Mat warped = new Mat();
            Cv2.WarpPerspective(imgBgr, warped, m, new Size(outW, outH), InterpolationFlags.Linear);
            m.Dispose();
            return warped;
        }

        private static List<Rect> DetectPatchRects(Mat warpedBgr, double iouTh)
        {
            int H = warpedBgr.Rows;
            int W = warpedBgr.Cols;
            double total = H * W;

            using (var gray = new Mat())
            using (var blur = new Mat())
            using (var edges = new Mat())
            using (var morphed = new Mat())
            using (var kernel3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)))
            using (var kernel5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5)))
            {
                Cv2.CvtColor(warpedBgr, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(gray, blur, new Size(5, 5), 0);
                Cv2.Canny(blur, edges, 40, 140);

                Cv2.Dilate(edges, edges, kernel3, iterations: 1);
                Cv2.MorphologyEx(edges, morphed, MorphTypes.Close, kernel5, iterations: 2);

                Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(
                    morphed,
                    out contours,
                    out hierarchy,
                    RetrievalModes.Tree,
                    ContourApproximationModes.ApproxSimple);

                double minArea = total * 0.008;
                double maxArea = total * 0.15;

                List<Rect> candRects = new List<Rect>();
                List<double> candScores = new List<double>();

                foreach (var cnt in contours)
                {
                    double area = Cv2.ContourArea(cnt);
                    if (area < minArea || area > maxArea) continue;

                    double peri = Cv2.ArcLength(cnt, true);
                    Point[] approx = Cv2.ApproxPolyDP(cnt, 0.02 * peri, true);

                    if (approx.Length != 4) continue;
                    if (!Cv2.IsContourConvex(approx)) continue;

                    Rect rect = Cv2.BoundingRect(approx);
                    double ar = rect.Width / Math.Max(1.0, rect.Height);

                    if (ar < 0.75 || ar > 1.33) continue;
                    if (rect.Width < W * 0.05 || rect.Height < H * 0.05) continue;

                    candRects.Add(rect);
                    candScores.Add(area);
                }

                if (candRects.Count == 0)
                    return new List<Rect>();

                List<int> keep = NmsRects(candRects, candScores, iouTh);
                List<Rect> rects = keep.Select(i => candRects[i]).ToList();

                if (rects.Count > 24)
                {
                    double[] areas = rects.Select(r => (double)r.Width * r.Height).ToArray();
                    double med = Median(areas);

                    List<Rect> sel = new List<Rect>();
                    for (int i = 0; i < rects.Count; i++)
                    {
                        double a = areas[i];
                        if (a >= 0.5 * med && a <= 1.8 * med)
                            sel.Add(rects[i]);
                    }

                    if (sel.Count >= 12)
                        rects = sel;

                    rects = rects
                        .OrderByDescending(r => r.Width * r.Height)
                        .Take(24)
                        .ToList();
                }

                return rects;
            }
        }

        private static Rect?[,] AssignRectsToGridPartial(List<Rect> rects)
        {
            Rect?[,] grid = new Rect?[ROWS, COLS];
            if (rects.Count == 0) return grid;

            double[] cx = rects.Select(r => r.X + r.Width / 2.0).ToArray();
            double[] cy = rects.Select(r => r.Y + r.Height / 2.0).ToArray();

            double[] colCenters = cx.Length >= COLS ? KMeans1D(cx, COLS) : cx.OrderBy(v => v).ToArray();
            double[] rowCenters = cy.Length >= ROWS ? KMeans1D(cy, ROWS) : cy.OrderBy(v => v).ToArray();

            Array.Sort(colCenters);
            Array.Sort(rowCenters);

            List<GridCandidate> items = new List<GridCandidate>();
            for (int i = 0; i < rects.Count; i++)
            {
                double x = cx[i];
                double y = cy[i];
                int c = ArgMin(colCenters.Select(v => Math.Abs(v - x)).ToArray());
                int r = ArgMin(rowCenters.Select(v => Math.Abs(v - y)).ToArray());
                double dist = Math.Abs(colCenters[c] - x) + Math.Abs(rowCenters[r] - y);
                items.Add(new GridCandidate { Dist = dist, Index = i, Row = r, Col = c });
            }

            HashSet<string> used = new HashSet<string>();
            foreach (var item in items.OrderBy(t => t.Dist))
            {
                string key = item.Row + "_" + item.Col;
                if (!used.Contains(key) && grid[item.Row, item.Col] == null)
                {
                    grid[item.Row, item.Col] = rects[item.Index];
                    used.Add(key);
                }
            }

            return grid;
        }

        private class GridCandidate
        {
            public double Dist { get; set; }
            public int Index { get; set; }
            public int Row { get; set; }
            public int Col { get; set; }
        }

        private static List<PatchRecord> BuildRecordsFromGrid(
            Mat warpedBgr,
            Rect?[,] gridRects,
            bool[,] manualFlags,
            string orderMode)
        {
            List<PatchRecord> records = new List<PatchRecord>();

            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    if (!gridRects[r, c].HasValue)
                        continue;

                    Rect rect = gridRects[r, c].Value;
                    double[] rgb = SamplePatchRgbCenter50(warpedBgr, rect);
                    int pid = IdFromRc(r, c, orderMode);

                    PatchRecord rec = new PatchRecord();
                    rec.Id = pid;
                    rec.Row = r;
                    rec.Col = c;
                    rec.MeasR = rgb[0];
                    rec.MeasG = rgb[1];
                    rec.MeasB = rgb[2];
                    rec.Rect = rect;
                    rec.IsManual = manualFlags[r, c];
                    records.Add(rec);
                }
            }

            return records.OrderBy(t => t.Id).ToList();
        }


        private static int IdFromRc(int r, int c, string orderMode)
        {
            return string.Equals(orderMode, "col", StringComparison.OrdinalIgnoreCase)
                ? c * ROWS + r + 1
                : r * COLS + c + 1;
        }

        private static double[] SamplePatchRgbCenter50(Mat warpedBgr, Rect rect)
        {
            Rect center = Center50Box(rect);
            center = IntersectRect(center, new Rect(0, 0, warpedBgr.Cols, warpedBgr.Rows));

            using (Mat roi = new Mat(warpedBgr, center))
            {
                if (roi.Empty())
                    return new double[] { double.NaN, double.NaN, double.NaN };

                List<double> rVals = new List<double>();
                List<double> gVals = new List<double>();
                List<double> bVals = new List<double>();

                for (int y = 0; y < roi.Rows; y++)
                {
                    for (int x = 0; x < roi.Cols; x++)
                    {
                        Vec3b bgr = roi.At<Vec3b>(y, x);
                        bVals.Add(bgr.Item0);
                        gVals.Add(bgr.Item1);
                        rVals.Add(bgr.Item2);
                    }
                }

                return new double[]
                {
                    Median(rVals.ToArray()),
                    Median(gVals.ToArray()),
                    Median(bVals.ToArray())
                };
            }
        }

        private static Rect Center50Box(Rect rect)
        {
            int x0 = (int)(rect.X + 0.25 * rect.Width);
            int x1 = (int)(rect.X + 0.75 * rect.Width);
            int y0 = (int)(rect.Y + 0.25 * rect.Height);
            int y1 = (int)(rect.Y + 0.75 * rect.Height);

            return new Rect(x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
        }

        private static Rect IntersectRect(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 <= x1 || y2 <= y1)
                return new Rect(0, 0, 0, 0);

            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        private static List<int> NmsRects(List<Rect> rects, List<double> scores, double iouTh)
        {
            List<int> idx = Enumerable.Range(0, rects.Count)
                .OrderByDescending(i => scores[i])
                .ToList();

            List<int> keep = new List<int>();
            while (idx.Count > 0)
            {
                int i = idx[0];
                idx.RemoveAt(0);
                keep.Add(i);

                idx = idx.Where(j => RectIou(rects[i], rects[j]) < iouTh).ToList();
            }
            return keep;
        }

        private static double RectIou(Rect a, Rect b)
        {
            int ix1 = Math.Max(a.X, b.X);
            int iy1 = Math.Max(a.Y, b.Y);
            int ix2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int iy2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int iw = Math.Max(0, ix2 - ix1);
            int ih = Math.Max(0, iy2 - iy1);
            double inter = iw * ih;
            if (inter <= 0) return 0.0;

            double union = a.Width * a.Height + b.Width * b.Height - inter;
            return inter / union;
        }

        private static double[] KMeans1D(double[] x, int k, int iters = 50)
        {
            if (x.Length < k)
                throw new ArgumentException("KMeans1D: The number of data points is less than k.");

            double[] initP = Enumerable.Range(0, k)
                .Select(i => i * 100.0 / k + 50.0 / k)
                .ToArray();

            double[] centers = initP.Select(p => Percentile(x, p)).ToArray();

            for (int t = 0; t < iters; t++)
            {
                int[] lab = new int[x.Length];

                for (int i = 0; i < x.Length; i++)
                {
                    double best = Math.Abs(x[i] - centers[0]);
                    int bestIdx = 0;
                    for (int j = 1; j < k; j++)
                    {
                        double d = Math.Abs(x[i] - centers[j]);
                        if (d < best)
                        {
                            best = d;
                            bestIdx = j;
                        }
                    }
                    lab[i] = bestIdx;
                }

                double[] newCenters = (double[])centers.Clone();
                for (int j = 0; j < k; j++)
                {
                    double[] sel = x.Where((v, idx) => lab[idx] == j).ToArray();
                    if (sel.Length > 0)
                        newCenters[j] = sel.Average();
                }

                bool same = true;
                for (int j = 0; j < k; j++)
                {
                    if (Math.Abs(newCenters[j] - centers[j]) > 1e-9)
                    {
                        same = false;
                        break;
                    }
                }

                centers = newCenters;
                if (same) break;
            }

            return centers;
        }

        private static int ArgMin(double[] arr)
        {
            int idx = 0;
            double best = arr[0];
            for (int i = 1; i < arr.Length; i++)
            {
                if (arr[i] < best)
                {
                    best = arr[i];
                    idx = i;
                }
            }
            return idx;
        }

        // =========================================================
        //  LUT
        // =========================================================
        private static FitResult FitPoly3RidgeFromRecords(
            List<PatchRecord> records,
            double[,] trueRgb24,
            double ridgeLambda)
        {
            int n = records.Count;
            double[,] meas255 = new double[n, 3];
            double[,] true255 = new double[n, 3];

            for (int i = 0; i < n; i++)
            {
                meas255[i, 0] = records[i].MeasR;
                meas255[i, 1] = records[i].MeasG;
                meas255[i, 2] = records[i].MeasB;

                int idx = records[i].Id - 1;
                true255[i, 0] = trueRgb24[idx, 0];
                true255[i, 1] = trueRgb24[idx, 1];
                true255[i, 2] = trueRgb24[idx, 2];
            }

            double[,] meas01 = DivideAndClip255(meas255);
            double[,] true01 = DivideAndClip255(true255);

            double[,] xFeat = Poly3Features(meas01);

            var M = Matrix<double>.Build;
            Matrix<double> X = M.DenseOfArray(xFeat);
            Matrix<double> Y = M.DenseOfArray(true01);

            Matrix<double> XtX = X.TransposeThisAndMultiply(X);
            Matrix<double> I = M.DenseIdentity(X.ColumnCount);
            I[0, 0] = 0.0;

            Matrix<double> A = XtX + ridgeLambda * I;
            Matrix<double> W = A.Solve(X.TransposeThisAndMultiply(Y));

            Matrix<double> YHat = X * W;
            Matrix<double> Err = YHat - Y;

            double[] rmse01 = new double[3];
            for (int c = 0; c < 3; c++)
            {
                double sum = 0.0;
                for (int i = 0; i < n; i++)
                    sum += Err[i, c] * Err[i, c];
                rmse01[c] = Math.Sqrt(sum / n);
            }

            double[] rmse255 = rmse01.Select(v => v * 255.0).ToArray();

            double sumAll = 0.0;
            for (int i = 0; i < Err.RowCount; i++)
            {
                for (int j = 0; j < Err.ColumnCount; j++)
                {
                    double e = Err[i, j] * 255.0;
                    sumAll += e * e;
                }
            }

            FitResult ret = new FitResult();
            ret.WPoly = W.ToArray();
            ret.RmseRgb255 = rmse255;
            ret.TotalRmse255 = Math.Sqrt(sumAll / (Err.RowCount * Err.ColumnCount));
            return ret;
        }

        private static double[,] DivideAndClip255(double[,] arr)
        {
            int r = arr.GetLength(0);
            int c = arr.GetLength(1);
            double[,] outArr = new double[r, c];

            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    double v = arr[i, j] / 255.0;
                    outArr[i, j] = Math.Min(1.0, Math.Max(0.0, v));
                }

            return outArr;
        }

        private static double[,] Poly3Features(double[,] rgb01)
        {
            int n = rgb01.GetLength(0);
            double[,] X = new double[n, 20];

            for (int i = 0; i < n; i++)
            {
                double R = rgb01[i, 0];
                double G = rgb01[i, 1];
                double B = rgb01[i, 2];

                double R2 = R * R, G2 = G * G, B2 = B * B;
                double RG = R * G, RB = R * B, GB = G * B;
                double R3 = R2 * R, G3 = G2 * G, B3 = B2 * B;
                double R2G = R2 * G, R2B = R2 * B, G2R = G2 * R, G2B = G2 * B, B2R = B2 * R, B2G = B2 * G;
                double RGB = R * G * B;

                X[i, 0] = 1.0;
                X[i, 1] = R;
                X[i, 2] = G;
                X[i, 3] = B;
                X[i, 4] = R2;
                X[i, 5] = G2;
                X[i, 6] = B2;
                X[i, 7] = RG;
                X[i, 8] = RB;
                X[i, 9] = GB;
                X[i, 10] = R3;
                X[i, 11] = G3;
                X[i, 12] = B3;
                X[i, 13] = R2G;
                X[i, 14] = R2B;
                X[i, 15] = G2R;
                X[i, 16] = G2B;
                X[i, 17] = B2R;
                X[i, 18] = B2G;
                X[i, 19] = RGB;
            }

            return X;
        }

        private static float[,,,] Build3DLutFromPoly(double[,] wPoly, int lutSize)
        {
            int S = lutSize;
            float[,,,] lut = new float[S, S, S, 3];
            double[] grid = Enumerable.Range(0, S)
                .Select(i => S == 1 ? 0.0 : i / (double)(S - 1))
                .ToArray();

            for (int i = 0; i < S; i++)
            {
                for (int j = 0; j < S; j++)
                {
                    for (int k = 0; k < S; k++)
                    {
                        double[,] one = new double[1, 3];
                        one[0, 0] = grid[i];
                        one[0, 1] = grid[j];
                        one[0, 2] = grid[k];

                        double[,] feat = Poly3Features(one);

                        for (int ch = 0; ch < 3; ch++)
                        {
                            double sum = 0.0;
                            for (int f = 0; f < 20; f++)
                                sum += feat[0, f] * wPoly[f, ch];

                            sum = Math.Min(1.0, Math.Max(0.0, sum));
                            lut[i, j, k, ch] = (float)sum;
                        }
                    }
                }
            }

            return lut;
        }

        // =========================================================
        //  npz
        // =========================================================
        private static void SaveModelNpz(
            string path,
            double[,] wPoly,
            float[,,,] lut,
            double ridgeLambda,
            int lutSize,
            double rmseR,
            double rmseG,
            double rmseB,
            double rmseTotal,
            int nRecords)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            using (var fs = File.Create(path))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteNpyUnicodeScalar(zip, "model_type.npy", "poly3_ridge_lut");
                WriteNpyFloat64_2D(zip, "W_poly.npy", wPoly);
                WriteNpyFloat32_4D(zip, "lut.npy", lut);
                WriteNpyFloat64Scalar(zip, "ridge_lambda.npy", ridgeLambda);
                WriteNpyInt32Scalar(zip, "lut_size.npy", lutSize);
                WriteNpyFloat64Scalar(zip, "rmse_R.npy", rmseR);
                WriteNpyFloat64Scalar(zip, "rmse_G.npy", rmseG);
                WriteNpyFloat64Scalar(zip, "rmse_B.npy", rmseB);
                WriteNpyFloat64Scalar(zip, "rmse_total.npy", rmseTotal);
                WriteNpyInt32Scalar(zip, "n_records.npy", nRecords);
            }
        }

        private static float[] LoadLutFromNpz(string npzPath, out int lutSize)
        {
            using (var fs = File.OpenRead(npzPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var modelEntry = zip.Entries.FirstOrDefault(e => e.Name == "model_type.npy");
                if (modelEntry != null)
                {
                    using (var s = modelEntry.Open())
                    {
                        string modelType = NpyReader.ReadUnicodeScalar(s);
                        if (!string.Equals(modelType, "poly3_ridge_lut", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Unsupported model_type: " + modelType);
                    }
                }

                var lutEntry = zip.Entries.FirstOrDefault(e => e.Name == "lut.npy");
                if (lutEntry == null)
                    throw new InvalidDataException("The file \"lut.npy\" was not found.");

                using (var ls = lutEntry.Open())
                {
                    NpyArrayFloat32 lutArray = NpyReader.ReadFloat32Array(ls);

                    if (lutArray.Shape.Length != 4 || lutArray.Shape[3] != 3)
                        throw new InvalidDataException("The shape of lut.npy must be [S,S,S,3]");
                    if (lutArray.Shape[0] != lutArray.Shape[1] || lutArray.Shape[1] != lutArray.Shape[2])
                        throw new InvalidDataException("The first three dimensions of lut.npy must be equal.");

                    lutSize = lutArray.Shape[0];
                    return lutArray.Data;
                }
            }
        }

        private static void WriteNpyFloat64Scalar(ZipArchive zip, string name, double value)
        {
            using (var ms = new MemoryStream())
            {
                WriteNpyHeader(ms, "<f8", new int[0]);
                ms.Write(BitConverter.GetBytes(value), 0, 8);
                ms.Position = 0;

                var entry = zip.CreateEntry(name);
                using (var es = entry.Open())
                    ms.CopyTo(es);
            }
        }

        private static void WriteNpyInt32Scalar(ZipArchive zip, string name, int value)
        {
            using (var ms = new MemoryStream())
            {
                WriteNpyHeader(ms, "<i4", new int[0]);
                ms.Write(BitConverter.GetBytes(value), 0, 4);
                ms.Position = 0;

                var entry = zip.CreateEntry(name);
                using (var es = entry.Open())
                    ms.CopyTo(es);
            }
        }

        private static void WriteNpyUnicodeScalar(ZipArchive zip, string name, string value)
        {
            int len = Math.Max(1, value.Length);

            using (var ms = new MemoryStream())
            {
                WriteNpyHeader(ms, "<U" + len, new int[0]);

                for (int i = 0; i < len; i++)
                {
                    int code = i < value.Length ? value[i] : 0;
                    byte[] bytes = BitConverter.GetBytes(code);
                    ms.Write(bytes, 0, bytes.Length);
                }

                ms.Position = 0;
                var entry = zip.CreateEntry(name);
                using (var es = entry.Open())
                    ms.CopyTo(es);
            }
        }

        private static void WriteNpyFloat64_2D(ZipArchive zip, string name, double[,] arr)
        {
            int d0 = arr.GetLength(0);
            int d1 = arr.GetLength(1);

            using (var ms = new MemoryStream())
            {
                WriteNpyHeader(ms, "<f8", new int[] { d0, d1 });

                for (int i = 0; i < d0; i++)
                    for (int j = 0; j < d1; j++)
                    {
                        byte[] bytes = BitConverter.GetBytes(arr[i, j]);
                        ms.Write(bytes, 0, bytes.Length);
                    }

                ms.Position = 0;
                var entry = zip.CreateEntry(name);
                using (var es = entry.Open())
                    ms.CopyTo(es);
            }
        }

        private static void WriteNpyFloat32_4D(ZipArchive zip, string name, float[,,,] arr)
        {
            int d0 = arr.GetLength(0);
            int d1 = arr.GetLength(1);
            int d2 = arr.GetLength(2);
            int d3 = arr.GetLength(3);

            using (var ms = new MemoryStream())
            {
                WriteNpyHeader(ms, "<f4", new int[] { d0, d1, d2, d3 });

                for (int i = 0; i < d0; i++)
                    for (int j = 0; j < d1; j++)
                        for (int k = 0; k < d2; k++)
                            for (int c = 0; c < d3; c++)
                            {
                                byte[] bytes = BitConverter.GetBytes(arr[i, j, k, c]);
                                ms.Write(bytes, 0, bytes.Length);
                            }

                ms.Position = 0;
                var entry = zip.CreateEntry(name);
                using (var es = entry.Open())
                    ms.CopyTo(es);
            }
        }

        private static void WriteNpyHeader(Stream stream, string descr, int[] shape)
        {
            byte[] magic = new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' };
            stream.Write(magic, 0, magic.Length);
            stream.WriteByte(1);
            stream.WriteByte(0);

            string shapeStr;
            if (shape.Length == 0)
                shapeStr = "()";
            else if (shape.Length == 1)
                shapeStr = "(" + shape[0] + ",)";
            else
                shapeStr = "(" + string.Join(", ", shape) + ")";

            string dict = "{'descr': '" + descr + "', 'fortran_order': False, 'shape': " + shapeStr + ", }";
            string header = dict;

            int preamble = 10;
            int total = preamble + Encoding.ASCII.GetByteCount(header) + 1;
            int pad = 16 - (total % 16);
            if (pad == 16) pad = 0;
            header += new string(' ', pad) + "\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            ushort headerLen = (ushort)headerBytes.Length;
            byte[] lenBytes = BitConverter.GetBytes(headerLen);

            stream.Write(lenBytes, 0, lenBytes.Length);
            stream.Write(headerBytes, 0, headerBytes.Length);
        }

        private static RgbFloat SampleTrilinear(float[] lut, int S, float r01, float g01, float b01)
        {
            float x = Clamp(r01 * (S - 1), 0f, S - 1);
            float y = Clamp(g01 * (S - 1), 0f, S - 1);
            float z = Clamp(b01 * (S - 1), 0f, S - 1);

            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int z0 = (int)Math.Floor(z);

            int x1 = Math.Min(x0 + 1, S - 1);
            int y1 = Math.Min(y0 + 1, S - 1);
            int z1 = Math.Min(z0 + 1, S - 1);

            float xd = x - x0;
            float yd = y - y0;
            float zd = z - z0;

            RgbFloat c000 = GetLut(lut, S, x0, y0, z0);
            RgbFloat c100 = GetLut(lut, S, x1, y0, z0);
            RgbFloat c010 = GetLut(lut, S, x0, y1, z0);
            RgbFloat c110 = GetLut(lut, S, x1, y1, z0);
            RgbFloat c001 = GetLut(lut, S, x0, y0, z1);
            RgbFloat c101 = GetLut(lut, S, x1, y0, z1);
            RgbFloat c011 = GetLut(lut, S, x0, y1, z1);
            RgbFloat c111 = GetLut(lut, S, x1, y1, z1);

            RgbFloat c00 = Lerp(c000, c100, xd);
            RgbFloat c10 = Lerp(c010, c110, xd);
            RgbFloat c01 = Lerp(c001, c101, xd);
            RgbFloat c11 = Lerp(c011, c111, xd);

            RgbFloat c0 = Lerp(c00, c10, yd);
            RgbFloat c1 = Lerp(c01, c11, yd);

            return Lerp(c0, c1, zd);
        }

        private static RgbFloat GetLut(float[] lut, int S, int i, int j, int k)
        {
            int idx = (((i * S) + j) * S + k) * 3;
            return new RgbFloat(lut[idx], lut[idx + 1], lut[idx + 2]);
        }

        private static RgbFloat Lerp(RgbFloat a, RgbFloat b, float t)
        {
            return new RgbFloat(
                a.R * (1 - t) + b.R * t,
                a.G * (1 - t) + b.G * t,
                a.B * (1 - t) + b.B * t);
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static byte ToByteTruncate(float v)
        {
            if (v <= 0) return 0;
            if (v >= 255) return 255;
            return (byte)v;
        }

        private static double Percentile(double[] data, double p)
        {
            double[] s = data.OrderBy(v => v).ToArray();
            if (s.Length == 0) throw new ArgumentException("Empty array");
            if (s.Length == 1) return s[0];

            double rank = (p / 100.0) * (s.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi) return s[lo];

            double t = rank - lo;
            return s[lo] * (1 - t) + s[hi] * t;
        }

        private static double Median(double[] data)
        {
            return Percentile(data, 50.0);
        }

        private class NpyArrayFloat32
        {
            public int[] Shape { get; set; }
            public float[] Data { get; set; }
        }

        private static class NpyReader
        {
            public static string ReadUnicodeScalar(Stream stream)
            {
                using (var br = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    string descr;
                    bool fortran;
                    int[] shape;
                    ReadMagicAndHeader(br, out descr, out fortran, out shape);

                    if (fortran)
                        throw new NotSupportedException("Not supporting Fortran-order");
                    if (!descr.StartsWith("<U") && !descr.StartsWith("|U"))
                        throw new InvalidDataException("Not a Unicode string scalar: " + descr);
                    if (shape.Length != 0)
                        throw new InvalidDataException("Not a scalar string");

                    Match m = Regex.Match(descr, @"[<|]U(\d+)");
                    int charCount = int.Parse(m.Groups[1].Value);

                    StringBuilder sb = new StringBuilder(charCount);
                    for (int i = 0; i < charCount; i++)
                    {
                        int code = br.ReadInt32();
                        if (code != 0)
                            sb.Append(char.ConvertFromUtf32(code));
                    }

                    return sb.ToString();
                }
            }

            public static NpyArrayFloat32 ReadFloat32Array(Stream stream)
            {
                using (var br = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    string descr;
                    bool fortran;
                    int[] shape;
                    ReadMagicAndHeader(br, out descr, out fortran, out shape);

                    if (fortran)
                        throw new NotSupportedException("Not supporting Fortran-order");
                    if (descr != "<f4" && descr != "|f4")
                        throw new InvalidDataException("Currently only float32 is supported. The actual descr = " + descr);

                    long count = 1;
                    foreach (int dim in shape) count *= dim;

                    if (count <= 0 || count > int.MaxValue)
                        throw new InvalidDataException("Abnormal number of array elements");

                    float[] data = new float[count];
                    byte[] raw = br.ReadBytes((int)count * sizeof(float));
                    if (raw.Length != count * sizeof(float))
                        throw new EndOfStreamException("The length of the npy data is insufficient.");

                    Buffer.BlockCopy(raw, 0, data, 0, raw.Length);

                    NpyArrayFloat32 arr = new NpyArrayFloat32();
                    arr.Shape = shape;
                    arr.Data = data;
                    return arr;
                }
            }

            private static void ReadMagicAndHeader(BinaryReader br, out string descr, out bool fortran, out int[] shape)
            {
                byte[] magic = br.ReadBytes(6);
                byte[] expected = new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' };
                if (!magic.SequenceEqual(expected))
                    throw new InvalidDataException("Not a valid .npy file");

                byte major = br.ReadByte();
                byte minor = br.ReadByte();

                int headerLen;
                if (major == 1)
                    headerLen = br.ReadUInt16();
                else if (major == 2 || major == 3)
                    headerLen = (int)br.ReadUInt32();
                else
                    throw new NotSupportedException("Unsupported npy version: " + major + "." + minor);

                string header = Encoding.ASCII.GetString(br.ReadBytes(headerLen));

                descr = Regex.Match(header, @"'descr'\s*:\s*'([^']+)'").Groups[1].Value;
                string fortranStr = Regex.Match(header, @"'fortran_order'\s*:\s*(True|False)").Groups[1].Value;
                string shapeRaw = Regex.Match(header, @"'shape'\s*:\s*\(([^)]*)\)").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(descr) || string.IsNullOrWhiteSpace(fortranStr))
                    throw new InvalidDataException("Unable to parse the npy header");

                fortran = string.Equals(fortranStr, "True", StringComparison.Ordinal);

                shape = shapeRaw
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(int.Parse)
                    .ToArray();
            }
        }
    }
}