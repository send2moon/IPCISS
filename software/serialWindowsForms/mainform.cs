//using ChLaiInference;
using ColorCalibrationDemo;
using Kitware.VTK;
using OpenCvSharp;
using serialWindowsForms.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

/// <summary>
/// ActiViz.NET (the .NET wrapper for VTK)
/// </summary>
namespace serialWindowsForms
{
    public partial class mainform : Form
    {
        delegate void UpdateTextEventHandler(string text);
        delegate void UpdateIntEventHandler(float size);
        delegate void UpdatePictureBoxShowEventHandler(PictureBox picbox, Mat img);
        delegate void UpdateRGBvaluesEventHandler(double[] val);

        private bool isrunning = false;
        private bool isreset = false;
        private DateTime startT;
        double distance_port2 = 0;
        double distance_port3 = 0;
        int round_z = -1;
        double theta_x = 0;
        double L = 9.7;
        double D_2_3 = 3;
        double v_run_z = 8.22;
        double move_x = 0.18;
        double max_x = 45;
        double max_z = 40;
        int t_space = 100;
        int max_d = 300;
        VideoCapture nirCamera = null;
        VideoCapture rgbCamera = null;
        bool cameraOn = false;
        List<ColorPoint> pointcloud;
        List<ColorPoint> pointcloud_d;
        List<ColorPoint> pointcloud1;
        List<ColorPoint> pointcloud1_d;
        bool upordown = true;
        Vec3b pointcolor = new Vec3b(255, 255, 255);
        Vec3b pointcolor2 = new Vec3b(255, 255, 255);
        bool readrgb = true;
        LrfColorQuery getcolor = new LrfColorQuery();
        double[,] M;
        double[,] K;
        double[] dist;
        string model;
        double x_now = 0;
        double y_now = 0;
        double z_now = 0;
        double x_now2 = 0;
        double y_now2 = 0;
        double z_now2 = 0;
        Mat referenceImage = Cv2.ImRead(@".\aligns\NIR1.jpg", ImreadModes.Grayscale);
        string root = Settings.Default.root;
        string rootpath = Settings.Default.rootpath;
        int colorcheck = 0;
        string img_RGB = @".\aligns\tempRGB.jpg";
        string pythonExe = @".\Anaconda3\envs\py310\python.exe";
        string scriptPath = @".\py\predict_CH_LAI_single.py";
        string chModel = @".\model\CH.joblib";
        string laiModel = @".\model\LAI.joblib";

        public mainform()
        {
            try
            {
                InitializeComponent();

                Updata_Serialport_Name(comboBox1);
                Updata_Serialport_Name(comboBox_ck2);
                Updata_Serialport_Name(comboBox_ck3);

                max_x = Settings.Default.max_x;
                textBox_xmax.Text = max_x.ToString();
                max_z = Settings.Default.max_z;
                textBox_zmax.Text = max_z.ToString();
                t_space = Settings.Default.t_space;
                textBox_space.Text = t_space.ToString();
                max_d = Settings.Default.max_d;
                textBox_filterd.Text = max_d.ToString();
                pointcloud = new List<ColorPoint>();
                pointcloud_d = new List<ColorPoint>();
                pointcloud1 = new List<ColorPoint>();
                pointcloud1_d = new List<ColorPoint>();

                M = getcolor.LoadMatrixTxt(@".\aligns\M_lrf_to_cam_nodistort_fixed.txt", 4, 4);
                getcolor.ReadCalibIni(@".\aligns\calibration_inner_dist.ini", out K, out dist, out model);

                int num = GetUSBDevices();
                if (num < 2)
                {
                    textBox1.AppendText("Camera missing!\r\n");
                    return;
                }

                rgbCamera = new VideoCapture(1);
                bool openrgb = rgbCamera.IsOpened();
                if (rgbCamera == null || !openrgb)
                {
                    textBox1.AppendText("The camera cannot be opened!\r\n");
                    return;
                }
                else
                {
                    textBox1.AppendText("The camera has been turned on.\r\n");
                    cameraOn = true;
                    double width = rgbCamera.Get(3);
                    double height = rgbCamera.Get(4);
                    textBox1.AppendText("Resolution：" + width + "," + height);
                }
            }
            catch (Exception ex)
            {
                string str = "catch ERROR - Form1！" + ex;
                textBox1.AppendText(str);
            }
        }

        private static int GetUSBDevices()
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Camera')");
            int num = searcher.Get().Count;
            foreach (var device in searcher.Get())
            {
                Console.WriteLine($"Device: {device["PNPClass"]} / {device["Caption"]}");
            }
            return num;
        }

        private void Updata_Serialport_Name(ComboBox MycomboBox)
        {
            string[] ArryPort = System.IO.Ports.SerialPort.GetPortNames();
            MycomboBox.Items.Clear();
            for (int i = 0; i < ArryPort.Length; i++)
            {
                MycomboBox.Items.Add(ArryPort[i]);
            }
            if (ArryPort.Length > 0) MycomboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// log 
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        private void savelog(string txt, string path, string name)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, name);
            if (!File.Exists(path))
            {
                FileStream fs = new FileStream(path, FileMode.OpenOrCreate);
                StreamWriter sw = new StreamWriter(fs);
                sw.Close();
            }

            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.BaseStream.Seek(0, SeekOrigin.End);
                    sw.Write("Time：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Cmd：{0}\n", txt, DateTime.Now);
                    sw.Flush();
                }
            }
        }

        private Mat getimgs()
        {
            try
            {
                if (rgbCamera == null) return null;

                Mat rgbFrame = new Mat();
                rgbCamera.Read(rgbFrame);
                if (rgbFrame.Empty())
                {
                    this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { "Failed to obtain the image!" });
                }
                else
                {
                    this.Invoke(new UpdatePictureBoxShowEventHandler(pictureBoxShow), pictureBox1, rgbFrame);
                }

                return rgbFrame;
            }
            catch
            {
                return null;
            }
        }

        private void pictureBoxShow(PictureBox picbox, Mat img)
        {
            Bitmap bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(img);
            picbox.Image = bitmap;
        }

        #region 3D points
        private (int, int) cacul(double x, double y, double z)
        {
            double Xc, Yc, Zc;
            {
                double rx = M[0, 0] * x + M[0, 1] * y + M[0, 2] * z + M[0, 3];
                double ry = M[1, 0] * x + M[1, 1] * y + M[1, 2] * z + M[1, 3];
                double rz = M[2, 0] * x + M[2, 1] * y + M[2, 2] * z + M[2, 3];
                Xc = rx; Yc = ry; Zc = rz;
            }

            double xn = Xc / Zc;
            double yn = Yc / Zc;
            double k1 = 0, k2 = 0, p1 = 0, p2 = 0, k3 = 0, k4 = 0, k5 = 0, k6 = 0;
            if (dist != null)
            {
                if (dist.Length > 0) k1 = dist[0];
                if (dist.Length > 1) k2 = dist[1];
                if (dist.Length > 2) p1 = dist[2];
                if (dist.Length > 3) p2 = dist[3];
                if (dist.Length > 4) k3 = dist[4];
                if (dist.Length > 5) k4 = dist[5];
                if (dist.Length > 6) k5 = dist[6];
                if (dist.Length > 7) k6 = dist[7];
            }

            double r2 = xn * xn + yn * yn;
            double r4 = r2 * r2;
            double r6 = r4 * r2;
            double radial_num = 1.0 + k1 * r2 + k2 * r4 + k3 * r6;
            double radial_den = 1.0;
            if (Math.Abs(k4) + Math.Abs(k5) + Math.Abs(k6) > 0)
                radial_den = 1.0 + k4 * r2 + k5 * r4 + k6 * r6;

            double radial = radial_num / radial_den;

            double x_tangential = 2.0 * p1 * xn * yn + p2 * (r2 + 2.0 * xn * xn);
            double y_tangential = p1 * (r2 + 2.0 * yn * yn) + 2.0 * p2 * xn * yn;

            double xd = xn * radial + x_tangential;
            double yd = yn * radial + y_tangential;

            double fx = K[0, 0], fy = K[1, 1], cx = K[0, 2], cy = K[1, 2];
            double u0 = fx * xd + cx;
            double v0 = fy * yd + cy;
            double du = 0.0015 * z * z - 0.8819 * z + 126.8;
            double dv = -0.0166 * z * z + 4.8466 * z - 450.17;
            int u = (int)(u0 + du);
            int v = (int)(v0 + dv);

            return (u, v);
        }

        private Vec3b getRGBvalues(int x, int y)
        {
            Vec3b color = new Vec3b();  // bgr
            color.Item2 = 255;
            color.Item1 = 242;
            color.Item0 = 0;

            try
            {
                if (!cameraOn)
                {
                    this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { "Camera not connected!" });
                }
                Mat Image = getimgs();
                Mat rgbImage = Image;

                if (colorcheck == 1 && rgbImage != null)
                {
                    Rgb24 rgb = ColorCalibrationApi73.CorrectPixelFromMat(
                        rgbImage,
                        x,
                        y,
                        @".\aligns\calib_matrix_img-lut.npz");
                    //Console.WriteLine("Corrected RGB = ({0}, {1}, {2})", rgb.R, rgb.G, rgb.B);
                    color.Item2 = rgb.B;
                    color.Item1 = rgb.G;
                    color.Item0 = rgb.R;
                }
                else
                {
                    OpenCvSharp.Point projectedPoint = new OpenCvSharp.Point(x, y);
                    color = rgbImage.At<Vec3b>(projectedPoint.Y, projectedPoint.X);
                }

                return color;
            }
            catch (Exception ex)
            {
                string str = "catch ERROR - getRGBvalues！" + ex;
                this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { str });
                return color;
            }
        }

        private void getRGB()
        {
            while (isrunning && cameraOn)
            {
                try
                {
                    double dist_now_port2 = distance_port2;
                    if (dist_now_port2 > max_d) continue;
                    if (readrgb)
                    {
                        readrgb = false;
                        bool isup = upordown;
                        (int x, int y) = cacul(x_now, y_now, z_now);
                        (int x2, int y2) = cacul(x_now2, y_now2, z_now2);
                        pointcolor = getRGBvalues(x, y);
                        pointcolor2 = getRGBvalues(x2, y2); 
                    }
                }
                catch (Exception ex)
                {
                    string str = "catch ERROR - getRGB！" + ex;
                    this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { str });
                }

                Thread.Sleep(100);
            }
        }

        private void paint3d()
        {
            try
            {
                while (isrunning)
                {
                    if (round_z != -1)
                    {
                        double dist_now_port2 = distance_port2;
                        double dist_now_port3 = distance_port3;

                        if (dist_now_port2 > max_d)
                        {
                            continue;
                        }

                        double theta_x_now1 = theta_x;
                        DateTime t = DateTime.Now;
                        Tuple<double, double, double> axises = getaxis_single(t, dist_now_port2, theta_x_now1);
                        Tuple<double, double, double> axises3 = getaxis_single(t, dist_now_port3, theta_x_now1);
                        x_now = axises.Item1;
                        y_now = axises.Item2;
                        z_now = axises.Item3;
                        x_now2 = axises3.Item1;
                        y_now2 = axises3.Item2;
                        z_now2 = axises3.Item3;
                        this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { "X2=" + axises.Item1 + " Y2=" + axises.Item2 + " Z2=" + axises.Item3 + " dist_now_2=" + dist_now_port2 });
                        this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { "X3=" + axises3.Item1 + " Y3=" + axises3.Item2 + " Z3=" + axises3.Item3 + " dist_now_3=" + dist_now_port3 });

                        Vec3b co1 = pointcolor;
                        Vec3b co2 = pointcolor2;
                        ColorPoint p = new ColorPoint(axises.Item1, axises.Item2, axises.Item3, co1.Item2, co1.Item1, co1.Item0);
                        ColorPoint p3 = new ColorPoint(axises3.Item1, axises3.Item2, axises3.Item3, co2.Item2, co2.Item1, co2.Item0);
                        readrgb = true;

                        bool isup = upordown;
                        if (isup)
                        {
                            pointcloud.Add(p);
                            pointcloud1.Add(p3);
                        }
                        else
                        {
                            pointcloud_d.Add(p);
                            pointcloud1_d.Add(p3);
                        }

                        Thread.Sleep(t_space);
                    }
                }
            }
            catch (Exception ex)
            {
                string str = "catch ERROR - paint3d！" + ex;
                this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { str });
            }
        }

        private Tuple<double, double, double> getaxis_single(DateTime t, double d2, double theta_x_now1)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            double D_ = 0;
            double D1 = 0;
            double D2 = 0;
            double theta_z = 0;
            double theta_z_rad = 0;
            double theta_x_now = theta_x_now1;
            double theta_x_now_rad = theta_x_now1;
            Tuple<double, double, double> cloud = new Tuple<double, double, double>(0, 0, 0);

            double rundeg_z = (t - startT).TotalSeconds * v_run_z;

            if (round_z % 2 == 0)
            {
                theta_z = rundeg_z;
                upordown = true;
            }
            else
            {
                theta_z = max_z - rundeg_z;
                upordown = false;
            }

            theta_z_rad = Math.PI / 180 * theta_z;

            if (max_x > 0 && theta_x_now > max_x)
            {
                isrunning = false;
                return cloud;
            }

            theta_x_now_rad = Math.PI / 180 * theta_x_now;
            D_ = Math.Sqrt(d2 * d2 + L * L);
            double alfa = Math.Asin(L / D_);
            double beta = Math.Asin(d2 / D_);

            if (beta + theta_z_rad == Math.PI / 2)
            {
                x = 0;
                y = 0;
                z = D_;
            }
            else
            {
                if (beta + theta_z_rad > Math.PI / 2)
                {
                    D1 = D_ * Math.Sin(Math.PI - theta_z_rad - beta);
                    D2 = D_ * Math.Cos(Math.PI - theta_z_rad - beta);
                }
                else
                {
                    D1 = D_ * Math.Sin(theta_z_rad + beta);
                    D2 = D_ * Math.Cos(theta_z_rad + beta);
                }

                if (theta_x_now_rad < Math.PI / 2)
                {
                    x = D2 * Math.Cos(theta_x_now_rad);
                    y = D2 * Math.Sin(theta_x_now_rad);
                    z = D1;
                }
                else if (theta_x_now_rad > Math.PI / 2)
                {
                    x = D2 * Math.Cos(Math.PI - theta_x_now_rad);
                    y = D2 * Math.Sin(Math.PI - theta_x_now_rad);
                    z = D1;
                }
                else
                {
                    x = 0;
                    y = D2;
                    z = D1;
                }
            }

            x = Math.Round(x, 3);
            y = Math.Round(y, 3);
            z = Math.Round(z, 3);

            cloud = new Tuple<double, double, double>(x, y, z);

            return cloud;
        }

        private void SavePointCloudAsPLY(List<ColorPoint> points, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine($"element vertex {points.Count}");
                writer.WriteLine("property float x");
                writer.WriteLine("property float y");
                writer.WriteLine("property float z");
                writer.WriteLine("property uchar red");
                writer.WriteLine("property uchar green");
                writer.WriteLine("property uchar blue");
                writer.WriteLine("end_header");

                foreach (var point in points)
                {
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0:F4} {1:F4} {2:F4} {3} {4} {5}",
                        point.X, point.Y, point.Z, point.R, point.G, point.B);
                    writer.WriteLine(line);
                }
            }
        }
        #endregion

        #region  data port
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            int length = serialPort1.BytesToRead;
            byte[] data = new byte[length];
            serialPort1.Read(data, 0, length);

            string str = "";
            str = Encoding.UTF8.GetString(data, 0, length);
            this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { str });

            string[] strs = str.Split('(', ')');
            if (strs.Count() == 3)
            {
                if (str.Contains("ROTATEMOTORDEG(") || str.Contains("EG("))
                {
                    theta_x = Convert.ToDouble(strs[1]);
                    this.Invoke(new UpdateTextEventHandler(UpdateLabel), new string[] { theta_x.ToString() });
                    round_z = (int)(theta_x / move_x);
                    startT = DateTime.Now;
                }
            }

            if (str.Contains("ROTATEMOTOREND") || str.Contains("END"))
            {
                isrunning = false;
                rgbCamera.Release();
                cameraOn = false;
                string filePath = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".ply";
                SavePointCloudAsPLY(pointcloud, filePath);
                string filePath_d = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_d.ply";
                SavePointCloudAsPLY(pointcloud_d, filePath_d);
                string filePath1 = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-1.ply";
                SavePointCloudAsPLY(pointcloud1, filePath1);
                string filePath1_d = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-1_d.ply";
                SavePointCloudAsPLY(pointcloud1_d, filePath1_d);

                AlignAndMergeToA(
                    A: pointcloud,
                    B: pointcloud1,
                    C: pointcloud_d,
                    D: pointcloud1_d,
                    tBtoAPath: root + "\\aligns\\T_B_to_A.txt",
                    tCtoAPath: root + "\\aligns\\T_C_to_A.txt",
                    tDtoBPath: root + "\\aligns\\T_D_to_B.txt",
                    tDtoCPath: root + "\\aligns\\T_D_to_C.txt",
                    outDir: root + "\\aligns\\merge_out\\" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
                );

                string t = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                savelog(textBox1.Text, root + @"\logs", "cmds_" + t + ".log");
                savelog(textBox2.Text, root + @"\logs", "data_" + t + ".log");

                this.Invoke(new UpdateTextEventHandler(UpdateTextBox), new string[] { "Files are saved." });
            }
        }

        private void UpdateTextBox(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                textBox1.AppendText("Receive【" + DateTime.Now.ToLongTimeString() + "】>>>" + str + "\r\n");
            }
        }

        private void UpdateLabel(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                label9.Text = str;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text == "")
            {
                textBox1.AppendText("PortName couldn't be empty!\r\n");
                return;
            }
            textBox1.AppendText("Open port1...\r\n");
            serialPort1.PortName = comboBox1.Text;
            serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);

            string param;
            int BaudRate = serialPort1.BaudRate;
            int Parity = (int)serialPort1.Parity;
            int DataBits = serialPort1.DataBits;
            int StopBits = (int)serialPort1.StopBits;
            int tout = serialPort1.ReadTimeout;
            param = "BaudRate:" + BaudRate + ",Parity:" + Parity + ",DataBits:" + DataBits + ",StopBits:" + StopBits + ",readtimeout:" + tout;
            textBox1.AppendText(param + "\r\n");

            serialPort1.Open();
            button1.Enabled = false;
            button3.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                textBox1.AppendText("Serial port not opened! \r\n");
                return;
            }
            string tx = "ORGRESET(1)";
            textBox1.AppendText("Send >>>>>> " + tx + "\r\n");
            serialPort1.Write(tx);
            isreset = true;

            Mat Image = getimgs();
            if (Image != null)
            {
                if (colorcheck == 1)
                {
                    Image = ColorCalibrationApi73.CorrectImageFromMat(Image, @".\aligns\calib_matrix_img-lut.npz");
                }
                string dir = Path.GetDirectoryName(img_RGB);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                bool ok = Cv2.ImWrite(img_RGB, Image);
            }
        }

        private void button_b1_Click(object sender, EventArgs e)
        {
            if (isreset)
            {
                string tx = "AUTORUNB(1)";
                textBox1.AppendText("Send >>>>>> " + tx + "\r\n");
                serialPort1.Write(tx);

                isrunning = true;
                isreset = false;
                pointcloud = new List<ColorPoint>();
                pointcloud_d = new List<ColorPoint>();
                pointcloud1 = new List<ColorPoint>();
                pointcloud1_d = new List<ColorPoint>();
                Thread th_getRGB = new Thread(getRGB);
                th_getRGB.IsBackground = true;
                th_getRGB.Start();
                th_getRGB.IsBackground = true;

                Thread.Sleep(1000);
                Thread th_3d = new Thread(paint3d);
                th_3d.IsBackground = true;
                th_3d.Start();
                th_3d.IsBackground = true;

                button_b1.Enabled = false;
                button_b0.Enabled = true;
            }
            else
            {
                MessageBox.Show("Please send the reset command first!");
                return;
            }
        }

        private void button_b0_Click(object sender, EventArgs e)
        {
            string tx = "AUTORUNB(0)";
            textBox1.AppendText("Send >>>>>> " + tx + "\r\n");
            serialPort1.Write(tx);
            isrunning = false;
            rgbCamera.Release();
            cameraOn = false;
            Cv2.DestroyAllWindows();
            string filePath = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".ply";
            SavePointCloudAsPLY(pointcloud, filePath);
            string filePath_d = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_d.ply";
            SavePointCloudAsPLY(pointcloud_d, filePath_d);
            string filePath1 = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-1.ply";
            SavePointCloudAsPLY(pointcloud1, filePath1);
            string filePath1_d = root + "\\ply\\colored_point_cloud-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-1_d.ply";
            SavePointCloudAsPLY(pointcloud1_d, filePath1_d);
            AlignAndMergeToA(
                A: pointcloud,
                B: pointcloud1,
                C: pointcloud_d,
                D: pointcloud1_d,
                tBtoAPath: root + "\\aligns\\T_B_to_A.txt",
                tCtoAPath: root + "\\aligns\\T_C_to_A.txt",
                tDtoBPath: root + "\\aligns\\T_D_to_B.txt",
                tDtoCPath: root + "\\aligns\\T_D_to_C.txt",
                outDir: root + "\\aligns\\merge_out\\" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            );

            string t = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            savelog(textBox1.Text, root + @"\logs", "cmds_" + t + ".log");
            savelog(textBox2.Text, root + @"\logs", "data_" + t + ".log");
            textBox1.AppendText("Files are saved.");

            button_b1.Enabled = true;
            button_b0.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                textBox1.AppendText("Close port1.\r\n");
                serialPort1.Close();

                button1.Enabled = true;
                button3.Enabled = false;
            }
            catch
            {
                MessageBox.Show("Close port1 error!");
            }
        }
        #endregion

        #region distance port 1
        private void button_seri2_Click(object sender, EventArgs e)
        {
            textBox2.AppendText("Open port2...\r\n");
            if (comboBox_ck2.Text == "")
            {
                textBox2.AppendText("PortName couldn't be empty!\r\n");
                return;
            }
            serialPort2.PortName = comboBox_ck2.Text;
            serialPort2.BaudRate = Convert.ToInt32(comboBox_rate2.Text);

            string param;
            int BaudRate = serialPort2.BaudRate;
            int Parity = (int)serialPort2.Parity;
            int DataBits = serialPort2.DataBits;
            int StopBits = (int)serialPort2.StopBits;
            int tout = serialPort2.ReadTimeout;
            param = "BaudRate:" + BaudRate + ",Parity:" + Parity + ",DataBits:" + DataBits + ",StopBits:" + StopBits + ",readtimeout:" + tout;
            textBox2.AppendText(param + "\r\n");

            serialPort2.Open();
            button_seri2.Enabled = false;
            button_closeseri2.Enabled = true;
        }

        private void button_closeseri2_Click(object sender, EventArgs e)
        {
            try
            {
                textBox2.AppendText("Close port2.\r\n");
                serialPort2.Close();
                button_seri2.Enabled = true;
                button_closeseri2.Enabled = false;
            }
            catch
            {
                MessageBox.Show("Close port2 error!");
            }
        }

        private void serialPort2_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            int length = serialPort2.BytesToRead;
            byte[] data = new byte[length];
            serialPort2.Read(data, 0, length);
            string str = "";
            str = BitConverter.ToString(data, 0).Replace("-", string.Empty).ToLower();
            if (length > 0)
            {
                distance_port2 = getdistance(data);
                this.Invoke(new UpdateTextEventHandler(UpdateTextBox_seri2), new string[] { "D(cm) = " + distance_port2 + "\r\n" });
            }
        }

        private double getdistance(byte[] data)
        {
            double d = 0;

            if (data.Count() != 195 || (data.Count() == 195 && data[8] != 0xB8))
            {
                this.Invoke(new UpdateTextEventHandler(UpdateTextBox_seri2), new string[] { "Receive >>> error data -- length!\r\n" });
            }
            else
            {
                string str = BitConverter.ToString(data, 0).ToLower();
                string[] dists = str.Split('-');
                double sum = 0;
                int n = 0;
                for (int i = 10; i < 190; i += 15)
                {
                    double d1 = (double)Math.Round((decimal)Convert.ToInt64(dists[i + 1] + dists[i], 16) / 10, 2);// mm -> cm
                    sum += d1;
                    n += 1;
                }
                if (n == 12)
                {
                    d = Math.Round(sum / n, 2);
                }
                else
                {
                    this.Invoke(new UpdateTextEventHandler(UpdateTextBox_seri2), new string[] { "Receive >>> error data -- datalength!\r\n" });
                }
            }

            return d;
        }

        private void UpdateTextBox_seri2(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                textBox2.AppendText("Receive【" + DateTime.Now.ToLongTimeString() + "】>>> " + str + "\r\n");
            }
        }
        #endregion

        #region distance port 2
        private void button_seri3_Click(object sender, EventArgs e)
        {
            if (comboBox_ck3.Text == "")
            {
                textBox3.AppendText("PortName couldn't be empty!\r\n");
                return;
            }
            textBox3.AppendText("Open port3...\r\n");
            serialPort3.PortName = comboBox_ck3.Text;
            serialPort3.BaudRate = Convert.ToInt32(comboBox_rate3.Text);

            string param;
            int BaudRate = serialPort3.BaudRate;
            int Parity = (int)serialPort3.Parity;
            int DataBits = serialPort3.DataBits;
            int StopBits = (int)serialPort3.StopBits;

            int tout = serialPort3.ReadTimeout;
            param = "BaudRate:" + BaudRate + ",Parity:" + Parity + ",DataBits:" + DataBits + ",StopBits:" + StopBits + ",readtimeout:" + tout;
            textBox3.AppendText(param + "\r\n");

            serialPort3.Open();

            button_seri3.Enabled = false;
            button_closeseri3.Enabled = true;
        }

        private void button_closeseri3_Click(object sender, EventArgs e)
        {
            try
            {
                textBox3.AppendText("Close port3.\r\n");
                serialPort3.Close();
                button_seri3.Enabled = true;
                button_closeseri3.Enabled = false;
            }
            catch
            {
                MessageBox.Show("Close port3 error!");
            }
        }

        private void serialPort3_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            int length = serialPort3.BytesToRead;
            byte[] data = new byte[length];
            serialPort3.Read(data, 0, length);
            string str = "";
            str = BitConverter.ToString(data, 0).Replace("-", string.Empty).ToLower();
            if (length > 0)
            {
                distance_port3 = getdistance(data);   //cm
                this.Invoke(new UpdateTextEventHandler(UpdateTextBox_seri3), new string[] { "D(cm) = " + distance_port3 + "\r\n" });
            }
        }

        private void UpdateTextBox_seri3(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                textBox3.AppendText("Receive【" + DateTime.Now.ToLongTimeString() + "】>>> " + str + "\r\n");
            }
        }
        #endregion

        private void textBox_xmax_TextChanged(object sender, EventArgs e)
        {
            if (textBox_xmax.Text != "")
            {
                max_x = Convert.ToInt16(textBox_xmax.Text);
                Settings.Default.max_x = max_x;
                Settings.Default.Save();
            }
        }

        private void textBox_zmax_TextChanged(object sender, EventArgs e)
        {
            if (textBox_zmax.Text != "")
            {
                max_z = Convert.ToInt16(textBox_zmax.Text);
                Settings.Default.max_z = max_z;
                Settings.Default.Save();
            }
        }

        private void textBox_space_TextChanged(object sender, EventArgs e)
        {
            t_space = Convert.ToInt16(textBox_space.Text);
            Settings.Default.t_space = t_space;
            Settings.Default.Save();
        }

        private void newfolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void textBox_filterd_TextChanged(object sender, EventArgs e)
        {
            if (textBox_filterd.Text != "")
            {
                max_d = Convert.ToInt16(textBox_filterd.Text);
                Settings.Default.max_d = max_d;
                Settings.Default.Save();
            }
        }

        public void AlignAndMergeToA(
            System.Collections.Generic.List<ColorPoint> A,
            System.Collections.Generic.List<ColorPoint> B,
            System.Collections.Generic.List<ColorPoint> C,
            System.Collections.Generic.List<ColorPoint> D,
            string tBtoAPath,
            string tCtoAPath,
            string tDtoBPath,
            string tDtoCPath,
            string outDir)
        {
            double[,] LoadMatrix(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    throw new System.IO.FileNotFoundException("Matrix file not found.", path);

                var tokens = new System.Collections.Generic.List<string>(32);
                foreach (var line in System.IO.File.ReadLines(path))
                {
                    var s = line.Trim();
                    if (string.IsNullOrEmpty(s)) continue;
                    s = s.Replace(",", " ").Replace("[", " ").Replace("]", " ");
                    tokens.AddRange(s.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
                }
                if (tokens.Count < 16)
                    Console.WriteLine($"Insufficient digital data in the matrix file：{tokens.Count}");

                var M = new double[4, 4];
                for (int i = 0; i < 16; i++)
                {
                    if (!double.TryParse(tokens[i],
                                         System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                                         System.Globalization.CultureInfo.InvariantCulture,
                                         out var v))
                        Console.WriteLine($"Matrix element parsing failed：{tokens[i]}");
                    M[i / 4, i % 4] = v;
                }
                return M;
            }

            double[,] Mul(double[,] A_, double[,] B_)
            {
                var C_ = new double[4, 4];
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < 4; k++) s += A_[i, k] * B_[k, j];
                        C_[i, j] = s;
                    }
                return C_;
            }

            System.Collections.Generic.List<ColorPoint> TransformCopy(System.Collections.Generic.List<ColorPoint> src, double[,] T)
            {
                var dst = new System.Collections.Generic.List<ColorPoint>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    var p = src[i];
                    double nx = T[0, 0] * p.X + T[0, 1] * p.Y + T[0, 2] * p.Z + T[0, 3];
                    double ny = T[1, 0] * p.X + T[1, 1] * p.Y + T[1, 2] * p.Z + T[1, 3];
                    double nz = T[2, 0] * p.X + T[2, 1] * p.Y + T[2, 2] * p.Z + T[2, 3];
                    dst.Add(new ColorPoint(nx, ny, nz, p.R, p.G, p.B));
                }
                return dst;
            }


            void SaveAsPlyAscii(string path, System.Collections.Generic.IReadOnlyList<ColorPoint> pts)
            {
                using (var sw = new System.IO.StreamWriter(path, false))
                {
                    sw.WriteLine("ply");
                    sw.WriteLine("format ascii 1.0");
                    sw.WriteLine("element vertex " + pts.Count);
                    sw.WriteLine("property float x");
                    sw.WriteLine("property float y");
                    sw.WriteLine("property float z");
                    sw.WriteLine("property uchar red");
                    sw.WriteLine("property uchar green");
                    sw.WriteLine("property uchar blue");
                    sw.WriteLine("end_header");

                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var p = pts[i];
                        sw.WriteLine(string.Format(inv, "{0} {1} {2} {3} {4} {5}",
                            p.X, p.Y, p.Z, p.R, p.G, p.B));
                    }
                }
            }

            string M2S(double[,] M)
            {
                return string.Join(System.Environment.NewLine, new[]{
            $"{M[0,0]:G17} {M[0,1]:G17} {M[0,2]:G17} {M[0,3]:G17}",
            $"{M[1,0]:G17} {M[1,1]:G17} {M[1,2]:G17} {M[1,3]:G17}",
            $"{M[2,0]:G17} {M[2,1]:G17} {M[2,2]:G17} {M[2,3]:G17}",
            $"{M[3,0]:G17} {M[3,1]:G17} {M[3,2]:G17} {M[3,3]:G17}",});
            }

            Directory.CreateDirectory(outDir);

            var T_BA = LoadMatrix(tBtoAPath);
            var T_CA = LoadMatrix(tCtoAPath);

            double[,] T_DB = (!string.IsNullOrWhiteSpace(tDtoBPath) && System.IO.File.Exists(tDtoBPath)) ? LoadMatrix(tDtoBPath) : null;
            double[,] T_DC = (!string.IsNullOrWhiteSpace(tDtoCPath) && System.IO.File.Exists(tDtoCPath)) ? LoadMatrix(tDtoCPath) : null;

            double[,] T_DA;
            string pathUsed;
            if (T_DC != null)
            {
                T_DA = Mul(T_CA, T_DC);
                pathUsed = "T_C_to_A @ T_D_to_C";
            }
            else if (T_DB != null)
            {
                T_DA = Mul(T_BA, T_DB);
                pathUsed = "T_B_to_A @ T_D_to_B";
            }
            else
            {
                throw new System.InvalidOperationException("Lack of matrix!");
            }

            var B_in_A = TransformCopy(B, T_BA);
            var C_in_A = TransformCopy(C, T_CA);
            var D_in_A = TransformCopy(D, T_DA);

            var outB = System.IO.Path.Combine(outDir, "B_in_A.ply");
            var outC = System.IO.Path.Combine(outDir, "C_in_A.ply");
            var outD = System.IO.Path.Combine(outDir, "D_in_A.ply");
            SaveAsPlyAscii(outB, B_in_A);
            SaveAsPlyAscii(outC, C_in_A);
            SaveAsPlyAscii(outD, D_in_A);

            var merged = new System.Collections.Generic.List<ColorPoint>(A.Count + B_in_A.Count + C_in_A.Count + D_in_A.Count);
            merged.AddRange(A);
            merged.AddRange(B_in_A);
            merged.AddRange(C_in_A);
            merged.AddRange(D_in_A);

            var outMerged = System.IO.Path.Combine(outDir, "A_B_C_D_merged_in_A-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".ply");
            SaveAsPlyAscii(outMerged, merged);

            Mat img = Cv2.ImRead(img_RGB);
            if (img != null)
            {
                string plyPath = outMerged;
                var result = WheatPredictor.PredictCHAndLAI(
                    pythonExe: pythonExe,
                    scriptPath: scriptPath,
                    plyPath: plyPath,
                    rgbImage: img,
                    chModelPath: chModel,
                    laiModelPath: laiModel
                );

                label11.Text = result.CH + " m";
                label12.Text = result.LAI + "";
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Mat checker = getimgs();
            if (checker != null)
            {
                colorcheck = ColorCalibrationApi73.BuildCalibrationFileFromChecker(
                    checker,
                    @".\aligns\colortrue.xlsx",
                    @".\aligns\calib_matrix_img-lut.npz",
                    null,
                    new CalibrationOptions());
                if (colorcheck == 1) textBox1.AppendText("Color correction was successful.\r\n");
                else textBox1.AppendText("Color correction was not successful!\r\n");
            }
            else
            {
                textBox1.AppendText("Color correction was not successful!\r\n");
            }
        }
    }
} 

