using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenCvSharp;
using Newtonsoft.Json;

public class PredictResult
{
    public string sample_id { get; set; }
    public string PLY { get; set; }
    public string RGB { get; set; }
    public double CH { get; set; }
    public double LAI { get; set; }
}

public static class WheatPredictor
{
    public static PredictResult PredictCHAndLAI(
        string pythonExe,
        string scriptPath,
        string plyPath,
        Mat rgbImage,
        string chModelPath,
        string laiModelPath,
        string workDir = null)
    {
        if (string.IsNullOrWhiteSpace(workDir))
            workDir = Path.Combine(Path.GetTempPath(), "WheatPredictTemp");

        Directory.CreateDirectory(workDir);
        string tempImagePath = Path.Combine(workDir, Guid.NewGuid().ToString("N") + ".png");
        Cv2.ImWrite(tempImagePath, rgbImage);
        string tempJsonPath = Path.Combine(workDir, Guid.NewGuid().ToString("N") + ".json");
        string arguments =
            $"\"{scriptPath}\" " +
            $"--ply_path \"{plyPath}\" " +
            $"--rgb_path \"{tempImagePath}\" " +
            $"--ch_model \"{chModelPath}\" " +
            $"--lai_model \"{laiModelPath}\" " +
            $"--save_json \"{tempJsonPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        string stdout, stderr;
        int exitCode;

        using (var process = new Process())
        {
            process.StartInfo = psi;
            process.Start();

            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();

            process.WaitForExit();
            exitCode = process.ExitCode;
        }

        if (exitCode != 0)
        {
            throw new Exception(
                "Estimation failed.\n" +
                $"ExitCode: {exitCode}\n" +
                $"STDOUT:\n{stdout}\n" +
                $"STDERR:\n{stderr}");
        }

        if (!File.Exists(tempJsonPath))
        {
            throw new Exception(
                "The script execution is complete, but no result JSON was generated.\n" +
                $"STDOUT:\n{stdout}\n" +
                $"STDERR:\n{stderr}");
        }

        string json = File.ReadAllText(tempJsonPath, Encoding.UTF8);
        var result = JsonConvert.DeserializeObject<PredictResult>(json);
        TryDelete(tempImagePath);
        TryDelete(tempJsonPath);

        return result;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}