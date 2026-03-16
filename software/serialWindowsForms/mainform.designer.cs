
namespace serialWindowsForms
{
    partial class mainform
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.serialPort1 = new System.IO.Ports.SerialPort(this.components);
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.button_closeseri2 = new System.Windows.Forms.Button();
            this.button_seri2 = new System.Windows.Forms.Button();
            this.comboBox_rate2 = new System.Windows.Forms.ComboBox();
            this.comboBox_ck2 = new System.Windows.Forms.ComboBox();
            this.serialPort2 = new System.IO.Ports.SerialPort(this.components);
            this.label3 = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.button4 = new System.Windows.Forms.Button();
            this.comboBox_ck3 = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.button_closeseri3 = new System.Windows.Forms.Button();
            this.button_seri3 = new System.Windows.Forms.Button();
            this.comboBox_rate3 = new System.Windows.Forms.ComboBox();
            this.button_b0 = new System.Windows.Forms.Button();
            this.button_b1 = new System.Windows.Forms.Button();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel5 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel6 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel7 = new System.Windows.Forms.TableLayoutPanel();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.textBox_filterd = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btn_test = new System.Windows.Forms.Button();
            this.textBox_space = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textBox_zmax = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox_xmax = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.serialPort3 = new System.IO.Ports.SerialPort(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel5.SuspendLayout();
            this.tableLayoutPanel6.SuspendLayout();
            this.tableLayoutPanel7.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // serialPort1
            // 
            this.serialPort1.BaudRate = 115200;
            this.serialPort1.WriteBufferSize = 256;
            this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
            // 
            // comboBox1
            // 
            this.comboBox1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(311, 18);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(97, 24);
            this.comboBox1.TabIndex = 0;
            // 
            // comboBox2
            // 
            this.comboBox2.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Items.AddRange(new object[] {
            "115200",
            "230400"});
            this.comboBox2.Location = new System.Drawing.Point(433, 18);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(97, 24);
            this.comboBox2.TabIndex = 1;
            this.comboBox2.Text = "115200";
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Arial", 12F);
            this.button1.Location = new System.Drawing.Point(569, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(156, 35);
            this.button1.TabIndex = 2;
            this.button1.Text = "open";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox1.Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox1.Location = new System.Drawing.Point(4, 176);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size = new System.Drawing.Size(934, 387);
            this.textBox1.TabIndex = 3;
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Arial", 12F);
            this.button2.Location = new System.Drawing.Point(954, 30);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(200, 69);
            this.button2.TabIndex = 4;
            this.button2.Text = "Reset";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3
            // 
            this.button3.Enabled = false;
            this.button3.Font = new System.Drawing.Font("Arial", 12F);
            this.button3.Location = new System.Drawing.Point(762, 12);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(147, 35);
            this.button3.TabIndex = 6;
            this.button3.Text = "close";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Arial", 12F);
            this.label1.Location = new System.Drawing.Point(19, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(265, 18);
            this.label1.TabIndex = 9;
            this.label1.Text = "Communication Control Serial Port：";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Arial", 12F);
            this.label2.Location = new System.Drawing.Point(39, 33);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(222, 18);
            this.label2.TabIndex = 16;
            this.label2.Text = "Data Receiving Serial Port 1：";
            // 
            // button_closeseri2
            // 
            this.button_closeseri2.Enabled = false;
            this.button_closeseri2.Font = new System.Drawing.Font("Arial", 12F);
            this.button_closeseri2.Location = new System.Drawing.Point(761, 24);
            this.button_closeseri2.Name = "button_closeseri2";
            this.button_closeseri2.Size = new System.Drawing.Size(126, 35);
            this.button_closeseri2.TabIndex = 14;
            this.button_closeseri2.Text = "close";
            this.button_closeseri2.UseVisualStyleBackColor = true;
            this.button_closeseri2.Click += new System.EventHandler(this.button_closeseri2_Click);
            // 
            // button_seri2
            // 
            this.button_seri2.Font = new System.Drawing.Font("Arial", 12F);
            this.button_seri2.Location = new System.Drawing.Point(608, 23);
            this.button_seri2.Name = "button_seri2";
            this.button_seri2.Size = new System.Drawing.Size(124, 35);
            this.button_seri2.TabIndex = 12;
            this.button_seri2.Text = "open";
            this.button_seri2.UseVisualStyleBackColor = true;
            this.button_seri2.Click += new System.EventHandler(this.button_seri2_Click);
            // 
            // comboBox_rate2
            // 
            this.comboBox_rate2.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox_rate2.FormattingEnabled = true;
            this.comboBox_rate2.Items.AddRange(new object[] {
            "230400",
            "115200"});
            this.comboBox_rate2.Location = new System.Drawing.Point(433, 30);
            this.comboBox_rate2.Name = "comboBox_rate2";
            this.comboBox_rate2.Size = new System.Drawing.Size(119, 24);
            this.comboBox_rate2.TabIndex = 11;
            this.comboBox_rate2.Text = "230400";
            // 
            // comboBox_ck2
            // 
            this.comboBox_ck2.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox_ck2.FormattingEnabled = true;
            this.comboBox_ck2.Location = new System.Drawing.Point(285, 29);
            this.comboBox_ck2.Name = "comboBox_ck2";
            this.comboBox_ck2.Size = new System.Drawing.Size(123, 24);
            this.comboBox_ck2.TabIndex = 10;
            // 
            // serialPort2
            // 
            this.serialPort2.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort2_DataReceived);
            // 
            // label3
            // 
            this.label3.AutoEllipsis = true;
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Arial", 12F);
            this.label3.Location = new System.Drawing.Point(12, 253);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(655, 16);
            this.label3.TabIndex = 17;
            this.label3.Text = "";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(148, 3);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(640, 495);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoScroll = true;
            this.tableLayoutPanel1.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55.94837F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Controls.Add(this.panel3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel5, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 2);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 81.9063F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 18.0937F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 295F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1900, 1007);
            this.tableLayoutPanel1.TabIndex = 19;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.button4);
            this.panel3.Controls.Add(this.comboBox_ck3);
            this.panel3.Controls.Add(this.label8);
            this.panel3.Controls.Add(this.button_closeseri3);
            this.panel3.Controls.Add(this.button_seri3);
            this.panel3.Controls.Add(this.comboBox_rate3);
            this.panel3.Controls.Add(this.comboBox_ck2);
            this.panel3.Controls.Add(this.button_b0);
            this.panel3.Controls.Add(this.button_b1);
            this.panel3.Controls.Add(this.label2);
            this.panel3.Controls.Add(this.button2);
            this.panel3.Controls.Add(this.button_closeseri2);
            this.panel3.Controls.Add(this.button_seri2);
            this.panel3.Controls.Add(this.comboBox_rate2);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(3, 583);
            this.panel3.Margin = new System.Windows.Forms.Padding(2);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1894, 124);
            this.panel3.TabIndex = 20;
            // 
            // button4
            // 
            this.button4.Font = new System.Drawing.Font("Arial", 12F);
            this.button4.Location = new System.Drawing.Point(1185, 30);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(200, 69);
            this.button4.TabIndex = 24;
            this.button4.Text = "ColorCorrection";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // comboBox_ck3
            // 
            this.comboBox_ck3.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox_ck3.FormattingEnabled = true;
            this.comboBox_ck3.Location = new System.Drawing.Point(285, 74);
            this.comboBox_ck3.Name = "comboBox_ck3";
            this.comboBox_ck3.Size = new System.Drawing.Size(123, 24);
            this.comboBox_ck3.TabIndex = 19;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Arial", 12F);
            this.label8.Location = new System.Drawing.Point(39, 78);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(222, 18);
            this.label8.TabIndex = 23;
            this.label8.Text = "Data Receiving Serial Port 2：";
            // 
            // button_closeseri3
            // 
            this.button_closeseri3.Enabled = false;
            this.button_closeseri3.Font = new System.Drawing.Font("Arial", 12F);
            this.button_closeseri3.Location = new System.Drawing.Point(761, 69);
            this.button_closeseri3.Name = "button_closeseri3";
            this.button_closeseri3.Size = new System.Drawing.Size(126, 35);
            this.button_closeseri3.TabIndex = 22;
            this.button_closeseri3.Text = "close";
            this.button_closeseri3.UseVisualStyleBackColor = true;
            this.button_closeseri3.Click += new System.EventHandler(this.button_closeseri3_Click);
            // 
            // button_seri3
            // 
            this.button_seri3.Font = new System.Drawing.Font("Arial", 12F);
            this.button_seri3.Location = new System.Drawing.Point(608, 68);
            this.button_seri3.Name = "button_seri3";
            this.button_seri3.Size = new System.Drawing.Size(124, 35);
            this.button_seri3.TabIndex = 21;
            this.button_seri3.Text = "open";
            this.button_seri3.UseVisualStyleBackColor = true;
            this.button_seri3.Click += new System.EventHandler(this.button_seri3_Click);
            // 
            // comboBox_rate3
            // 
            this.comboBox_rate3.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox_rate3.FormattingEnabled = true;
            this.comboBox_rate3.Items.AddRange(new object[] {
            "230400",
            "115200"});
            this.comboBox_rate3.Location = new System.Drawing.Point(433, 75);
            this.comboBox_rate3.Name = "comboBox_rate3";
            this.comboBox_rate3.Size = new System.Drawing.Size(119, 24);
            this.comboBox_rate3.TabIndex = 20;
            this.comboBox_rate3.Text = "230400";
            // 
            // button_b0
            // 
            this.button_b0.Enabled = false;
            this.button_b0.Font = new System.Drawing.Font("Arial", 12F);
            this.button_b0.Location = new System.Drawing.Point(1649, 30);
            this.button_b0.Name = "button_b0";
            this.button_b0.Size = new System.Drawing.Size(200, 69);
            this.button_b0.TabIndex = 18;
            this.button_b0.Text = "Stop";
            this.button_b0.UseVisualStyleBackColor = true;
            this.button_b0.Click += new System.EventHandler(this.button_b0_Click);
            // 
            // button_b1
            // 
            this.button_b1.Font = new System.Drawing.Font("Arial", 12F);
            this.button_b1.Location = new System.Drawing.Point(1417, 30);
            this.button_b1.Name = "button_b1";
            this.button_b1.Size = new System.Drawing.Size(200, 69);
            this.button_b1.TabIndex = 17;
            this.button_b1.Text = "Run";
            this.button_b1.UseVisualStyleBackColor = true;
            this.button_b1.Click += new System.EventHandler(this.button_b1_Click);
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.Controls.Add(this.textBox2, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.textBox3, 1, 0);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(4, 713);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 1;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(1892, 290);
            this.tableLayoutPanel3.TabIndex = 21;
            // 
            // textBox2
            // 
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox2.Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox2.Location = new System.Drawing.Point(3, 3);
            this.textBox2.Multiline = true;
            this.textBox2.Name = "textBox2";
            this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox2.Size = new System.Drawing.Size(940, 284);
            this.textBox2.TabIndex = 13;
            // 
            // textBox3
            // 
            this.textBox3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox3.Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox3.Location = new System.Drawing.Point(949, 3);
            this.textBox3.Multiline = true;
            this.textBox3.Name = "textBox3";
            this.textBox3.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox3.Size = new System.Drawing.Size(940, 284);
            this.textBox3.TabIndex = 14;
            // 
            // tableLayoutPanel5
            // 
            this.tableLayoutPanel5.ColumnCount = 2;
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.21142F));
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49.78858F));
            this.tableLayoutPanel5.Controls.Add(this.tableLayoutPanel6, 1, 0);
            this.tableLayoutPanel5.Controls.Add(this.tableLayoutPanel4, 0, 0);
            this.tableLayoutPanel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel5.Location = new System.Drawing.Point(4, 4);
            this.tableLayoutPanel5.Name = "tableLayoutPanel5";
            this.tableLayoutPanel5.RowCount = 1;
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel5.Size = new System.Drawing.Size(1892, 573);
            this.tableLayoutPanel5.TabIndex = 23;
            // 
            // tableLayoutPanel6
            // 
            this.tableLayoutPanel6.ColumnCount = 1;
            this.tableLayoutPanel6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.Controls.Add(this.pictureBox1, 0, 0);
            this.tableLayoutPanel6.Controls.Add(this.tableLayoutPanel7, 0, 1);
            this.tableLayoutPanel6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel6.Location = new System.Drawing.Point(953, 3);
            this.tableLayoutPanel6.Name = "tableLayoutPanel6";
            this.tableLayoutPanel6.RowCount = 2;
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 88.41463F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 11.58537F));
            this.tableLayoutPanel6.Size = new System.Drawing.Size(936, 567);
            this.tableLayoutPanel6.TabIndex = 29;
            // 
            // tableLayoutPanel7
            // 
            this.tableLayoutPanel7.ColumnCount = 2;
            this.tableLayoutPanel7.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel7.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel7.Controls.Add(this.label11, 0, 0);
            this.tableLayoutPanel7.Controls.Add(this.label12, 1, 0);
            this.tableLayoutPanel7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel7.Location = new System.Drawing.Point(3, 504);
            this.tableLayoutPanel7.Name = "tableLayoutPanel7";
            this.tableLayoutPanel7.RowCount = 1;
            this.tableLayoutPanel7.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel7.Size = new System.Drawing.Size(930, 60);
            this.tableLayoutPanel7.TabIndex = 0;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label11.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label11.Location = new System.Drawing.Point(3, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(459, 60);
            this.label11.TabIndex = 0;
            this.label11.Text = "Estimation Value of Wheat Canopy Height: 0.0 m";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label12.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label12.Location = new System.Drawing.Point(468, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(459, 60);
            this.label12.TabIndex = 1;
            this.label12.Text = "Estimation Value of Wheat Leaf Area Index: 0.0";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel4.ColumnCount = 1;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.Controls.Add(this.panel2, 0, 0);
            this.tableLayoutPanel4.Controls.Add(this.textBox1, 0, 1);
            this.tableLayoutPanel4.Dock = System.Windows.Forms.DockStyle.Left;
            this.tableLayoutPanel4.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 2;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30.34623F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 69.65377F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(942, 567);
            this.tableLayoutPanel4.TabIndex = 22;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.label10);
            this.panel2.Controls.Add(this.label9);
            this.panel2.Controls.Add(this.textBox_filterd);
            this.panel2.Controls.Add(this.label7);
            this.panel2.Controls.Add(this.btn_test);
            this.panel2.Controls.Add(this.textBox_space);
            this.panel2.Controls.Add(this.label6);
            this.panel2.Controls.Add(this.textBox_zmax);
            this.panel2.Controls.Add(this.label5);
            this.panel2.Controls.Add(this.textBox_xmax);
            this.panel2.Controls.Add(this.label4);
            this.panel2.Controls.Add(this.label3);
            this.panel2.Controls.Add(this.comboBox1);
            this.panel2.Controls.Add(this.comboBox2);
            this.panel2.Controls.Add(this.button1);
            this.panel2.Controls.Add(this.button3);
            this.panel2.Controls.Add(this.label1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 3);
            this.panel2.Margin = new System.Windows.Forms.Padding(2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(936, 167);
            this.panel2.TabIndex = 19;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Arial", 12F);
            this.label10.Location = new System.Drawing.Point(723, 100);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(156, 18);
            this.label10.TabIndex = 28;
            this.label10.Text = "Rotation Angle/deg：";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label9.Location = new System.Drawing.Point(875, 101);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 16);
            this.label9.TabIndex = 27;
            this.label9.Text = "0.0°";
            // 
            // textBox_filterd
            // 
            this.textBox_filterd.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox_filterd.Location = new System.Drawing.Point(618, 123);
            this.textBox_filterd.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_filterd.Name = "textBox_filterd";
            this.textBox_filterd.Size = new System.Drawing.Size(88, 21);
            this.textBox_filterd.TabIndex = 26;
            this.textBox_filterd.Text = "300";
            this.textBox_filterd.TextChanged += new System.EventHandler(this.textBox_filterd_TextChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Arial", 12F);
            this.label7.Location = new System.Drawing.Point(426, 124);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(171, 18);
            this.label7.TabIndex = 25;
            this.label7.Text = "Filtering Distance/cm：";
            // 
            // btn_test
            // 
            this.btn_test.Location = new System.Drawing.Point(216, 216);
            this.btn_test.Name = "btn_test";
            this.btn_test.Size = new System.Drawing.Size(160, 23);
            this.btn_test.TabIndex = 24;
            this.btn_test.Text = "test";
            this.btn_test.UseVisualStyleBackColor = true;
            // 
            // textBox_space
            // 
            this.textBox_space.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox_space.Location = new System.Drawing.Point(618, 77);
            this.textBox_space.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_space.Name = "textBox_space";
            this.textBox_space.Size = new System.Drawing.Size(88, 21);
            this.textBox_space.TabIndex = 23;
            this.textBox_space.Text = "100";
            this.textBox_space.TextChanged += new System.EventHandler(this.textBox_space_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Arial", 12F);
            this.label6.Location = new System.Drawing.Point(426, 79);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(167, 18);
            this.label6.TabIndex = 22;
            this.label6.Text = "Sampling Interval/ms：";
            // 
            // textBox_zmax
            // 
            this.textBox_zmax.Location = new System.Drawing.Point(311, 124);
            this.textBox_zmax.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_zmax.Name = "textBox_zmax";
            this.textBox_zmax.Size = new System.Drawing.Size(97, 21);
            this.textBox_zmax.TabIndex = 21;
            this.textBox_zmax.Text = "40";
            this.textBox_zmax.TextChanged += new System.EventHandler(this.textBox_zmax_TextChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Arial", 12F);
            this.label5.Location = new System.Drawing.Point(19, 124);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(286, 18);
            this.label5.TabIndex = 20;
            this.label5.Text = "Forward Limit of the Flipping Axis/deg：";
            // 
            // textBox_xmax
            // 
            this.textBox_xmax.Location = new System.Drawing.Point(311, 76);
            this.textBox_xmax.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_xmax.Name = "textBox_xmax";
            this.textBox_xmax.Size = new System.Drawing.Size(97, 21);
            this.textBox_xmax.TabIndex = 19;
            this.textBox_xmax.Text = "45";
            this.textBox_xmax.TextChanged += new System.EventHandler(this.textBox_xmax_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Arial", 12F);
            this.label4.Location = new System.Drawing.Point(19, 76);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(288, 18);
            this.label4.TabIndex = 18;
            this.label4.Text = "Forward Limit of the Rotation Axis/deg：";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.AutoSize = true;
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 71.63242F));
            this.tableLayoutPanel2.Controls.Add(this.tableLayoutPanel1, 0, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 796F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(1904, 1011);
            this.tableLayoutPanel2.TabIndex = 20;
            // 
            // serialPort3
            // 
            this.serialPort3.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort3_DataReceived);
            // 
            // mainform
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1904, 1011);
            this.Controls.Add(this.tableLayoutPanel2);
            this.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Name = "mainform";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Image and Point Cloud Integrated Scanning Software";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel5.ResumeLayout(false);
            this.tableLayoutPanel6.ResumeLayout(false);
            this.tableLayoutPanel7.ResumeLayout(false);
            this.tableLayoutPanel7.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.IO.Ports.SerialPort serialPort1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button_closeseri2;
        private System.Windows.Forms.Button button_seri2;
        private System.Windows.Forms.ComboBox comboBox_rate2;
        private System.Windows.Forms.ComboBox comboBox_ck2;
        private System.IO.Ports.SerialPort serialPort2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TextBox textBox_zmax;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox_xmax;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox_space;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button button_b0;
        private System.Windows.Forms.Button button_b1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btn_test;
        private System.Windows.Forms.TextBox textBox_filterd;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.ComboBox comboBox_ck3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button button_closeseri3;
        private System.Windows.Forms.Button button_seri3;
        private System.Windows.Forms.ComboBox comboBox_rate3;
        private System.IO.Ports.SerialPort serialPort3;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel5;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel6;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel7;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button button4;
    }
}

