using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Utilities;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Interface for MAC address operations.
    /// </summary>
    public interface IMacService
    {
        /// <summary>
        /// Gets the current MAC address of an adapter.
        /// </summary>
        Task<Result<string>> GetCurrentMacAsync(string adapterName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the permanent (hardware) MAC address of an adapter.
        /// </summary>
        Task<Result<string>> GetPermanentMacAsync(string adapterName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes the MAC address of an adapter.
        /// </summary>
        Task<Result> ChangeMacAsync(string adapterName, string newMac, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores the original MAC address of an adapter.
        /// </summary>
        Task<Result> RestoreOriginalMacAsync(string adapterName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a random MAC address.
        /// </summary>
        string GenerateRandomMac(string manufacturer = null);

        /// <summary>
        /// Gets the manufacturer name for a MAC address.
        /// </summary>
        string GetManufacturer(string macAddress);

        /// <summary>
        /// Gets common manufacturers for selection.
        /// </summary>
        IReadOnlyList<ManufacturerEntry> GetCommonManufacturers();

        /// <summary>
        /// Validates a MAC address format.
        /// </summary>
        bool IsValidMac(string macAddress);

        /// <summary>
        /// Normalizes MAC address to colon-separated format.
        /// </summary>
        string NormalizeMac(string macAddress);
    }
}
