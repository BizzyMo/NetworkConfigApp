using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Interface for managing network configuration presets.
    /// </summary>
    public interface IPresetService
    {
        /// <summary>
        /// Gets all saved presets.
        /// </summary>
        Task<Result<IReadOnlyList<Preset>>> GetAllPresetsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a preset by name.
        /// </summary>
        Task<Result<Preset>> GetPresetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a new preset or updates an existing one.
        /// </summary>
        Task<Result<Preset>> SavePresetAsync(Preset preset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a preset by name.
        /// </summary>
        Task<Result> DeletePresetAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports presets from a file.
        /// </summary>
        Task<Result<IReadOnlyList<Preset>>> ImportPresetsAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports presets to a file.
        /// </summary>
        Task<Result> ExportPresetsAsync(string filePath, IEnumerable<Preset> presets = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a preset from the current adapter configuration.
        /// </summary>
        Task<Result<Preset>> CreateFromCurrentAsync(
            string name,
            string adapterName,
            NetworkConfiguration config,
            bool encrypt = false,
            CancellationToken cancellationToken = default);
    }
}
