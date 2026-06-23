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
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Configuration;
using WinForge.GUI.Helpers;
using WinForge.GUI.Services;
using WinForge.GUI.Services.PowerShell;
using Wpf.Ui.Controls;

namespace WinForge.GUI.ViewModels;

/// <summary>
/// ViewModel for the dedicated Logs view.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly string _logsPath;
    private readonly string _jsonLogsPath;
    private readonly IFileDialogService _fileDialogService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _logger;

    [ObservableProperty]
    private ObservableCollection<LogFileEntry> _logFiles = new();

    [ObservableProperty]
    private ObservableCollection<LogFileEntry> _filteredLogFiles = new();

    [ObservableProperty]
    private LogFileEntry? _selectedLogFile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedLogLevel = Resources.Resources.Logs_Filter_All;

    [ObservableProperty]
    private DateTime? _filterDate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    public bool HasNoLogs => !IsLoading && FilteredLogFiles.Count == 0;

    public List<string> LogLevels { get; } =
    [
        Resources.Resources.Logs_Filter_All,
        Resources.Resources.Logs_Filter_Text,
        Resources.Resources.Logs_Filter_Json,
        Resources.Resources.Logs_Filter_Error
    ];

    public LogsViewModel(
        IFileDialogService? fileDialogService = null,
        IDialogService? dialogService = null,
        IRepositoryPathService? pathService = null,
        ILoggerFactory? loggerFactory = null)
    {
        _fileDialogService = fileDialogService ?? new FileDialogService();
        _dialogService = dialogService ?? new DialogService();
        IRepositoryPathService resolvedPathService = pathService ?? new RepositoryPathService();
        _logsPath = resolvedPathService.LogsDirectory;
        _jsonLogsPath = Path.Combine(_logsPath, WinForgePathNames.JsonLogsDirectoryName);
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<LogsViewModel>();

        RefreshAsync().SafeFireAndForget();
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
        StatusMessage = Resources.Resources.Logs_Loading;

        try
        {
            await Task.Run(() =>
            {
                List<LogFileEntry> files = new List<LogFileEntry>();
                long totalSize = 0;
                string logsRoot = Path.GetFullPath(_logsPath);
                string jsonRoot = Path.GetFullPath(_jsonLogsPath);

                if (Directory.Exists(_logsPath))
                {
                    foreach (string file in Directory.EnumerateFiles(_logsPath, "*.*", SearchOption.AllDirectories))
                    {
                        FileInfo info = new FileInfo(file);
                        string fileDirectory = Path.GetFullPath(info.DirectoryName ?? string.Empty);
                        bool isTopLevelTextLog = string.Equals(fileDirectory, logsRoot, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(info.Extension, ".log", StringComparison.OrdinalIgnoreCase);
                        bool isJsonLog = fileDirectory.StartsWith(jsonRoot, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(info.Extension, ".json", StringComparison.OrdinalIgnoreCase);
                        bool isErrorLog = info.Name.Contains("error", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(info.Extension, ".log", StringComparison.OrdinalIgnoreCase);

                        if (!isTopLevelTextLog && !isJsonLog && !isErrorLog)
                        {
                            continue;
                        }

                        string logType = isTopLevelTextLog
                            ? Resources.Resources.Logs_Filter_Text
                            : isJsonLog
                                ? Resources.Resources.Logs_Filter_Json
                                : Resources.Resources.Logs_Filter_Error;
                        SymbolRegular icon = isJsonLog
                            ? SymbolRegular.BracesVariable24
                            : isErrorLog && !isTopLevelTextLog
                                ? SymbolRegular.ErrorCircle24
                                : SymbolRegular.Document24;
                        LogEntryKind iconKind = isJsonLog
                            ? LogEntryKind.Json
                            : isErrorLog && !isTopLevelTextLog
                                ? LogEntryKind.Error
                                : LogEntryKind.Text;

                        totalSize += info.Length;
                        files.Add(new LogFileEntry
                        {
                            FullPath = file,
                            FileName = info.Name,
                            Size = info.Length,
                            LastModified = info.LastWriteTime,
                            LogType = logType,
                            Icon = icon,
                            IconKind = iconKind
                        });
                    }
                }

                Dispatcher? dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        LogFiles.Clear();
                        foreach (LogFileEntry? file in files.OrderByDescending(f => f.LastModified))
                        {
                            LogFiles.Add(file);
                        }
                        TotalSizeFormatted = FormatFileSize(totalSize);
                    });
                }
            });

            ApplyFilters();
            StatusMessage = string.Format(Resources.Resources.Logs_Status_FilesFound, LogFiles.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_Error_LoadFailed, ex.Message);
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
        SelectedLogLevel = Resources.Resources.Logs_Filter_All;
        FilterDate = null;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<LogFileEntry> filtered = LogFiles.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(f =>
                f.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply log level filter
        if (SelectedLogLevel != Resources.Resources.Logs_Filter_All)
        {
            filtered = filtered.Where(f => f.LogType == SelectedLogLevel);
        }

        // Apply date filter
        if (FilterDate.HasValue)
        {
            filtered = filtered.Where(f => f.LastModified.Date == FilterDate.Value.Date);
        }

        FilteredLogFiles.Clear();
        foreach (LogFileEntry? file in filtered)
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
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = _logsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_Error_OpenFolderFailed, ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        try
        {
            string? filePath = await _fileDialogService.ShowSaveAsync(new FileDialogOptions(
                string.Empty,
                Resources.Resources.Logs_Export_Filter,
                DefaultFileName: $"WinForge_Logs_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExtension: ".zip"));

            if (filePath != null)
            {
                // Security: Use unpredictable random temp directory name
                string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);
                string tempDirFullPath = Path.GetFullPath(tempDir);

                try
                {
                    int copiedCount = 0;
                    foreach (LogFileEntry logFile in FilteredLogFiles)
                    {
                        try
                        {
                            // Security: Validate filename doesn't contain path traversal
                            string safeFileName = Path.GetFileName(logFile.FileName);
                            if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Contains(".."))
                            {
                                _logger.LogWarning($"Skipping unsafe filename: {logFile.FileName}");
                                continue;
                            }

                            string destPath = Path.Combine(tempDir, safeFileName);

                            // Security: Verify destination stays within temp directory (TOCTOU protection)
                            string destFullPath = Path.GetFullPath(destPath);
                            if (!destFullPath.StartsWith(tempDirFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogWarning($"Path traversal attempt blocked: {destPath}");
                                continue;
                            }

                            File.Copy(logFile.FullPath, destPath, true);
                            copiedCount++;
                        }
                        catch (FileNotFoundException)
                        {
                            // File was deleted between enumeration and copy - skip silently
                            _logger.LogWarning($"Log file not found (deleted): {logFile.FullPath}");
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            // Could be symlink attack or permission issue - log and skip
                            _logger.LogWarning($"Access denied copying log (possible symlink): {logFile.FullPath} - {ex.Message}");
                        }
                        catch (IOException ex)
                        {
                            // File locked or other IO issue - log and skip
                            _logger.LogWarning($"IO error copying log: {logFile.FullPath} - {ex.Message}");
                        }
                    }

                    if (copiedCount == 0)
                    {
                        StatusMessage = Resources.Resources.Logs_Export_None;
                        return;
                    }

                    // Create ZIP
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, filePath);
                    StatusMessage = string.Format(
                        Resources.Resources.Logs_Export_Success,
                        Path.GetFileName(filePath),
                        copiedCount);
                }
                finally
                {
                    // Cleanup temp directory
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to cleanup temp directory: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_Export_Failed, ex.Message);
        }
    }

    [RelayCommand]
    private async Task ClearOldLogsAsync()
    {
        bool confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.Confirm_ClearOldLogs_Title,
            Resources.Resources.Confirm_ClearOldLogs_Message,
            Resources.Resources.Confirm_ClearOldLogs_Btn,
            Resources.Resources.Common_Cancel);

        if (!confirmed) return;

        try
        {
            DateTime cutoffDate = DateTime.Now.AddDays(-7);
            int deletedCount = 0;

            foreach (LogFileEntry? file in LogFiles.Where(f => f.LastModified < cutoffDate).ToList())
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

            RefreshAsync().SafeFireAndForget();
            StatusMessage = string.Format(Resources.Resources.Logs_ClearOld_Success, deletedCount);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_ClearOld_Failed, ex.Message);
        }
    }

    [RelayCommand]
    private void ViewLog(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(logFile.FullPath))
            {
                StatusMessage = Resources.Resources.Logs_Error_EmptyPath;
                return;
            }

            if (!File.Exists(logFile.FullPath))
            {
                StatusMessage = string.Format(Resources.Resources.Logs_Error_NotFound, logFile.FileName);
                RefreshAsync().SafeFireAndForget();
                return;
            }

            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = logFile.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_Error_OpenLogFailed, ex.Message);
        }
    }

    [RelayCommand]
    private void CopyLogPath(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        try
        {
            Clipboard.SetText(logFile.FullPath);
            StatusMessage = Resources.Resources.Logs_Status_PathCopied;
        }
        catch
        {
            // Clipboard operation failed
        }
    }

    [RelayCommand]
    private async Task DeleteLogAsync(LogFileEntry? logFile)
    {
        if (logFile == null) return;

        bool confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.Confirm_DeleteLog_Title,
            string.Format(Resources.Resources.Confirm_DeleteLog_Message, logFile.FileName),
            Resources.Resources.Confirm_Delete_Btn,
            Resources.Resources.Common_Cancel);

        if (!confirmed) return;

        try
        {
            File.Delete(logFile.FullPath);
            LogFiles.Remove(logFile);
            FilteredLogFiles.Remove(logFile);
            StatusMessage = string.Format(Resources.Resources.Logs_Delete_Success, logFile.FileName);
            OnPropertyChanged(nameof(HasNoLogs));
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Resources.Resources.Logs_Delete_Failed, ex.Message);
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
/// Classifies a log file entry for presentation purposes (icon coloring).
/// The mapping to an actual brush is performed in the view layer.
/// </summary>
public enum LogEntryKind
{
    /// <summary>Plain text log file.</summary>
    Text,
    /// <summary>Structured JSON log file.</summary>
    Json,
    /// <summary>Error log file.</summary>
    Error
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
    public string LogType { get; set; } = Resources.Resources.Logs_Filter_Text;
    public SymbolRegular Icon { get; set; } = SymbolRegular.Document24;
    public LogEntryKind IconKind { get; set; } = LogEntryKind.Text;

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
