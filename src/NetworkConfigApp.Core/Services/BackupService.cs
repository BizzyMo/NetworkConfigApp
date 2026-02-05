using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;
using Newtonsoft.Json;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Manages automatic and manual backups of network configurations.
    ///
    /// Algorithm: Backups are stored as timestamped JSON files in the backups directory.
    /// Each backup contains the adapter name, configuration, and metadata.
    ///
    /// Performance: File I/O is async. Cleanup runs periodically to remove old backups.
    ///
    /// Security: Backups are stored unencrypted (contains only network config, no credentials).
    /// Files are stored in user's AppData folder with appropriate permissions.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly string _backupsDirectory;
        private readonly int _defaultRetentionCount;
        private const string BACKUP_PREFIX = "backup_";
        private const string BACKUP_EXTENSION = ".json";

        public BackupService() : this(GetDefaultBackupsDirectory(), 10)
        {
        }

        public BackupService(string backupsDirectory, int defaultRetentionCount = 10)
        {
            _backupsDirectory = backupsDirectory ?? GetDefaultBackupsDirectory();
            _defaultRetentionCount = defaultRetentionCount > 0 ? defaultRetentionCount : 10;
            EnsureDirectoryExists();
        }

        public async Task<Result<BackupInfo>> CreateBackupAsync(
            string adapterName,
            NetworkConfiguration config,
            string description = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return Result<BackupInfo>.Failure("Adapter name is required", ErrorCode.InvalidInput);
            }

            if (config == null)
            {
                return Result<BackupInfo>.Failure("Configuration is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var id = Guid.NewGuid().ToString("N").Substring(0, 8);
                    var timestamp = DateTime.Now;
                    var fileName = $"{BACKUP_PREFIX}{timestamp:yyyyMMdd_HHmmss}_{id}{BACKUP_EXTENSION}";
                    var filePath = Path.Combine(_backupsDirectory, fileName);

                    var backupData = new BackupData
                    {
                        Id = id,
                        AdapterName = adapterName,
                        Configuration = config,
                        CreatedAt = timestamp,
                        Description = description ?? $"Backup before changes on {timestamp:g}"
                    };

                    var json = JsonConvert.SerializeObject(backupData, Formatting.Indented);
                    File.WriteAllText(filePath, json, Encoding.UTF8);

                    var backupInfo = new BackupInfo(
                        id,
                        adapterName,
                        config,
                        timestamp,
                        backupData.Description,
                        filePath);

                    LoggingService.Instance?.Info($"Created backup: {fileName}");

                    // Cleanup old backups in background
                    Task.Run(() => CleanupOldBackupsAsync(_defaultRetentionCount, CancellationToken.None));

                    return Result<BackupInfo>.Success(backupInfo);
                }
                catch (Exception ex)
                {
                    return Result<BackupInfo>.FromException(ex, "Failed to create backup");
                }
            }, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<BackupInfo>>> GetAllBackupsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var backups = new List<BackupInfo>();
                    var files = Directory.GetFiles(_backupsDirectory, $"{BACKUP_PREFIX}*{BACKUP_EXTENSION}");

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var backup = LoadBackupFromFile(file);
                            if (backup != null)
                            {
                                backups.Add(backup);
                            }
                        }
                        catch
                        {
                            // Skip corrupted files
                        }
                    }

                    return Result<IReadOnlyList<BackupInfo>>.Success(
                        backups.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly());
                }
                catch (Exception ex)
                {
                    return Result<IReadOnlyList<BackupInfo>>.FromException(ex, "Failed to load backups");
                }
            }, cancellationToken);
        }

        public async Task<Result<BackupInfo>> GetLatestBackupAsync(CancellationToken cancellationToken = default)
        {
            var allResult = await GetAllBackupsAsync(cancellationToken);
            if (!allResult.IsSuccess)
            {
                return Result<BackupInfo>.Failure(allResult.Error, allResult.ErrorCode);
            }

            var latest = allResult.Value.FirstOrDefault();
            if (latest == null)
            {
                return Result<BackupInfo>.Failure("No backups found", ErrorCode.IoError);
            }

            return Result<BackupInfo>.Success(latest);
        }

        public async Task<Result<IReadOnlyList<BackupInfo>>> GetBackupsForAdapterAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            var allResult = await GetAllBackupsAsync(cancellationToken);
            if (!allResult.IsSuccess)
            {
                return Result<IReadOnlyList<BackupInfo>>.Failure(allResult.Error, allResult.ErrorCode);
            }

            var filtered = allResult.Value
                .Where(b => b.AdapterName.Equals(adapterName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Result<IReadOnlyList<BackupInfo>>.Success(filtered.AsReadOnly());
        }

        public async Task<Result<NetworkConfiguration>> RestoreFromBackupAsync(
            string backupId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(backupId))
            {
                return Result<NetworkConfiguration>.Failure("Backup ID is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(_backupsDirectory, $"{BACKUP_PREFIX}*{backupId}*{BACKUP_EXTENSION}");

                    if (files.Length == 0)
                    {
                        return Result<NetworkConfiguration>.Failure($"Backup '{backupId}' not found", ErrorCode.IoError);
                    }

                    var backup = LoadBackupFromFile(files[0]);
                    if (backup == null)
                    {
                        return Result<NetworkConfiguration>.Failure("Failed to load backup", ErrorCode.IoError);
                    }

                    LoggingService.Instance?.Info($"Restored configuration from backup: {backupId}");
                    return Result<NetworkConfiguration>.Success(backup.Configuration);
                }
                catch (Exception ex)
                {
                    return Result<NetworkConfiguration>.FromException(ex, "Failed to restore backup");
                }
            }, cancellationToken);
        }

        public async Task<Result> DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(backupId))
            {
                return Result.Failure("Backup ID is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(_backupsDirectory, $"{BACKUP_PREFIX}*{backupId}*{BACKUP_EXTENSION}");

                    if (files.Length == 0)
                    {
                        return Result.Failure($"Backup '{backupId}' not found", ErrorCode.IoError);
                    }

                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }

                    LoggingService.Instance?.Info($"Deleted backup: {backupId}");
                    return Result.Success();
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to delete backup");
                }
            }, cancellationToken);
        }

        public async Task<Result> CleanupOldBackupsAsync(int retentionCount = 10, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(_backupsDirectory, $"{BACKUP_PREFIX}*{BACKUP_EXTENSION}")
                        .OrderByDescending(f => new FileInfo(f).CreationTime)
                        .ToList();

                    if (files.Count <= retentionCount)
                    {
                        return Result.Success();
                    }

                    var toDelete = files.Skip(retentionCount);
                    var deletedCount = 0;

                    foreach (var file in toDelete)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch
                        {
                            // Skip files that can't be deleted
                        }
                    }

                    if (deletedCount > 0)
                    {
                        LoggingService.Instance?.Info($"Cleaned up {deletedCount} old backups");
                    }

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to cleanup backups");
                }
            }, cancellationToken);
        }

        private BackupInfo LoadBackupFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var data = JsonConvert.DeserializeObject<BackupData>(json);

            if (data == null)
            {
                return null;
            }

            return new BackupInfo(
                data.Id,
                data.AdapterName,
                data.Configuration,
                data.CreatedAt,
                data.Description,
                filePath);
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_backupsDirectory))
                {
                    Directory.CreateDirectory(_backupsDirectory);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Error("Failed to create backups directory", ex);
            }
        }

        private static string GetDefaultBackupsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NetworkConfigApp", "backups");
        }

        /// <summary>
        /// Internal data structure for serialization.
        /// </summary>
        private class BackupData
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("adapterName")]
            public string AdapterName { get; set; }

            [JsonProperty("configuration")]
            public NetworkConfiguration Configuration { get; set; }

            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }
        }
    }
}
