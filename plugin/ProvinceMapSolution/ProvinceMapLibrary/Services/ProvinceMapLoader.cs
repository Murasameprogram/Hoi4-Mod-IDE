using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProvinceMapLibrary.Models;

namespace ProvinceMapLibrary.Services
{
    public static class ProvinceMapLoader
    {
        public static async Task<ProvinceMapData> LoadMapAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                var frame = decoder.Frames[0];

                int width = frame.PixelWidth;
                int height = frame.PixelHeight;
                int bitsPerPixel = frame.Format.BitsPerPixel;
                if (bitsPerPixel != 24)
                    throw new NotSupportedException("仅支持24位BMP文件。");

                int bytesPerPixel = bitsPerPixel / 8;
                int stride = (width * bytesPerPixel + 3) & ~3; // 4字节对齐
                byte[] pixelData = new byte[height * stride];
                frame.CopyPixels(pixelData, stride, 0);

                var colorToInfoMap = new Dictionary<Color, ProvinceColorInfo>();
                var colorsList = new List<ProvinceColorInfo>();

                for (int y = 0; y < height; y++)
                {
                    int rowStart = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int index = rowStart + x * bytesPerPixel;
                        byte b = pixelData[index];
                        byte g = pixelData[index + 1];
                        byte r = pixelData[index + 2];
                        Color color = Color.FromRgb(r, g, b);

                        if (!colorToInfoMap.ContainsKey(color))
                        {
                            var info = new ProvinceColorInfo
                            {
                                Color = color,
                                DisplayText = $"R:{color.R} G:{color.G} B:{color.B}  #{color.R:X2}{color.G:X2}{color.B:X2}"
                            };
                            colorToInfoMap.Add(color, info);
                            colorsList.Add(info);
                        }
                    }
                }

                return new ProvinceMapData
                {
                    Colors = colorsList.AsReadOnly(),
                    PixelData = pixelData,
                    Width = width,
                    Height = height,
                    Stride = stride,
                    ColorToInfoMap = colorToInfoMap
                };
            });
        }
    }
}