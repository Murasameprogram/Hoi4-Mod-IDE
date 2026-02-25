using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ProvinceMapLibrary.Models;
using ProvinceMapLibrary.Services;

namespace ProvinceMapLibrary.Controls
{
    public partial class ProvinceMapViewer : UserControl
    {
        // 依赖属性：文件路径
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(ProvinceMapViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFilePathChanged));

        // 依赖属性：当前选中的颜色信息
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(ProvinceColorInfo), typeof(ProvinceMapViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public ProvinceColorInfo SelectedColor
        {
            get => (ProvinceColorInfo)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private ProvinceMapData? _mapData;
        private Dictionary<Color, ProvinceColorInfo>? _colorToInfoMap;
        private double _zoomFactor = 1.0; // 当前缩放因子
        private const double ZoomMin = 0.1;
        private const double ZoomMax = 10.0;

        public ProvinceMapViewer()
        {
            InitializeComponent();
        }

        private static async void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ProvinceMapViewer)d;
            string newPath = e.NewValue as string ?? string.Empty;
            if (!string.IsNullOrEmpty(newPath))
            {
                await control.LoadMapAsync(newPath);
            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "BMP 文件|*.bmp|所有文件|*.*",
                Title = "选择 provinces.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName; // 触发依赖属性变化
            }
        }

        private async Task LoadMapAsync(string filePath)
        {
            try
            {
                btnOpen.IsEnabled = false;
                txtStatus.Text = "正在解析，请稍候...";
                lstColors.ItemsSource = null;
                imgPreview.Source = null;
                _mapData = null;
                _colorToInfoMap = null;
                _zoomFactor = 1.0;
                imageScaleTransform.ScaleX = imageScaleTransform.ScaleY = 1.0;

                var data = await ProvinceMapLoader.LoadMapAsync(filePath);
                _mapData = data;
                _colorToInfoMap = data.ColorToInfoMap;

                txtStatus.Text = $"解析完成，共找到 {data.Colors.Count} 个唯一颜色。";
                lstColors.ItemsSource = data.Colors;

                // 从像素数据重建 BitmapSource 用于显示
                var bitmapSource = BitmapSource.Create(
                    data.Width, data.Height, 96, 96, PixelFormats.Bgr24, null,
                    data.PixelData, data.Stride);
                imgPreview.Source = bitmapSource;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "加载失败";
            }
            finally
            {
                btnOpen.IsEnabled = true;
            }
        }

        private void ImgPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_mapData == null || imgPreview.Source == null) return;

            Point mousePos = e.GetPosition(imgPreview);

            // 将屏幕坐标转换为原始像素坐标（考虑缩放）
            int x = (int)(mousePos.X / _zoomFactor);
            int y = (int)(mousePos.Y / _zoomFactor);

            // 确保坐标在有效范围内
            if (x >= 0 && x < _mapData.Width && y >= 0 && y < _mapData.Height)
            {
                int index = y * _mapData.Stride + x * 3; // BGR24
                byte b = _mapData.PixelData[index];
                byte g = _mapData.PixelData[index + 1];
                byte r = _mapData.PixelData[index + 2];
                Color color = Color.FromRgb(r, g, b);

                if (_colorToInfoMap != null && _colorToInfoMap.TryGetValue(color, out ProvinceColorInfo? info))
                {
                    lstColors.SelectedItem = info;
                    lstColors.ScrollIntoView(info);
                    txtSelectedColor.Text = $"选中颜色: {info.DisplayText}";
                    SelectedColor = info;
                }
                else
                {
                    txtSelectedColor.Text = "未找到对应颜色（可能为海洋或未定义省份？）";
                }
            }
        }

        private void ImgPreview_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 检测按下的修饰键
            bool isCtrlDown = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            // 优先处理 Ctrl 缩放
            if (isCtrlDown)
            {
                double delta = e.Delta > 0 ? 1.1 : 0.9; // 每次滚轮缩放10%
                double newZoom = _zoomFactor * delta;

                // 限制范围
                if (newZoom < ZoomMin) newZoom = ZoomMin;
                if (newZoom > ZoomMax) newZoom = ZoomMax;

                _zoomFactor = newZoom;
                imageScaleTransform.ScaleX = _zoomFactor;
                imageScaleTransform.ScaleY = _zoomFactor;

                e.Handled = true;
            }
            // 其次处理 Shift 横向滚动
            else if (isShiftDown)
            {
                // 每次滚动移动的像素量（可调整）
                const double scrollAmount = 40.0;
                double offset = imageScrollViewer.HorizontalOffset;

                if (e.Delta > 0)
                    offset -= scrollAmount; // 向左滚动
                else
                    offset += scrollAmount; // 向右滚动

                // 限制范围
                offset = Math.Max(0, Math.Min(offset, imageScrollViewer.ScrollableWidth));

                imageScrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
            // 如果不按任何修饰键，ScrollViewer 会自动处理垂直滚动
        }
    }
}