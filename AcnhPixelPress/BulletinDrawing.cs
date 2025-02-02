﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using ColorMine.ColorSpaces;
using ColorMine.ColorSpaces.Comparisons;
using static System.Threading.Tasks.Task;

namespace AcnhPixelPress
{
    public class BulletinDrawing
    {
        private static Controller _sysBot;

        // Colors inside the game
        private static readonly Rgb OriginalBlue = new() {R = 123, G = 210, B = 217};
        private static readonly Rgb OriginalRed = new() {R = 253, G = 169, B = 153};
        private static readonly Rgb OriginalYellow = new() {R = 228, G = 219, B = 26};
        private static readonly Rgb OriginalBlack = new() {R = 0, G = 0, B = 0};
        private static readonly Rgb OriginalWhite = new() {R = 255, G = 255, B = 255};

        private AcnhColor _blueColor;
        private AcnhColor _redColor;
        private AcnhColor _yellowColor;
        private AcnhColor _blackColor;
        private AcnhColor _whiteColor;

        private readonly List<AcnhColor> _rgbList = new();
        private readonly List<PicturePixel> _pixelList = new();

        public static string ConnectSysbot(string ipAddress, int pollingRate)
        {
            string message;
            _sysBot = new Controller();
            try
            {
                _sysBot.Connect(ipAddress, "6000");
                var version = _sysBot.Bot.GetVersion();
                message = $"Connected successfully, sys-botbase {version} detected!";
            }
            catch (Exception e)
            {
                message = e.Message;
            }

            _sysBot.Bot.sendPollRate(pollingRate);
            return message;
        }

        public void SetColors(Rgb red, Rgb blue, Rgb yellow, Rgb black, Rgb white)
        {
            _blueColor = new AcnhColor("blue", blue, OriginalBlue, 609, 80);
            _redColor = new AcnhColor("red", red, OriginalRed, 670, 80);
            _yellowColor = new AcnhColor("yellow", yellow, OriginalYellow, 730, 80);
            _blackColor = new AcnhColor("black", black, OriginalBlack, 540, 80);
            _whiteColor = new AcnhColor("white", white, OriginalWhite, 0, 0);
            AcnhColor[] colorRange = {_blueColor, _redColor, _yellowColor, _blackColor, _whiteColor};
            _rgbList.AddRange(colorRange);
        }


        public void ParseImage(string imagePath, int resizePercentage, int pixelDensity, bool blackWhite)
        {
            var img = Image.FromFile(imagePath);
            var resized = (Bitmap) FixedSize(img, 760 * resizePercentage / 100, 460 * resizePercentage / 100);

            if (blackWhite)
            {
                ConvertToBlackWhite(resized);
            }

            for (var xCoord = 0; xCoord < 760 * resizePercentage / 100; xCoord += pixelDensity)
            {
                for (var yCoord = 0; yCoord < 460 * resizePercentage / 100; yCoord += pixelDensity)
                {
                    var pixelColor = resized.GetPixel(xCoord, yCoord);
                    var currentPixel = new Rgb {R = pixelColor.R, G = pixelColor.G, B = pixelColor.B};
                    var smallestDistance = double.MaxValue;
                    var chosenColor = new AcnhColor();
                    foreach (var currentColor in _rgbList)
                    {
                        var deltaE = currentPixel.Compare(currentColor.MatchingColor, new Cie1976Comparison());
                        if (!(deltaE < smallestDistance)) continue;
                        smallestDistance = deltaE;
                        chosenColor = currentColor;
                    }

                    var x = xCoord;
                    var y = yCoord;

                    if (chosenColor.Name.Equals("white")) continue;
                    var picturePixel = new PicturePixel(chosenColor, x, y);
                    _pixelList.Add(picturePixel);
                }
            }
        }

        public async Task DrawImage(int pollingRate, int shiftX, int shiftY, CancellationToken token)
        {
            foreach (var pixel in _pixelList)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                DrawPoint(pixel.AcnhColor, pixel.xPixelCoordinate + 259 + shiftX,
                    pixel.YPixelCoordinate + 168 + shiftY);
                // numbers from testing
                await Delay(pollingRate + 93, token);
            }
        }

        public Image CreatePreviewImage(int shiftX, int shiftY)
        {
            var convertedBitmap = new Bitmap(760, 460);

            foreach (var pixel in _pixelList)
            {
                var chosenColor = pixel.AcnhColor;
                var pixelRgb = chosenColor.OriginalColor;
                var color = Color.FromArgb((int) pixelRgb.R, (int) pixelRgb.G, (int) pixelRgb.B);
                var brush = new SolidBrush(color);
                const float radius = 2.4f;
                using var g = Graphics.FromImage(convertedBitmap);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, pixel.xPixelCoordinate + shiftX - radius,
                    pixel.YPixelCoordinate + shiftY - radius, 2 * radius, 2 * radius);
            }

            return convertedBitmap;
        }

        private static void ConvertToBlackWhite(Image sourceImage)
        {
            using var gr = Graphics.FromImage(sourceImage);
            var grayMatrix = new[]
            {
                new[] {0.299f, 0.299f, 0.299f, 0, 0},
                new[] {0.587f, 0.587f, 0.587f, 0, 0},
                new[] {0.114f, 0.114f, 0.114f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
            };

            var ia = new ImageAttributes();
            ia.SetColorMatrix(new ColorMatrix(grayMatrix));
            ia.SetThreshold(0.7f);
            var rc = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
            gr.DrawImage(sourceImage, rc, 0, 0, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, ia);
        }

        public double CalculateDuration()
        {
            // numbers from testing
            var duration = Math.Round(_pixelList.Count * 0.062 * 2.15);
            return duration;
        }

        public bool TooManyPixel()
        {
            return _pixelList.Count > 10000;
        }

        public bool CheckBoundaries(int resizePercentage, int startX, int startY)
        {
            var lastPixel = _pixelList[^1];
            var pixelX = lastPixel.xPixelCoordinate + startX;
            var pixelY = lastPixel.YPixelCoordinate + startY;
            return pixelX > 760 + 259 || pixelY > 460 + 168;
        }

        /**
         * No additional color press
         */
        private void DrawPointPrevious(AcnhColor color, int x, int y, string previousColor)
        {
            if (!previousColor.Equals(color.Name))
            {
                TouchPixel(color.ColorXPosition, color.ColorYPosition);
            }

            TouchPixel(x, y);
        }

        private void DrawPoint(AcnhColor color, int x, int y)
        {
            TouchPixel(color.ColorXPosition, color.ColorYPosition);
            TouchPixel(x, y);
        }


        private void TouchPixel(int x, int y)
        {
            _sysBot.Bot.touch($"{x} {y}");
        }

        /**
         * Fix ratio of the input picture
         */
        private Image FixedSize(Image imgPhoto, int width, int height)
        {
            var sourceWidth = imgPhoto.Width;
            var sourceHeight = imgPhoto.Height;
            var sourceX = 0;
            var sourceY = 0;
            var destX = 0;
            var destY = 0;

            float nPercent;
            float nPercentW;
            float nPercentH;

            nPercentW = (width / (float) sourceWidth);
            nPercentH = (height / (float) sourceHeight);
            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
                destX = Convert.ToInt16((width -
                                         (sourceWidth * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentW;
                destY = Convert.ToInt16((height -
                                         (sourceHeight * nPercent)) / 2);
            }

            var destWidth = (int) (sourceWidth * nPercent);
            var destHeight = (int) (sourceHeight * nPercent);

            var bmPhoto = new Bitmap(width, height,
                PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(imgPhoto.HorizontalResolution,
                imgPhoto.VerticalResolution);

            var grPhoto = Graphics.FromImage(bmPhoto);
            grPhoto.Clear(Color.White);
            grPhoto.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }
    }
}