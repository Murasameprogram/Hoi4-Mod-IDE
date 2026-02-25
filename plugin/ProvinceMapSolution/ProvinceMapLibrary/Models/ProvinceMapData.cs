using System.Collections.Generic;
using System.Windows.Media;

namespace ProvinceMapLibrary.Models
{
    public class ProvinceMapData
    {
        public IReadOnlyList<ProvinceColorInfo> Colors { get; set; } = new List<ProvinceColorInfo>();
        public byte[] PixelData { get; set; } = Array.Empty<byte>();   // BGR格式，包含 stride 填充
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public Dictionary<Color, ProvinceColorInfo> ColorToInfoMap { get; set; } = new();
    }
}