using System;
using System.IO;
using System.Text;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// File-based logging service with rotation support.
    ///
    /// Algorithm: Appends log entries to daily log files with rotation when
    /// size exceeds configured maximum. Thread-safe via locking.
    ///
    /// Performance: Buffered writes with flush on important messages.
    /// File I/O is synchronous but should not block UI (called from background).
    ///
    /// Security: Logs are stored in user's AppData folder with appropriate permissions.
    /// Sensitive data (passwords, keys) should never be logged.
    /// </summary>
    public class LoggingService : IDisposable
    {
        private readonly string _logDirectory;
        private readonly int _maxFileSizeMb;
        private readonly LogLevel _minLevel;
        private readonly object _writeLock = new object();
        private StreamWriter _currentWriter;
        private string _currentFilePath;
        private long _currentFileSize;
        private bool _disposed;

        private static LoggingService _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// Gets the singleton instance of the logging service.
        /// </summary>
        public static LoggingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LoggingService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes or reinitializes the singleton with new settings.
        /// </summary>
        public static void Initialize(string logDirectory, int maxFileSizeMb = 10, LogLevel minLevel = LogLevel.Normal)
        {
            lock (_instanceLock)
            {
                _instance?.Dispose();
                _instance = new LoggingService(logDirectory, maxFileSizeMb, minLevel);
            }
        }

        public LoggingService() : this(GetDefaultLogDirectory(), 10, LogLevel.Normal)
        {
        }

        public LoggingService(string logDirectory, int maxFileSizeMb = 10, LogLevel minLevel = LogLevel.Normal)
        {
            _logDirectory = logDirectory ?? GetDefaultLogDirectory();
            _maxFileSizeMb = maxFileSizeMb > 0 ? maxFileSizeMb : 10;
            _minLevel = minLevel;

            EnsureDirectoryExists();
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public void Info(string message)
        {
            Write(LogLevel.Normal, "INFO", message);
        }

        /// <summary>
        /// Logs a verbose/debug message.
        /// </summary>
        public void Verbose(string message)
        {
            Write(LogLevel.Verbose, "DEBUG", message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public void Warning(string message)
        {
            Write(LogLevel.Minimal, "WARN", message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public void Error(string message)
        {
            Write(LogLevel.Minimal, "ERROR", message);
        }

        /// <summary>
        /// Logs an error with exception details.
        /// </summary>
        public void Error(string message, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine($"Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }
            if (ex.InnerException != null)
            {
                sb.AppendLine($"Inner exception: {ex.InnerException.Message}");
            }
            Write(LogLevel.Minimal, "ERROR", sb.ToString());
        }

        /// <summary>
        /// Logs a network operation.
        /// </summary>
        public void LogOperation(string operation, string adapter, bool success, string details = "")
        {
            var status = success ? "SUCCESS" : "FAILED";
            var message = $"[{operation}] Adapter: {adapter} - {status}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }

            if (success)
                Info(message);
            else
                Error(message);
        }

        /// <summary>
        /// Logs a configuration change.
        /// </summary>
        public void LogConfigChange(string adapter, NetworkConfiguration oldConfig, NetworkConfiguration newConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[CONFIG CHANGE] Adapter: {adapter}");
            sb.AppendLine($"  Old: {oldConfig?.ToDisplayString() ?? "N/A"}");
            sb.AppendLine($"  New: {newConfig?.ToDisplayString() ?? "N/A"}");
            Info(sb.ToString());
        }

        /// <summary>
        /// Gets all log entries for today.
        /// </summary>
        public string GetTodaysLog()
        {
            var todayFile = GetLogFilePath(DateTime.Now);
            if (File.Exists(todayFile))
            {
                try
                {
                    // Need to read while writer might have it open
                    using (var fs = new FileStream(todayFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Exports log to a specified file.
        /// </summary>
        public Result ExportLog(string targetPath)
        {
            try
            {
                var content = GetTodaysLog();
                File.WriteAllText(targetPath, content, Encoding.UTF8);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.FromException(ex, "Failed to export log");
            }
        }

        /// <summary>
        /// Clears old log files beyond retention period.
        /// </summary>
        public void CleanupOldLogs(int retentionDays = 30)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-retentionDays);
                var logFiles = Directory.GetFiles(_logDirectory, "networkconfig_*.log");

                foreach (var file in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Skip files that can't be deleted
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void Write(LogLevel level, string levelTag, string message)
        {
            // Check if this level should be logged
            if (!ShouldLog(level))
                return;

            lock (_writeLock)
            {
                if (_disposed)
                    return;

                try
                {
                    EnsureWriterReady();

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logLine = $"[{timestamp}] [{levelTag}] {message}";

                    _currentWriter.WriteLine(logLine);
                    _currentFileSize += Encoding.UTF8.GetByteCount(logLine) + Environment.NewLine.Length;

                    // Flush immediately for errors
                    if (level == LogLevel.Minimal)
                    {
                        _currentWriter.Flush();
                    }

                    // Check for rotation
                    if (_currentFileSize > _maxFileSizeMb * 1024 * 1024)
                    {
                        RotateLogFile();
                    }
                }
                catch
                {
                    // Logging should never throw
                }
            }
        }

        private bool ShouldLog(LogLevel messageLevel)
        {
            // Minimal = only important, Normal = normal operations, Verbose = everything
            switch (_minLevel)
            {
                case LogLevel.Minimal:
                    return messageLevel == LogLevel.Minimal;
                case LogLevel.Normal:
                    return messageLevel != LogLevel.Verbose;
                case LogLevel.Verbose:
                    return true;
                default:
                    return true;
            }
        }

        private void EnsureWriterReady()
        {
            var expectedPath = GetLogFilePath(DateTime.Now);

            if (_currentFilePath != expectedPath || _currentWriter == null)
            {
                _currentWriter?.Dispose();
                _currentFilePath = expectedPath;
                _currentWriter = new StreamWriter(
                    new FileStream(_currentFilePath, FileMode.Append, FileAccess.Write, FileShare.Read),
                    Encoding.UTF8);
                _currentFileSize = new FileInfo(_currentFilePath).Length;
            }
        }

        private void RotateLogFile()
        {
            _currentWriter?.Dispose();
            _currentWriter = null;

            // Rename current file with timestamp
            var rotatedPath = _currentFilePath.Replace(".log", $"_{DateTime.Now:HHmmss}.log");
            try
            {
                File.Move(_currentFilePath, rotatedPath);
            }
            catch
            {
                // If rename fails, just start a new file
            }

            _currentFilePath = null;
            _currentFileSize = 0;
        }

        private string GetLogFilePath(DateTime date)
        {
            return Path.Combine(_logDirectory, $"networkconfig_{date:yyyyMMdd}.log");
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                // Fall back to temp directory
                _logDirectory.Replace(_logDirectory, Path.GetTempPath());
            }
        }

        private static string GetDefaultLogDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NetworkConfigApp", "logs");
        }

        public void Dispose()
        {
            lock (_writeLock)
            {
                if (!_disposed)
                {
                    _currentWriter?.Flush();
                    _currentWriter?.Dispose();
                    _currentWriter = null;
                    _disposed = true;
                }
            }
        }
    }
}
