using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TimeTraking.Models;
using TimeTraking.Services;

namespace TimeTraking
{
    public partial class MainWindow : Window
    {
        private static readonly IBrush ActiveTabBackground = new SolidColorBrush(Color.Parse("#1E293B"));
        private static readonly IBrush ActiveTabForeground = new SolidColorBrush(Color.Parse("#F8FAFC"));
        private static readonly IBrush InactiveTabForeground = new SolidColorBrush(Color.Parse("#94A3B8"));

        // System shell processes that shouldn't clutter the auto-detected list unless added manually
        private static readonly HashSet<string> DefaultHiddenShellProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "TextInputHost",
            "ShellExperienceHost",
            "StartMenuExperienceHost",
            "SearchHost",
            "LockApp",
            "Widgets",
            "SystemSettings"
        };

        private readonly Dictionary<string, TrackedApp> _trackedByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<TrackedApp> _visibleTrackedApps = new();
        private readonly ObservableCollection<string> _visibleIgnoredProcesses = new();
        private readonly HashSet<string> _ignoredProcesses = new(StringComparer.OrdinalIgnoreCase);

        private readonly DispatcherTimer _timer;
        private int _secondsSinceSave;
        private bool _showingIgnoredTab;
        private bool _trackActiveOnly;
        private bool _isInitializing = true;

        public MainWindow()
        {
            InitializeComponent();

            TaskListBox.ItemsSource = _visibleTrackedApps;
            IgnoredListBox.ItemsSource = _visibleIgnoredProcesses;

            LoadSavedState();
            _isInitializing = false;

            Closing += (_, _) => SaveState();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => OnTrackerTick();
            _timer.Start();

            OnTrackerTick();
        }

        private void LoadSavedState()
        {
            var state = TrackerStorage.Load();
            _trackActiveOnly = state.TrackActiveOnly;
            TrackActiveOnlyCheckBox.IsChecked = _trackActiveOnly;

            foreach (var ignored in state.IgnoredProcesses)
            {
                if (!string.IsNullOrWhiteSpace(ignored))
                    _ignoredProcesses.Add(ignored);
            }

            foreach (var rec in state.Apps)
            {
                if (string.IsNullOrWhiteSpace(rec.ProcessName) || _ignoredProcesses.Contains(rec.ProcessName))
                    continue;

                var app = new TrackedApp
                {
                    ProcessName = rec.ProcessName,
                    DisplayName = string.IsNullOrWhiteSpace(rec.DisplayName) ? rec.ProcessName : rec.DisplayName,
                    ExecutablePath = rec.ExecutablePath,
                    TotalSeconds = rec.TotalSeconds,
                    IsManuallyAdded = rec.IsManuallyAdded,
                    Icon = WindowsProcessHelper.GetIconForExecutable(rec.ExecutablePath)
                };

                _trackedByName[app.ProcessName] = app;
            }

            RefreshVisibleLists();
            UpdateSummaryHeader();
        }

        private void SaveState()
        {
            var state = new TrackerState
            {
                TrackActiveOnly = _trackActiveOnly,
                IgnoredProcesses = _ignoredProcesses.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                Apps = _trackedByName.Values.Select(a => new SavedAppRecord
                {
                    ProcessName = a.ProcessName,
                    DisplayName = a.DisplayName,
                    ExecutablePath = a.ExecutablePath,
                    TotalSeconds = a.TotalSeconds,
                    IsManuallyAdded = a.IsManuallyAdded
                }).ToList()
            };

            TrackerStorage.Save(state);
        }

        private void OnTrackerTick()
        {
            var runningSnapshot = WindowsProcessHelper.GetRunningProcessesSnapshot();
            bool collectionChanged = false;

            // 1. Auto-discover visible user apps that are not ignored
            foreach (var proc in runningSnapshot.Values)
            {
                if (_ignoredProcesses.Contains(proc.ProcessName))
                    continue;

                if (_trackedByName.TryGetValue(proc.ProcessName, out var existing))
                {
                    if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(proc.ExecutablePath))
                    {
                        existing.ExecutablePath = proc.ExecutablePath;
                        existing.DisplayName = proc.DisplayName;
                        existing.Icon = WindowsProcessHelper.GetIconForExecutable(proc.ExecutablePath);
                    }
                    else if (existing.Icon is null && !string.IsNullOrEmpty(existing.ExecutablePath))
                    {
                        existing.Icon = WindowsProcessHelper.GetIconForExecutable(existing.ExecutablePath);
                    }
                }
                else if (proc.HasVisibleWindow && !DefaultHiddenShellProcesses.Contains(proc.ProcessName))
                {
                    var app = new TrackedApp
                    {
                        ProcessName = proc.ProcessName,
                        DisplayName = proc.DisplayName,
                        ExecutablePath = proc.ExecutablePath,
                        Icon = WindowsProcessHelper.GetIconForExecutable(proc.ExecutablePath),
                        IsRunning = true,
                        IsActive = proc.IsForeground
                    };

                    _trackedByName[app.ProcessName] = app;
                    collectionChanged = true;
                }
            }

            // 2. Update running/active state and increment tracked seconds
            foreach (var app in _trackedByName.Values)
            {
                if (runningSnapshot.TryGetValue(app.ProcessName, out var info))
                {
                    app.IsRunning = true;
                    app.IsActive = info.IsForeground;

                    if (!_trackActiveOnly || info.IsForeground)
                    {
                        app.TotalSeconds += 1;
                    }
                }
                else
                {
                    app.IsRunning = false;
                    app.IsActive = false;
                }
            }

            if (collectionChanged)
            {
                RefreshVisibleLists();
            }

            UpdateSummaryHeader();

            _secondsSinceSave++;
            if (_secondsSinceSave >= 15)
            {
                _secondsSinceSave = 0;
                SaveState();
            }
        }

        private void RefreshVisibleLists()
        {
            string query = (FilterSearchBox.Text ?? string.Empty).Trim();

            // Preserve selected item when refreshing
            var selectedApp = TaskListBox.SelectedItem as TrackedApp;

            var filteredTracked = _trackedByName.Values
                .Where(a => string.IsNullOrEmpty(query)
                         || a.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || a.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.IsActive)
                .ThenByDescending(a => a.IsRunning)
                .ThenByDescending(a => a.TotalSeconds)
                .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SyncCollection(_visibleTrackedApps, filteredTracked);

            if (selectedApp is not null && _visibleTrackedApps.Contains(selectedApp))
            {
                TaskListBox.SelectedItem = selectedApp;
            }

            var filteredIgnored = _ignoredProcesses
                .Where(name => string.IsNullOrEmpty(query)
                            || name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SyncCollection(_visibleIgnoredProcesses, filteredIgnored);
        }

        private static void SyncCollection<T>(ObservableCollection<T> target, List<T> desired)
        {
            // Remove items no longer in desired
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(target[i]))
                    target.RemoveAt(i);
            }

            // Insert / reorder cleanly
            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                int currentIndex = target.IndexOf(item);
                if (currentIndex < 0)
                {
                    target.Insert(i, item);
                }
                else if (currentIndex != i)
                {
                    target.Move(currentIndex, i);
                }
            }
        }

        private void UpdateSummaryHeader()
        {
            long totalSec = _trackedByName.Values.Sum(a => a.TotalSeconds);
            var ts = TimeSpan.FromSeconds(totalSec);
            TotalTimeText.Text = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";

            int runningCount = _trackedByName.Values.Count(a => a.IsRunning);
            RunningCountText.Text = runningCount.ToString();

            int ignoredCount = _ignoredProcesses.Count;
            IgnoredSummaryText.Text = ignoredCount.ToString();
            IgnoredCountBadge.Text = ignoredCount.ToString();
            TrackedCountBadge.Text = _trackedByName.Count.ToString();
        }

        private void IgnoreAndRemoveApp(TrackedApp app)
        {
            _trackedByName.Remove(app.ProcessName);
            _ignoredProcesses.Add(app.ProcessName);
            RefreshVisibleLists();
            UpdateSummaryHeader();
            SaveState();
        }

        private async void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new AddProcessWindow(_ignoredProcesses);
            var result = await dialog.ShowDialog<ProcessCandidate?>(this);
            if (result is null || string.IsNullOrWhiteSpace(result.ProcessName))
                return;

            // Remove from ignore list if it was previously ignored
            _ignoredProcesses.Remove(result.ProcessName);

            if (!_trackedByName.TryGetValue(result.ProcessName, out var app))
            {
                app = new TrackedApp
                {
                    ProcessName = result.ProcessName,
                    DisplayName = string.IsNullOrWhiteSpace(result.DisplayName) ? result.ProcessName : result.DisplayName,
                    ExecutablePath = result.ExecutablePath,
                    Icon = result.Icon ?? WindowsProcessHelper.GetIconForExecutable(result.ExecutablePath),
                    IsManuallyAdded = true
                };
                _trackedByName[app.ProcessName] = app;
            }
            else
            {
                app.IsManuallyAdded = true;
                if (!string.IsNullOrWhiteSpace(result.ExecutablePath))
                {
                    app.ExecutablePath = result.ExecutablePath;
                    app.Icon = result.Icon ?? WindowsProcessHelper.GetIconForExecutable(result.ExecutablePath);
                }
            }

            SwitchTab(showIgnored: false);
            OnTrackerTick();
            RefreshVisibleLists();
            TaskListBox.SelectedItem = app;
            SaveState();
        }

        private void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_showingIgnoredTab)
            {
                if (IgnoredListBox.SelectedItem is string ignoredName)
                {
                    RestoreIgnoredProcess(ignoredName);
                }
                return;
            }

            if (TaskListBox.SelectedItem is TrackedApp selectedApp)
            {
                IgnoreAndRemoveApp(selectedApp);
            }
        }

        private void InlineRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TrackedApp app)
            {
                IgnoreAndRemoveApp(app);
            }
        }

        private void RestoreIgnoredButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is string processName)
            {
                RestoreIgnoredProcess(processName);
            }
        }

        private void RestoreIgnoredProcess(string processName)
        {
            _ignoredProcesses.Remove(processName);
            OnTrackerTick();
            RefreshVisibleLists();
            UpdateSummaryHeader();
            SaveState();
        }

        private void TabTrackedButton_Click(object? sender, RoutedEventArgs e)
        {
            SwitchTab(showIgnored: false);
        }

        private void TabIgnoredButton_Click(object? sender, RoutedEventArgs e)
        {
            SwitchTab(showIgnored: true);
        }

        private void SwitchTab(bool showIgnored)
        {
            _showingIgnoredTab = showIgnored;
            TrackedPanel.IsVisible = !showIgnored;
            IgnoredPanel.IsVisible = showIgnored;

            SectionTitleText.Text = showIgnored
                ? "Ігноровані процеси (не записуються)"
                : "Відстежувані програми";

            DeleteButton.Content = showIgnored
                ? "Повернути у відстеження"
                : "Видалити і не записувати";

            TabTrackedButton.Background = showIgnored ? Brushes.Transparent : ActiveTabBackground;
            TabTrackedButton.Foreground = showIgnored ? InactiveTabForeground : ActiveTabForeground;

            TabIgnoredButton.Background = showIgnored ? ActiveTabBackground : Brushes.Transparent;
            TabIgnoredButton.Foreground = showIgnored ? ActiveTabForeground : InactiveTabForeground;
        }

        private void FilterSearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshVisibleLists();
        }

        private void TrackActiveOnlyCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing)
                return;

            _trackActiveOnly = TrackActiveOnlyCheckBox.IsChecked == true;
            SaveState();
        }

        private void ExitButton_Click(object? sender, RoutedEventArgs e)
        {
            SaveState();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Close();
            }
        }
    }
}