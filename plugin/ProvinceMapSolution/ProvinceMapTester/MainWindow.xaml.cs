using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ProvinceMapLibrary.Models;

namespace ProvinceMapTester
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private ProvinceColorInfo? _selectedColor;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }

        public ProvinceColorInfo? SelectedColor
        {
            get => _selectedColor;
            set
            {
                _selectedColor = value;
                OnPropertyChanged();
                if (value != null)
                {
                    MessageBox.Show($"当前选中省份颜色: {value.DisplayText}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}