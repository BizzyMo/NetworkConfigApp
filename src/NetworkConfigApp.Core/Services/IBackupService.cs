using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Interface for backing up and restoring network configurations.
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Creates a backup of the current configuration.
        /// </summary>
        Task<Result<BackupInfo>> CreateBackupAsync(
            string adapterName,
            NetworkConfiguration config,
            string description = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available backups.
        /// </summary>
        Task<Result<IReadOnlyList<BackupInfo>>> GetAllBackupsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most recent backup.
        /// </summary>
        Task<Result<BackupInfo>> GetLatestBackupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets backups for a specific adapter.
        /// </summary>
        Task<Result<IReadOnlyList<BackupInfo>>> GetBackupsForAdapterAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a configuration from a backup.
        /// </summary>
        Task<Result<NetworkConfiguration>> RestoreFromBackupAsync(
            string backupId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a backup.
        /// </summary>
        Task<Result> DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up old backups beyond retention count.
        /// </summary>
        Task<Result> CleanupOldBackupsAsync(int retentionCount = 10, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Information about a backup.
    /// </summary>
    public sealed class BackupInfo
    {
        public string Id { get; }
        public string AdapterName { get; }
        public NetworkConfiguration Configuration { get; }
        public DateTime CreatedAt { get; }
        public string Description { get; }
        public string FilePath { get; }

        public BackupInfo(
            string id,
            string adapterName,
            NetworkConfiguration configuration,
            DateTime createdAt,
            string description,
            string filePath)
        {
            Id = id;
            AdapterName = adapterName;
            Configuration = configuration;
            CreatedAt = createdAt;
            Description = description ?? string.Empty;
            FilePath = filePath;
        }

        public string GetDisplayName()
        {
            return $"{AdapterName} - {CreatedAt:g}";
        }

        public override string ToString()
        {
            return GetDisplayName();
        }
    }
}
