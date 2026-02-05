using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Utilities;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Implements MAC address spoofing via Windows Registry modification.
    ///
    /// Algorithm: MAC address changes are made by modifying the NetworkAddress
    /// registry value for the adapter. This requires:
    /// 1. Finding the adapter's registry key
    /// 2. Writing the new MAC address
    /// 3. Disabling and re-enabling the adapter to apply changes
    ///
    /// Performance: Registry operations are fast, but adapter restart takes 2-5 seconds.
    ///
    /// Security: Requires administrator privileges. MAC spoofing is legal for personal use
    /// but may violate network policies. This feature is intended for legitimate use cases
    /// like privacy protection or network testing.
    /// </summary>
    public class MacService : IMacService
    {
        private readonly NetshExecutor _netsh;
        private readonly OuiDatabase _ouiDb;
        private const string NETWORK_ADAPTERS_KEY = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        public MacService()
        {
            _netsh = new NetshExecutor();
            _ouiDb = OuiDatabase.Instance;
        }

        public async Task<Result<string>> GetCurrentMacAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var adapter = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

                    if (adapter == null)
                    {
                        return Result<string>.Failure($"Adapter '{adapterName}' not found", ErrorCode.AdapterNotFound);
                    }

                    var mac = adapter.GetPhysicalAddress();
                    var macString = string.Join(":", mac.GetAddressBytes().Select(b => b.ToString("X2")));

                    return Result<string>.Success(macString);
                }
                catch (Exception ex)
                {
                    return Result<string>.FromException(ex, "Failed to get current MAC");
                }
            }, cancellationToken);
        }

        public async Task<Result<string>> GetPermanentMacAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var registryKey = FindAdapterRegistryKey(adapterName);
                    if (registryKey == null)
                    {
                        // Fall back to current MAC if registry key not found
                        return GetCurrentMacAsync(adapterName, cancellationToken).Result;
                    }

                    using (var key = Registry.LocalMachine.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            // Try to get original MAC from various sources
                            var origMac = key.GetValue("OriginalNetworkAddress") as string;
                            if (string.IsNullOrEmpty(origMac))
                            {
                                // If no original stored, current might be original
                                return GetCurrentMacAsync(adapterName, cancellationToken).Result;
                            }
                            return Result<string>.Success(FormatMacAddress(origMac));
                        }
                    }

                    return GetCurrentMacAsync(adapterName, cancellationToken).Result;
                }
                catch (Exception ex)
                {
                    return Result<string>.FromException(ex, "Failed to get permanent MAC");
                }
            }, cancellationToken);
        }

        public async Task<Result> ChangeMacAsync(string adapterName, string newMac, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);
            }

            if (!IsValidMac(newMac))
            {
                return Result.Failure("Invalid MAC address format", ErrorCode.InvalidInput);
            }

            return await Task.Run(async () =>
            {
                try
                {
                    var registryKey = FindAdapterRegistryKey(adapterName);
                    if (registryKey == null)
                    {
                        return Result.Failure($"Cannot find registry entry for adapter '{adapterName}'", ErrorCode.AdapterNotFound);
                    }

                    // Remove colons/dashes from MAC for registry
                    var cleanMac = newMac.Replace(":", "").Replace("-", "").ToUpper();

                    using (var key = Registry.LocalMachine.OpenSubKey(registryKey, true))
                    {
                        if (key == null)
                        {
                            return Result.Failure("Cannot open adapter registry key for writing", ErrorCode.RegistryError);
                        }

                        // Save original MAC if not already saved
                        var currentMacResult = await GetCurrentMacAsync(adapterName, cancellationToken);
                        if (currentMacResult.IsSuccess && key.GetValue("OriginalNetworkAddress") == null)
                        {
                            var currentClean = currentMacResult.Value.Replace(":", "").Replace("-", "");
                            key.SetValue("OriginalNetworkAddress", currentClean);
                        }

                        // Set new MAC address
                        key.SetValue("NetworkAddress", cleanMac);
                    }

                    LoggingService.Instance?.Info($"Set registry MAC for {adapterName} to {newMac}");

                    // Restart adapter to apply changes
                    var restartResult = await RestartAdapterAsync(adapterName, cancellationToken);
                    if (!restartResult.IsSuccess)
                    {
                        LoggingService.Instance?.Warning($"Adapter restart failed: {restartResult.Error}. Manual restart may be required.");
                        return Result.Failure(
                            $"MAC address set but adapter restart failed: {restartResult.Error}. Please disable and re-enable the adapter manually.",
                            ErrorCode.NetworkError);
                    }

                    // Verify the change
                    await Task.Delay(2000, cancellationToken); // Wait for adapter to come up
                    var verifyResult = await GetCurrentMacAsync(adapterName, cancellationToken);
                    if (verifyResult.IsSuccess)
                    {
                        var verifyClean = verifyResult.Value.Replace(":", "").Replace("-", "").ToUpper();
                        if (verifyClean == cleanMac)
                        {
                            LoggingService.Instance?.Info($"MAC address successfully changed to {newMac}");
                            return Result.Success();
                        }
                    }

                    return Result.Success(); // Registry was set, even if verification unclear
                }
                catch (UnauthorizedAccessException)
                {
                    return Result.Failure("Administrator privileges required to change MAC address", ErrorCode.AccessDenied);
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to change MAC address");
                }
            }, cancellationToken);
        }

        public async Task<Result> RestoreOriginalMacAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(async () =>
            {
                try
                {
                    var registryKey = FindAdapterRegistryKey(adapterName);
                    if (registryKey == null)
                    {
                        return Result.Failure($"Cannot find registry entry for adapter '{adapterName}'", ErrorCode.AdapterNotFound);
                    }

                    using (var key = Registry.LocalMachine.OpenSubKey(registryKey, true))
                    {
                        if (key == null)
                        {
                            return Result.Failure("Cannot open adapter registry key for writing", ErrorCode.RegistryError);
                        }

                        // Delete the NetworkAddress value to restore original
                        if (key.GetValue("NetworkAddress") != null)
                        {
                            key.DeleteValue("NetworkAddress");
                        }

                        // Clean up our saved original
                        if (key.GetValue("OriginalNetworkAddress") != null)
                        {
                            key.DeleteValue("OriginalNetworkAddress");
                        }
                    }

                    LoggingService.Instance?.Info($"Removed spoofed MAC for {adapterName}");

                    // Restart adapter to apply changes
                    var restartResult = await RestartAdapterAsync(adapterName, cancellationToken);
                    if (!restartResult.IsSuccess)
                    {
                        return Result.Failure(
                            $"Registry cleared but adapter restart failed: {restartResult.Error}. Please disable and re-enable the adapter manually.",
                            ErrorCode.NetworkError);
                    }

                    return Result.Success();
                }
                catch (UnauthorizedAccessException)
                {
                    return Result.Failure("Administrator privileges required to restore MAC address", ErrorCode.AccessDenied);
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to restore MAC address");
                }
            }, cancellationToken);
        }

        public string GenerateRandomMac(string manufacturer = null)
        {
            return _ouiDb.GenerateRandomMac(manufacturer);
        }

        public string GetManufacturer(string macAddress)
        {
            return _ouiDb.GetManufacturer(macAddress);
        }

        public IReadOnlyList<ManufacturerEntry> GetCommonManufacturers()
        {
            return _ouiDb.GetCommonManufacturers();
        }

        public bool IsValidMac(string macAddress)
        {
            return _ouiDb.IsValidMacFormat(macAddress);
        }

        public string NormalizeMac(string macAddress)
        {
            return _ouiDb.NormalizeMac(macAddress);
        }

        private async Task<Result> RestartAdapterAsync(string adapterName, CancellationToken cancellationToken)
        {
            // Disable adapter
            var disableResult = await _netsh.DisableAdapterAsync(adapterName, cancellationToken);
            if (!disableResult.IsSuccess)
            {
                return disableResult;
            }

            // Wait for disable to complete
            await Task.Delay(1000, cancellationToken);

            // Enable adapter
            var enableResult = await _netsh.EnableAdapterAsync(adapterName, cancellationToken);
            if (!enableResult.IsSuccess)
            {
                return enableResult;
            }

            // Wait for enable to complete
            await Task.Delay(2000, cancellationToken);

            return Result.Success();
        }

        private string FindAdapterRegistryKey(string adapterName)
        {
            try
            {
                using (var baseKey = Registry.LocalMachine.OpenSubKey(NETWORK_ADAPTERS_KEY))
                {
                    if (baseKey == null)
                    {
                        return null;
                    }

                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        // Skip non-numeric keys (like "Properties")
                        if (!int.TryParse(subKeyName, out _))
                        {
                            continue;
                        }

                        using (var subKey = baseKey.OpenSubKey(subKeyName))
                        {
                            if (subKey == null)
                            {
                                continue;
                            }

                            // Check if this is the adapter we're looking for
                            var driverDesc = subKey.GetValue("DriverDesc") as string;
                            var netCfgInstanceId = subKey.GetValue("NetCfgInstanceId") as string;

                            // Match by name or description
                            if (adapterName.Equals(driverDesc, StringComparison.OrdinalIgnoreCase))
                            {
                                return $@"{NETWORK_ADAPTERS_KEY}\{subKeyName}";
                            }

                            // Also check against NetworkInterface names
                            if (!string.IsNullOrEmpty(netCfgInstanceId))
                            {
                                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                                    .FirstOrDefault(n => n.Id == netCfgInstanceId);

                                if (adapter != null && adapter.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return $@"{NETWORK_ADAPTERS_KEY}\{subKeyName}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Warning($"Error searching for adapter registry key: {ex.Message}");
            }

            return null;
        }

        private string FormatMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12)
            {
                return mac;
            }

            return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
        }
    }
}
