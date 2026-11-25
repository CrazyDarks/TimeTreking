using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using TimeTraking.Services;

namespace TimeTraking
{
    public class ProcessCandidate
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public Bitmap? Icon { get; set; }
        public bool IsIgnored { get; set; }
        public bool HasVisibleWindow { get; set; }

        public bool HasIcon => Icon is not null;
        public bool ShowFallbackIcon => Icon is null;
        public string InitialLetter => string.IsNullOrEmpty(DisplayName)
            ? "?"
            : DisplayName.Substring(0, 1).ToUpperInvariant();
    }

    public partial class AddProcessWindow : Window
    {
        private readonly List<ProcessCandidate> _allCandidates = new();
        private string? _pickedExePath;

        public ProcessCandidate? SelectedResult { get; private set; }

        public AddProcessWindow() : this(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
        {
        }

        public AddProcessWindow(HashSet<string> ignoredProcesses)
        {
            InitializeComponent();
            LoadCandidates(ignoredProcesses);
        }

        private void LoadCandidates(HashSet<string> ignoredProcesses)
        {
            var running = WindowsProcessHelper.GetRunningProcessesSnapshot();

            var list = new List<ProcessCandidate>();
            foreach (var kv in running.Values)
            {
                list.Add(new ProcessCandidate
                {
                    ProcessName = kv.ProcessName,
                    DisplayName = kv.DisplayName,
                    ExecutablePath = kv.ExecutablePath,
                    Icon = WindowsProcessHelper.GetIconForExecutable(kv.ExecutablePath),
                    IsIgnored = ignoredProcesses.Contains(kv.ProcessName),
                    HasVisibleWindow = kv.HasVisibleWindow
                });
            }

            // Also include ignored processes that aren't currently running so user can easily restore them
            foreach (var ignored in ignoredProcesses)
            {
                if (!running.ContainsKey(ignored))
                {
                    list.Add(new ProcessCandidate
                    {
                        ProcessName = ignored,
                        DisplayName = ignored,
                        IsIgnored = true,
                        HasVisibleWindow = false
                    });
                }
            }

            _allCandidates.Clear();
            _allCandidates.AddRange(list
                .OrderByDescending(c => c.HasVisibleWindow)
                .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase));

            ApplyFilter(string.Empty);
        }

        private void ApplyFilter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ProcessesListBox.ItemsSource = _allCandidates.ToList();
                return;
            }

            var q = query.Trim();
            ProcessesListBox.ItemsSource = _allCandidates
                .Where(c => c.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                         || c.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _pickedExePath = null;
            SelectedExeText.IsVisible = false;
            ApplyFilter(SearchBox.Text ?? string.Empty);
        }

        private async void BrowseExeButton_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Оберіть виконуваний файл (.exe)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Програми (*.exe)")
                    {
                        Patterns = new[] { "*.exe" }
                    }
                }
            });

            var file = files.FirstOrDefault();
            if (file is null)
                return;

            var localPath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                return;

            _pickedExePath = localPath;
            string processName = Path.GetFileNameWithoutExtension(localPath);
            string displayName = WindowsProcessHelper.GetFriendlyName(processName, localPath);

            SelectedResult = new ProcessCandidate
            {
                ProcessName = processName,
                DisplayName = displayName,
                ExecutablePath = localPath,
                Icon = WindowsProcessHelper.GetIconForExecutable(localPath)
            };

            SelectedExeText.Text = $"Обрано файл: {localPath}";
            SelectedExeText.IsVisible = true;
            Close(SelectedResult);
        }

        private void ProcessesListBox_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ProcessesListBox.SelectedItem is ProcessCandidate candidate)
            {
                SelectedResult = candidate;
                Close(SelectedResult);
            }
        }

        private void ConfirmAddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (ProcessesListBox.SelectedItem is ProcessCandidate selected)
            {
                SelectedResult = selected;
                Close(SelectedResult);
                return;
            }

            string manualName = (SearchBox.Text ?? string.Empty).Trim();
            if (manualName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                manualName = manualName[..^4];

            if (string.IsNullOrWhiteSpace(manualName))
                return;

            SelectedResult = new ProcessCandidate
            {
                ProcessName = manualName,
                DisplayName = manualName,
                ExecutablePath = _pickedExePath,
                Icon = WindowsProcessHelper.GetIconForExecutable(_pickedExePath)
            };

            Close(SelectedResult);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}

