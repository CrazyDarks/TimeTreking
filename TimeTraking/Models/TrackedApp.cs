using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TimeTraking.Models
{
    public class TrackedApp : INotifyPropertyChanged
    {
        private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#22C55E"));
        private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#38BDF8"));
        private static readonly IBrush StoppedBrush = new SolidColorBrush(Color.Parse("#64748B"));

        private string _displayName = string.Empty;
        private string? _executablePath;
        private long _totalSeconds;
        private bool _isRunning;
        private bool _isActive;
        private bool _isManuallyAdded;
        private Bitmap? _icon;

        public string ProcessName { get; set; } = string.Empty;

        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(_displayName) ? ProcessName : _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InitialLetter));
                }
            }
        }

        public string ProcessSubtext => $"{ProcessName}.exe";

        public string? ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (_executablePath != value)
                {
                    _executablePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public long TotalSeconds
        {
            get => _totalSeconds;
            set
            {
                if (_totalSeconds != value)
                {
                    _totalSeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTime));
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public bool IsManuallyAdded
        {
            get => _isManuallyAdded;
            set
            {
                if (_isManuallyAdded != value)
                {
                    _isManuallyAdded = value;
                    OnPropertyChanged();
                }
            }
        }

        public Bitmap? Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasIcon));
                    OnPropertyChanged(nameof(ShowFallbackIcon));
                }
            }
        }

        public bool HasIcon => _icon is not null;
        public bool ShowFallbackIcon => _icon is null;

        public string InitialLetter
        {
            get
            {
                var name = DisplayName;
                return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            }
        }

        public string FormattedTime
        {
            get
            {
                var ts = TimeSpan.FromSeconds(TotalSeconds);
                int totalHours = (int)ts.TotalHours;
                return $"{totalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            }
        }

        public string StatusText => IsActive
            ? "Активна"
            : (IsRunning ? "Працює" : "Не запущена");

        public IBrush StatusBrush => IsActive
            ? ActiveBrush
            : (IsRunning ? RunningBrush : StoppedBrush);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
