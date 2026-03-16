using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class LrfColorQuery
{
    public double[,] LoadMatrixTxt(string path, int rows, int cols)
    {
        var lines = File.ReadAllLines(path);
        var M = new double[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            var parts = lines[r].Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            for (int c = 0; c < cols; c++)
                M[r, c] = double.Parse(parts[c]);
        }
        return M;
    }

    public void ReadCalibIni(string iniPath, out double[,] K, out double[] dist, out string model)
    {
        if (!File.Exists(iniPath))
            throw new FileNotFoundException("The internal reference ini file does not exist!", iniPath);

        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(iniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#") || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            if (key.Length == 0) continue;
            kv[key.ToLowerInvariant()] = val;
        }

        double Fx = ReadDouble(kv, "fx", true);
        double Fy = ReadDouble(kv, "fy", true);
        double Cx = ReadDouble(kv, "cx", true);
        double Cy = ReadDouble(kv, "cy", true);

        model = kv.TryGetValue("model", out var m) ? m.Trim().ToLowerInvariant() : "plumb_bob";
        if (model != "plumb_bob" && model != "rational" && model != "fisheye")
            model = "plumb_bob";

        double k1 = ReadDouble(kv, "k1", false, 0);
        double k2 = ReadDouble(kv, "k2", false, 0);
        double p1 = ReadDouble(kv, "p1", false, 0);
        double p2 = ReadDouble(kv, "p2", false, 0);
        double k3 = ReadDouble(kv, "k3", false, 0);
        double k4 = ReadDouble(kv, "k4", false, 0);
        double k5 = ReadDouble(kv, "k5", false, 0);
        double k6 = ReadDouble(kv, "k6", false, 0);

        K = new double[3, 3] {
            { Fx, 0.0, Cx },
            { 0.0, Fy, Cy },
            { 0.0, 0.0, 1.0 }
        };

        if (model == "fisheye")
        {
            // OpenCV fisheye: [k1,k2,k3,k4]
            dist = new double[] { k1, k2, k3, k4 };
        }
        else if (model == "rational")
        {
            // OpenCV rational: [k1,k2,p1,p2,k3,k4,k5,k6]
            dist = new double[] { k1, k2, p1, p2, k3, k4, k5, k6 };
        }
        else
        {
            // plumb_bob: [k1,k2,p1,p2,k3]
            dist = new double[] { k1, k2, p1, p2, k3 };
        }
    }

    private double ReadDouble(Dictionary<string, string> kv, string key, bool required, double defVal = 0.0)
    {
        if (!kv.TryGetValue(key, out var s))
        {
            if (required) throw new InvalidOperationException($"This is lacking the necessary key：{key}");
            return defVal;
        }
        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double v))
            return v;
        if (double.TryParse(s, out v)) return v;
        if (required) throw new FormatException($"Numerical analysis failed：{key}={s}");
        return defVal;
    }
}
