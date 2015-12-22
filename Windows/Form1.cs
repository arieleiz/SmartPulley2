using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OfficeOpenXml;

namespace SmartPulley2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            lbl_time.Text = "";
            lbl_count.Text = "";
            //lbl_A.Text = "";
            lbl_V.Text = "";
            lbl_X.Text = "";

            try
            {
                var sk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("HARDWARE\\DEVICEMAP\\SERIALCOMM");
                string[] names = sk.GetValueNames();
                string first = Properties.Settings.Default.SettingPort;
                int index = -1;
                int selindex = -1;
                foreach(var name in names)
                {
                    string port = sk.GetValue(name).ToString().Substring(0, 4);
                    cbCOM.Items.Add(port);
                    if (string.Compare(port, first, true) == 0)
                        selindex = index;
                    if (string.IsNullOrEmpty(first))
                        first = port;
                    ++index;
                }
                if (selindex == -1)
                {
                    if (string.IsNullOrEmpty(first))
                    {
                        MessageBox.Show("No serial ports found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }
                    selindex = 0;
                }

                Properties.Settings.Default.SettingPort = first;
                Properties.Settings.Default.Save();
                cbCOM.SelectedIndex = selindex;
                create_chart();
            }
            catch(Exception e)
            {
                MessageBox.Show("Could not enumerate serial ports!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            OpenPort();
            StopCollection();
            cbCOM.SelectedIndexChanged += cbCOM_SelectedIndexChanged;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            string str = serialport.ReadExisting();
            if (str.Length == 0)
                return;

            _buffer = _buffer + str;
            while (_buffer.Length > 0)
            {
                int start = _buffer.IndexOf('T');
                if (start < 0)
                {
                    _buffer = "";
                    return;
                }
                if (start > 0)
                    _buffer = _buffer.Substring(start);
                int eor = _buffer.IndexOf('T', 1);
                if (eor < 0)
                    return;
                string cur = _buffer.Substring(0, eor);
                _buffer = _buffer.Substring(eor);
                processRecord(cur);
            }
        }

        private void processRecord(string record)
        {
            int idx = record.IndexOf('C');
            if (idx < 0)
                return;
            int time = 0, count = 0;
            if (!int.TryParse(record.Substring(1, idx - 1), out time)
                || !int.TryParse(record.Substring(idx + 1), out count))
                return;

            updateCounts(time, count);
        }

        private void updateCounts(int time, int count)
        {
            double dx = (count - last_count) * STRIDE;
            double dt;
            if (time >= last_time)
                dt = (time - last_time) / 1000.0;
            else
                dt = ((30000 - last_time) + time) / 1000.0;

            double v = (dx / dt) * (1.0 - DECAY) + last_v * DECAY;
            double dv = v - last_v;
            double a = (dv / dt) * (1.0 - DECAY) + last_a * DECAY;
            last_t += dt;
            last_x = count * STRIDE;
            last_v = v;
            //last_a = a;
            last_count = count;
            last_time = time;
            lbl_time.Text = string.Format("{0:F4} ms", last_t);
            lbl_count.Text = string.Format("{0}", count);
            lbl_X.Text = string.Format("{0:F4} m", last_x);
            lbl_V.Text = string.Format("{0:F4} m/s", last_v < 0.00001 ? 0 : last_v);
            //lbl_A.Text = string.Format("{0:F4} m/s^2", last_a < 0.00001 ? 0 : last_a);

            //if(dx > 0)
             _points.Add(new DataPoint() { x = last_x, t = last_t });

            _series_x.Points.AddXY(last_t, last_x);
            _series_v.Points.AddXY(last_t, last_v);
            //_series_a.Points.AddXY(last_t, last_a);
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            _running = !_running;
            if (_running)
            {
                _points.Clear();
                last_t = 0; last_time = 0; last_count = 0; last_x = 0; last_v = 0; last_a = 0;
                _series_x.Points.Clear();
                _series_v.Points.Clear();
                serialport.DiscardOutBuffer();
                serialport.DiscardInBuffer();
                _buffer = "";

                //_series_a.Points.Clear();
                serialport.WriteLine(START_STRING);
                timer.Enabled = true;
            }
            else
            {
                StopCollection();
                _buffer = "";
            }
            btnStartStop.Text = _running ? "&Stop" : "&Start";
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            StopCollection();
            serialport.Close();
        }

        private void OpenPort()
        {
            serialport.PortName = Properties.Settings.Default.SettingPort;
            serialport.BaudRate = 115200;
            serialport.Open();
        }

        private void StopCollection()
        {
            serialport.DiscardOutBuffer();
            serialport.WriteLine(STOP_STRING);
            serialport.DiscardInBuffer();
            timer.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void cbCOM_SelectedIndexChanged(object sender, EventArgs e)
        {
            StopCollection();
            serialport.Close();

            Properties.Settings.Default.SettingPort = cbCOM.Text;
            Properties.Settings.Default.Save();

            OpenPort();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), String.Format("SP2_{0}.xlsx", DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            using (ExcelPackage package = new ExcelPackage(new System.IO.FileInfo(path)))
            {
                // add a new worksheet to the empty workbook
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("SmartPulley");
                worksheet.Cells[1, 1].Value = "Time (seconds)";
                worksheet.Cells[1, 2].Value = "Distance (meters)";
                for (int i = 0; i < _points.Count; ++i)
                {
                    DataPoint dp = _points[i];
                    worksheet.Cells[2 + i, 1].Value = dp.t;
                    worksheet.Cells[2 + i, 2].Value = dp.x;
                }
                package.Save();
            }
            System.Diagnostics.Process.Start(path);
        }

        void create_chart()
        {
            _chart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            _chart.BackColor = System.Drawing.Color.WhiteSmoke;
            _chart.BackGradientStyle = System.Windows.Forms.DataVisualization.Charting.GradientStyle.TopBottom;
            _chart.BackSecondaryColor = System.Drawing.Color.White;
            _chart.BorderlineColor = System.Drawing.Color.FromArgb(((int)(((byte)(26)))), ((int)(((byte)(59)))), ((int)(((byte)(105)))));
            _chart.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            _chart.BorderlineWidth = 2;
            this.Controls.Add(_chart);

            _chart.Location = new System.Drawing.Point(19, 144);
            _chart.Name = "chart";
            _chart.Size = new System.Drawing.Size(520, 264);
            var chartArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            chartArea.Name = "Default";

            var legend = new System.Windows.Forms.DataVisualization.Charting.Legend();
            legend.Name = "Default";

            _series_x = new System.Windows.Forms.DataVisualization.Charting.Series();
            _series_v = new System.Windows.Forms.DataVisualization.Charting.Series();
            //_series_a = new System.Windows.Forms.DataVisualization.Charting.Series();

            _series_x.Legend = "Default";
            _series_x.ChartArea = "Default";
            _series_x.Name = "X";
            _series_x.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastPoint;

            _series_v.Legend = "Default";
            _series_v.ChartArea = "Default";
            _series_v.Name = "V";
            _series_v.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastPoint;

            //_series_a.Legend = "Default";
            //_series_a.ChartArea = "Default";
            //_series_a.Name = "A";
            //_series_a.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastPoint;

            _chart.ChartAreas.Add(chartArea);
            _chart.Legends.Add(legend);
            _chart.Series.Add(_series_x);
            _chart.Series.Add(_series_v);
            //_chart.Series.Add(_series_a);

        }

        private readonly double STRIDE = 0.007875;
        private readonly string STOP_STRING = "0";
        private readonly string START_STRING = "1";
        private bool _running = false;
        private String _buffer = "";
        private int last_time = 0, last_count = 0;
        private double last_x = 0, last_v = 0, last_a = 0, last_t = 0;
        private readonly double DECAY = 0.3;
        private struct DataPoint
        {
            public double x, t;
        }
        private List<DataPoint> _points = new List<DataPoint>();
        private System.Windows.Forms.DataVisualization.Charting.Chart _chart;
        System.Windows.Forms.DataVisualization.Charting.Series _series_x, _series_v;//, _series_a;
    }
}
