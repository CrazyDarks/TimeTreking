using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TimeTraking.Services
{
    public class SavedAppRecord
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public long TotalSeconds { get; set; }
        public bool IsManuallyAdded { get; set; }
    }

    public class TrackerState
    {
        public List<SavedAppRecord> Apps { get; set; } = new();
        public List<string> IgnoredProcesses { get; set; } = new();
        public bool TrackActiveOnly { get; set; } = false;
    }

    public static class TrackerStorage
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTraking");

        private static readonly string DataFilePath = Path.Combine(DataDirectory, "tracker-data.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static TrackerState Load()
        {
            try
            {
                if (!File.Exists(DataFilePath))
                    return new TrackerState();

                var json = File.ReadAllText(DataFilePath);
                return JsonSerializer.Deserialize<TrackerState>(json, JsonOptions) ?? new TrackerState();
            }
            catch
            {
                return new TrackerState();
            }
        }

        public static void Save(TrackerState state)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                var json = JsonSerializer.Serialize(state, JsonOptions);
                File.WriteAllText(DataFilePath, json);
            }
            catch
            {
                // Ignore transient I/O errors during periodic save
            }
        }
    }
}

