﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ColorMine.ColorSpaces;
using Newtonsoft.Json;

namespace AcnhPixelPress
{
    public partial class Form1 : Form
    {
        private string _imagePath = "";
        private Rgb _red;
        private Rgb _blue;
        private Rgb _yellow;
        private Rgb _black;
        private Rgb _white;
        private int _pollingRate;
        private BulletinDrawing _bulletinDrawing;
        private CancellationTokenSource _source;


        public Form1()
        {
            InitializeComponent();
            CheckConfig();
        }

        private void CheckConfig()
        {
            var configPath = @AppDomain.CurrentDomain.BaseDirectory + "\\config";
            Directory.CreateDirectory(configPath);
            var folderPath = configPath + "\\config.json";

            var densities = new[] {2, 4};
            foreach (var density in densities)
            {
                densityCombobox.Items.Add(density);
            }

            densityCombobox.SelectedIndex = 1;

            var resizePercentages = new[] {50, 60, 80, 100};
            foreach (var resizePercentage in resizePercentages)
            {
                resizeCombobox.Items.Add(resizePercentage);
            }

            resizeCombobox.SelectedIndex = 3;

            startXTextbox.Text = "100";
            startYTextbox.Text = "100";

            if (File.Exists(folderPath))
            {
                var config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(folderPath));
                if (config == null) return;
                ipTextbox.Text = config.IP;
                _red = config.Red;
                _blue = config.Blue;
                _yellow = config.Yellow;
                _black = config.Black;
                _white = config.White;
                _pollingRate = config.PollingRate;
            }
            else
            {
                _red = new() {R = 255, G = 204, B = 204};
                _blue = new() {R = 173, G = 216, B = 230};
                _yellow = new() {R = 255, G = 255, B = 208};
                _black = new() {R = 38, G = 38, B = 38};
                _white = new() {R = 255, G = 255, B = 255};
                var config = new Configuration
                {
                    IP = "192.168.0.1",
                    Red = _red, Blue = _blue, Yellow = _yellow, Black = _black, White = _white,
                    PollingRate = 31
                };
                ipTextbox.Text = config.IP;
                _pollingRate = config.PollingRate;
                var json = JsonConvert.SerializeObject(config);
                File.WriteAllText(folderPath, json);
            }
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                var connectionString = BulletinDrawing.ConnectSysbot(ipTextbox.Text, _pollingRate);
                if (connectionString.Contains("successfully"))
                {
                    string jsonPath = @AppDomain.CurrentDomain.BaseDirectory + "\\config\\config.json";
                    string json = File.ReadAllText(jsonPath);
                    dynamic jsonObj = JsonConvert.DeserializeObject(json);
                    if (jsonObj != null && !jsonObj["IP"].Equals(ipTextbox.Text))
                    {
                        jsonObj["IP"] = ipTextbox.Text;
                        string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                        File.WriteAllText(jsonPath, output);
                    }
                }


                AddLogText(connectionString);
            }
            catch (SocketException exception)
            {
                AddLogText(exception.GetType().ToString());
            }
        }

        private async void drawButton_Click(object sender, EventArgs e)
        {
            _source = new CancellationTokenSource();
            var token = _source.Token;
            try
            {
                Task drawImage;
                if (resizeCombobox.Text.Equals("100"))
                {
                    drawButton.Enabled = false;
                    AddLogText("Drawing, please don't do anything on your console until it is finished.");
                    drawImage = Task.Run(() => _bulletinDrawing.DrawImage(_pollingRate, 0, 0, token), token);
                    await drawImage;
                }
                else
                {
                    var startX = int.Parse(startXTextbox.Text);
                    var startY = int.Parse(startYTextbox.Text);
                    if (!_bulletinDrawing.CheckBoundaries(int.Parse(resizeCombobox.Text), startX, startY))
                    {
                        drawButton.Enabled = false;
                        AddLogText("Drawing, please don't do anything on your console until it is finished.");
                        drawImage = Task.Run(() => _bulletinDrawing.DrawImage(_pollingRate, startX, startY, token),
                            token);
                        await drawImage;
                    }
                    else
                    {
                        MessageBox.Show("Your drawing will be out of boundary!");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                AddLogText("Drawing has been cancelled. Please wait a bit for the drawing to stop.");
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Please select your image and connect to your Nintendo Switch. You will have to reconnect if your Switch went into Sleep Mode.",
                    "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            drawButton.Enabled = true;
        }

        private void imageButton_Click(object sender, EventArgs e)
        {
            var choofdlog = new OpenFileDialog();
            const string filter = "image files (*.jpg, *.png, *.gif)|*.jpg; *.png; *.gif";
            choofdlog.Filter = filter;
            choofdlog.FilterIndex = 1;

            if (choofdlog.ShowDialog() != DialogResult.OK) return;
            _imagePath = choofdlog.FileName;
            var fileName = Path.GetFileName(_imagePath);
            AddLogText($"added {fileName}");

            loadedImage.ImageLocation = _imagePath;
            loadedImage.SizeMode = PictureBoxSizeMode.Zoom;

            SetImageAndParse(int.Parse(resizeCombobox.Text), int.Parse(densityCombobox.Text));
        }

        private void AddLogText(string text)
        {
            var sb = new StringBuilder(logTextbox.Text);
            sb.AppendLine(text);
            logTextbox.Text = sb.ToString();
            logTextbox.SelectionStart = logTextbox.TextLength;
            logTextbox.ScrollToCaret();
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            _source.Cancel();
            drawButton.Enabled = true;
        }


        private void SetImageAndParse(int resizePercentage, int pixelDensity)
        {
            _bulletinDrawing = new BulletinDrawing();
            _bulletinDrawing.SetColors(_red, _blue, _yellow, _black, _white);
            _bulletinDrawing.ParseImage(_imagePath, resizePercentage, pixelDensity, blackWhiteCheckbox.Checked);
            double duration = _bulletinDrawing.CalculateDuration();
            AddLogText($"This drawing will take around {duration} seconds");
            if (_bulletinDrawing.TooManyPixel())
            {
                MessageBox.Show("Your drawing won't get completed as there is not enough ink!");
            }
        }

        private void densityCombobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_bulletinDrawing != null)
            {
                SetImageAndParse(int.Parse(resizeCombobox.Text), int.Parse(densityCombobox.Text));
            }
        }

        private void resizeCombobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_bulletinDrawing != null)
            {
                SetImageAndParse(int.Parse(resizeCombobox.Text), int.Parse(densityCombobox.Text));
            }
        }

        private void blackWhiteCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (_imagePath != "")
            {
                SetImageAndParse(int.Parse(resizeCombobox.Text), int.Parse(densityCombobox.Text));
            }
        }

        private void previewButton_Click(object sender, EventArgs e)
        {
            try
            {
                var startX = int.Parse(startXTextbox.Text);
                var startY = int.Parse(startYTextbox.Text);
                if (resizeCombobox.Text.Equals("100"))
                {
                    startX = 0;
                    startY = 0;
                }

                var previewImage = _bulletinDrawing.CreatePreviewImage(startX, startY);
                var window = new ImageScreen();
                window.SetPreview(previewImage);
                window.Show();
            }
            catch (NullReferenceException)
            {
                MessageBox.Show("Please load an image first!");
            }
        }
    }
}