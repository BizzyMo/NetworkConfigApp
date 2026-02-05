using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;
using Newtonsoft.Json;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Manages application settings persistence.
    ///
    /// Algorithm: Settings are stored as JSON in the app data directory.
    /// Singleton pattern with lazy loading and automatic persistence.
    ///
    /// Performance: Settings are cached in memory after first load.
    /// Saves are debounced to avoid excessive file I/O.
    ///
    /// Security: Settings contain only non-sensitive application preferences.
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsPath;
        private AppSettings _currentSettings;
        private readonly object _settingsLock = new object();
        private DateTime _lastSaveRequest;
        private bool _pendingSave;

        private static SettingsService _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes or reinitializes with custom path.
        /// </summary>
        public static void Initialize(string settingsPath)
        {
            lock (_instanceLock)
            {
                _instance = new SettingsService(settingsPath);
            }
        }

        public SettingsService() : this(GetDefaultSettingsPath())
        {
        }

        public SettingsService(string settingsPath)
        {
            _settingsPath = settingsPath ?? GetDefaultSettingsPath();
            EnsureDirectoryExists();
        }

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public AppSettings GetSettings()
        {
            lock (_settingsLock)
            {
                if (_currentSettings == null)
                {
                    _currentSettings = LoadSettings();
                }
                return _currentSettings;
            }
        }

        /// <summary>
        /// Updates and saves settings.
        /// </summary>
        public Result UpdateSettings(AppSettings newSettings)
        {
            if (newSettings == null)
            {
                return Result.Failure("Settings cannot be null", ErrorCode.InvalidInput);
            }

            lock (_settingsLock)
            {
                _currentSettings = newSettings;
                _lastSaveRequest = DateTime.Now;
                _pendingSave = true;
            }

            // Debounced save (500ms)
            Task.Delay(500).ContinueWith(_ => SaveIfPending());

            return Result.Success();
        }

        /// <summary>
        /// Updates a specific setting using a transform function.
        /// </summary>
        public Result UpdateSetting(Func<AppSettings, AppSettings> transform)
        {
            var current = GetSettings();
            var updated = transform(current);
            return UpdateSettings(updated);
        }

        /// <summary>
        /// Forces an immediate save.
        /// </summary>
        public Result SaveNow()
        {
            lock (_settingsLock)
            {
                _pendingSave = false;
                return SaveSettingsInternal(_currentSettings ?? AppSettings.Default());
            }
        }

        /// <summary>
        /// Resets settings to defaults.
        /// </summary>
        public Result ResetToDefaults()
        {
            return UpdateSettings(AppSettings.Default());
        }

        /// <summary>
        /// Gets the storage directory based on portable mode.
        /// </summary>
        public string GetStorageDirectory()
        {
            var settings = GetSettings();
            if (settings.PortableMode)
            {
                return GetPortableDirectory();
            }
            return GetAppDataDirectory();
        }

        /// <summary>
        /// Checks if running in portable mode.
        /// </summary>
        public bool IsPortableMode()
        {
            // Check for portable marker file next to executable
            var portableMarker = Path.Combine(GetExecutableDirectory(), "portable.txt");
            return File.Exists(portableMarker);
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        // Check for portable mode override
                        if (IsPortableMode() && !settings.PortableMode)
                        {
                            settings = settings.WithPortableMode(true);
                        }
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Warning($"Failed to load settings: {ex.Message}");
            }

            return AppSettings.Default().WithPortableMode(IsPortableMode());
        }

        private void SaveIfPending()
        {
            AppSettings toSave = null;

            lock (_settingsLock)
            {
                if (_pendingSave && DateTime.Now - _lastSaveRequest >= TimeSpan.FromMilliseconds(500))
                {
                    _pendingSave = false;
                    toSave = _currentSettings;
                }
            }

            if (toSave != null)
            {
                SaveSettingsInternal(toSave);
            }
        }

        private Result SaveSettingsInternal(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
                return Result.Success();
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Error("Failed to save settings", ex);
                return Result.FromException(ex, "Failed to save settings");
            }
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch
            {
                // Ignore directory creation errors
            }
        }

        private static string GetDefaultSettingsPath()
        {
            return Path.Combine(GetAppDataDirectory(), "settings.json");
        }

        private static string GetAppDataDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NetworkConfigApp");
        }

        private static string GetPortableDirectory()
        {
            return GetExecutableDirectory();
        }

        private static string GetExecutableDirectory()
        {
            var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;
        }
    }
}
