/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the dedicated Logs view.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly string _logsPath;
    private readonly string _jsonLogsPath;

    [ObservableProperty]
    private ObservableCollection<LogFileEntry> _logFiles = new();

    [ObservableProperty]
    private ObservableCollection<LogFileEntry> _filteredLogFiles = new();

    [ObservableProperty]
    private LogFileEntry? _selectedLogFile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedLogLevel = "All";

    [ObservableProperty]
    private DateTime? _filterDate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    public bool HasNoLogs => !IsLoading && FilteredLogFiles.Count == 0;

    public List<string> LogLevels { get; } = new() { "All", "Text", "JSON", "Error" };

    public LogsViewModel()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logsPath = Path.Combine(localAppData, "Win11Forge", "Logs");
        _jsonLogsPath = Path.Combine(_logsPath, "json");

        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedLogLevelChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnFilterDateChanged(DateTime? value)
    {
        ApplyFilters();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = Resources.Resources.Logs_Loading ?? "Loading logs...";

        try
        {
            await Task.Run(() =>
            {
                var files = new List<LogFileEntry>();
                long totalSize = 0;

                // Get text logs
                if (Directory.Exists(_logsPath))
                {
                    var textLogs = Directory.GetFiles(_logsPath, "*.log", SearchOption.TopDirectoryOnly);
                    foreach (var file in textLogs)
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;
                        files.Add(new LogFileEntry
                        {
                            FullPath = file,
                            FileName = info.Name,
                            Size = info.Length,
                            LastModified = info.LastWriteTime,
                            LogType = "Text",
                            Icon = PackIconKind.FileDocument,
                            IconColor = Brushes.Gray
                        });
                    }
                }

                // Get JSON logs
                if (Directory.Exists(_jsonLogsPath))
                {
                    var jsonLogs = Directory.GetFiles(_jsonLogsPath, "*.json", SearchOption.TopDirectoryOnly);
                    foreach (var file in jsonLogs)
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;
                        files.Add(new LogFileEntry
                        {
                            FullPath = file,
                            FileName = info.Name,
                            Size = info.Length,
                            LastModified = info.LastWriteTime,
                            LogType = "JSON",
                            Icon = PackIconKind.CodeJson,
                            IconColor = Brushes.DodgerBlue
                        });
                    }
                }

                // Get error logs (any file containing "error" in name)
                if (Directory.Exists(_logsPath))
                {
                    var errorLogs = Directory.GetFiles(_logsPath, "*error*.log", SearchOption.AllDirectories)
                        .Where(f => !files.Any(existing => existing.FullPath == f));
                    foreach (var file in errorLogs)
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;
                        files.Add(new LogFileEntry
                        {
                            FullPath = file,
                            FileName = info.Name,
                            Size = info.Length,
                            LastModified = info.LastWriteTime,
                            LogType = "Error",
                            Icon = PackIconKind.AlertCircle,
                            IconColor = Brushes.Red
                        });
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogFiles.Clear();
                    foreach (var file in files.OrderByDescending(f => f.LastModified))
                    {
                        LogFiles.Add(file);
                    }
                    TotalSizeFormatted = FormatFileSize(totalSize);
                });
            });

            ApplyFilters();
            StatusMessage = $"{LogFiles.Count} log files found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoLogs));
        }
    }

    [RelayCommand]
    private void Search()
    {
        ApplyFilters();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedLogLevel = "All";
        FilterDate = null;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = LogFiles.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(f =>
                f.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply log level filter
        if (SelectedLogLevel != "All")
        {
            filtered = filtered.Where(f => f.LogType == SelectedLogLevel);
        }

        // Apply date filter
        if (FilterDate.HasValue)
        {
            filtered = filtered.Where(f => f.LastModified.Date == FilterDate.Value.Date);
        }

        FilteredLogFiles.Clear();
        foreach (var file in filtered)
        {
            FilteredLogFiles.Add(file);
        }

        OnPropertyChanged(nameof(HasNoLogs));
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            if (!Directory.Exists(_logsPath))
            {
                Directory.CreateDirectory(_logsPath);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = _logsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportLogs()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                DefaultExt = ".zip",
                FileName = $"Win11Forge_Logs_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                // Create a temporary directory and copy logs
                var tempDir = Path.Combine(Path.GetTempPath(), $"Win11Forge_Logs_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                foreach (var logFile in FilteredLogFiles)
                {
                    var destPath = Path.Combine(tempDir, logFile.FileName);
                    File.Copy(logFile.FullPath, destPath, true);
                }

                // Create ZIP
                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, dialog.FileName);

                // Cleanup
                Directory.Delete(tempDir, true);

                StatusMessage = $"Logs exported to {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearOldLogs()
    {
        var result = MessageBox.Show(
            Resources.Resources.Confirm_ClearOldLogs_Message ?? "Delete log files older than 7 days?",
            Resources.Resources.Confirm_ClearOldLogs_Title ?? "Clear Old Logs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var cutoffDate = DateTime.Now.AddDays(-7);
            var deletedCount = 0;

            foreach (var file in LogFiles.Where(f => f.LastModified < cutoffDate).ToList())
            {
                try
                {
                    File.Delete(file.FullPath);
                    deletedCount++;
                }
                catch
                {
                    // Skip files that can't be deleted
                }
            }

            _ = RefreshAsync();
            StatusMessage = $"Deleted {deletedCount} old log files";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewLog(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = logFile.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening log: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyLogPath(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        try
        {
            Clipboard.SetText(logFile.FullPath);
            StatusMessage = "Path copied to clipboard";
        }
        catch
        {
            // Clipboard operation failed
        }
    }

    [RelayCommand]
    private void DeleteLog(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        var result = MessageBox.Show(
            $"Delete {logFile.FileName}?",
            "Delete Log File",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            File.Delete(logFile.FullPath);
            LogFiles.Remove(logFile);
            FilteredLogFiles.Remove(logFile);
            StatusMessage = $"Deleted {logFile.FileName}";
            OnPropertyChanged(nameof(HasNoLogs));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting log: {ex.Message}";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Represents a log file entry.
/// </summary>
public class LogFileEntry
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string LogType { get; set; } = "Text";
    public PackIconKind Icon { get; set; } = PackIconKind.FileDocument;
    public Brush IconColor { get; set; } = Brushes.Gray;

    public string SizeFormatted
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = Size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
