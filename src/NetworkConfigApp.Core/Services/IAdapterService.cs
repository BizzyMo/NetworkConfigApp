using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Interface for network adapter discovery and information retrieval.
    /// </summary>
    public interface IAdapterService
    {
        /// <summary>
        /// Gets all network adapters on the system.
        /// </summary>
        /// <param name="includeLoopback">Include loopback adapters</param>
        /// <param name="includeDisabled">Include disabled adapters</param>
        /// <returns>List of network adapters</returns>
        Task<Result<IReadOnlyList<NetworkAdapter>>> GetAllAdaptersAsync(
            bool includeLoopback = false,
            bool includeDisabled = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the currently active adapter (connected with gateway).
        /// </summary>
        /// <returns>Active adapter or null if none found</returns>
        Task<Result<NetworkAdapter>> GetActiveAdapterAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific adapter by name.
        /// </summary>
        /// <param name="adapterName">Name of the adapter</param>
        /// <returns>Adapter if found, null otherwise</returns>
        Task<Result<NetworkAdapter>> GetAdapterByNameAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes adapter information from the system.
        /// </summary>
        Task<Result> RefreshAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current network configuration for an adapter.
        /// </summary>
        Task<Result<NetworkConfiguration>> GetCurrentConfigurationAsync(
            string adapterName,
            CancellationToken cancellationToken = default);
    }
}
