using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Application settings model for persisting user preferences.
    /// Immutable data model with JSON serialization support.
    ///
    /// Algorithm: Simple key-value store with defaults.
    /// Data Structure: Read-only properties with factory methods.
    /// Security: No sensitive data; theme/logging preferences only.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Current theme (Light or Dark).</summary>
        [JsonProperty("theme")]
        [JsonConverter(typeof(StringEnumConverter))]
        public AppTheme Theme { get; }

        /// <summary>Logging verbosity level.</summary>
        [JsonProperty("logLevel")]
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; }

        /// <summary>Auto-backup before making changes.</summary>
        [JsonProperty("autoBackup")]
        public bool AutoBackup { get; }

        /// <summary>Flush DNS after applying changes.</summary>
        [JsonProperty("flushDnsAfterChanges")]
        public bool FlushDnsAfterChanges { get; }

        /// <summary>Test connectivity after applying changes.</summary>
        [JsonProperty("testAfterApply")]
        public bool TestAfterApply { get; }

        /// <summary>Show in system tray when minimized.</summary>
        [JsonProperty("minimizeToTray")]
        public bool MinimizeToTray { get; }

        /// <summary>Start minimized to system tray.</summary>
        [JsonProperty("startMinimized")]
        public bool StartMinimized { get; }

        /// <summary>Last selected adapter name (for restoring state).</summary>
        [JsonProperty("lastAdapterName")]
        public string LastAdapterName { get; }

        /// <summary>Maximum log file size in MB before rotation.</summary>
        [JsonProperty("maxLogSizeMb")]
        public int MaxLogSizeMb { get; }

        /// <summary>Number of backup files to retain.</summary>
        [JsonProperty("backupRetentionCount")]
        public int BackupRetentionCount { get; }

        /// <summary>Enable portable mode (store in exe directory).</summary>
        [JsonProperty("portableMode")]
        public bool PortableMode { get; }

        /// <summary>Default ping timeout in milliseconds.</summary>
        [JsonProperty("pingTimeoutMs")]
        public int PingTimeoutMs { get; }

        /// <summary>Default DNS timeout in milliseconds.</summary>
        [JsonProperty("dnsTimeoutMs")]
        public int DnsTimeoutMs { get; }

        [JsonConstructor]
        private AppSettings(
            AppTheme theme,
            LogLevel logLevel,
            bool autoBackup,
            bool flushDnsAfterChanges,
            bool testAfterApply,
            bool minimizeToTray,
            bool startMinimized,
            string lastAdapterName,
            int maxLogSizeMb,
            int backupRetentionCount,
            bool portableMode,
            int pingTimeoutMs,
            int dnsTimeoutMs)
        {
            Theme = theme;
            LogLevel = logLevel;
            AutoBackup = autoBackup;
            FlushDnsAfterChanges = flushDnsAfterChanges;
            TestAfterApply = testAfterApply;
            MinimizeToTray = minimizeToTray;
            StartMinimized = startMinimized;
            LastAdapterName = lastAdapterName ?? string.Empty;
            MaxLogSizeMb = maxLogSizeMb > 0 ? maxLogSizeMb : 10;
            BackupRetentionCount = backupRetentionCount > 0 ? backupRetentionCount : 10;
            PortableMode = portableMode;
            PingTimeoutMs = pingTimeoutMs > 0 ? pingTimeoutMs : 3000;
            DnsTimeoutMs = dnsTimeoutMs > 0 ? dnsTimeoutMs : 5000;
        }

        /// <summary>Creates default settings.</summary>
        public static AppSettings Default()
        {
            return new AppSettings(
                AppTheme.Light,
                LogLevel.Normal,
                autoBackup: true,
                flushDnsAfterChanges: false,
                testAfterApply: true,
                minimizeToTray: true,
                startMinimized: false,
                lastAdapterName: string.Empty,
                maxLogSizeMb: 10,
                backupRetentionCount: 10,
                portableMode: false,
                pingTimeoutMs: 3000,
                dnsTimeoutMs: 5000);
        }

        public AppSettings WithTheme(AppTheme theme)
        {
            return new AppSettings(theme, LogLevel, AutoBackup, FlushDnsAfterChanges,
                TestAfterApply, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithLogLevel(LogLevel level)
        {
            return new AppSettings(Theme, level, AutoBackup, FlushDnsAfterChanges,
                TestAfterApply, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithAutoBackup(bool enabled)
        {
            return new AppSettings(Theme, LogLevel, enabled, FlushDnsAfterChanges,
                TestAfterApply, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithFlushDnsAfterChanges(bool enabled)
        {
            return new AppSettings(Theme, LogLevel, AutoBackup, enabled,
                TestAfterApply, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithTestAfterApply(bool enabled)
        {
            return new AppSettings(Theme, LogLevel, AutoBackup, FlushDnsAfterChanges,
                enabled, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithMinimizeToTray(bool enabled)
        {
            return new AppSettings(Theme, LogLevel, AutoBackup, FlushDnsAfterChanges,
                TestAfterApply, enabled, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithLastAdapterName(string name)
        {
            return new AppSettings(Theme, LogLevel, AutoBackup, FlushDnsAfterChanges,
                TestAfterApply, MinimizeToTray, StartMinimized, name ?? string.Empty,
                MaxLogSizeMb, BackupRetentionCount, PortableMode, PingTimeoutMs, DnsTimeoutMs);
        }

        public AppSettings WithPortableMode(bool enabled)
        {
            return new AppSettings(Theme, LogLevel, AutoBackup, FlushDnsAfterChanges,
                TestAfterApply, MinimizeToTray, StartMinimized, LastAdapterName,
                MaxLogSizeMb, BackupRetentionCount, enabled, PingTimeoutMs, DnsTimeoutMs);
        }
    }

    /// <summary>Application theme options.</summary>
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>Logging verbosity levels.</summary>
    public enum LogLevel
    {
        Minimal,
        Normal,
        Verbose
    }
}
