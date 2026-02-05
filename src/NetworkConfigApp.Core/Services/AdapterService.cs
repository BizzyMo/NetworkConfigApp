using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Implementation of adapter discovery using System.Net.NetworkInformation.
    ///
    /// Algorithm: Queries Windows network stack for adapter information.
    /// Uses NetworkInterface.GetAllNetworkInterfaces() which wraps Win32 APIs.
    ///
    /// Performance: Single system call to enumerate, O(n) iteration.
    /// Caches results until RefreshAsync is called.
    ///
    /// Security: Read-only operations, no elevated privileges required.
    /// </summary>
    public class AdapterService : IAdapterService
    {
        private List<NetworkAdapter> _cachedAdapters;
        private DateTime _lastRefresh;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);
        private readonly object _cacheLock = new object();

        public async Task<Result<IReadOnlyList<NetworkAdapter>>> GetAllAdaptersAsync(
            bool includeLoopback = false,
            bool includeDisabled = true,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var adapters = GetAdaptersInternal(includeLoopback, includeDisabled);
                    return Result<IReadOnlyList<NetworkAdapter>>.Success(adapters.AsReadOnly());
                }
                catch (NetworkInformationException ex)
                {
                    return Result<IReadOnlyList<NetworkAdapter>>.Failure(
                        $"Failed to enumerate network adapters: {ex.Message}",
                        ErrorCode.NetworkError);
                }
                catch (Exception ex)
                {
                    return Result<IReadOnlyList<NetworkAdapter>>.FromException(ex, "Failed to get adapters");
                }
            }, cancellationToken);
        }

        public async Task<Result<NetworkAdapter>> GetActiveAdapterAsync(CancellationToken cancellationToken = default)
        {
            var adaptersResult = await GetAllAdaptersAsync(false, false, cancellationToken);
            if (!adaptersResult.IsSuccess)
            {
                return Result<NetworkAdapter>.Failure(adaptersResult.Error, adaptersResult.ErrorCode);
            }

            // Find adapter that is:
            // 1. Connected (OperationalStatus.Up)
            // 2. Has an IPv4 address
            // 3. Has a gateway
            var activeAdapter = adaptersResult.Value
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.InterfaceType == NetworkInterfaceType.Ethernet) // Prefer Ethernet
                .ThenByDescending(a => a.Speed)
                .FirstOrDefault();

            if (activeAdapter == null)
            {
                return Result<NetworkAdapter>.Failure(
                    "No active network adapter found",
                    ErrorCode.AdapterNotFound);
            }

            return Result<NetworkAdapter>.Success(activeAdapter);
        }

        public async Task<Result<NetworkAdapter>> GetAdapterByNameAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return Result<NetworkAdapter>.Failure("Adapter name is required", ErrorCode.InvalidInput);
            }

            var adaptersResult = await GetAllAdaptersAsync(true, true, cancellationToken);
            if (!adaptersResult.IsSuccess)
            {
                return Result<NetworkAdapter>.Failure(adaptersResult.Error, adaptersResult.ErrorCode);
            }

            var adapter = adaptersResult.Value
                .FirstOrDefault(a => a.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

            if (adapter == null)
            {
                return Result<NetworkAdapter>.Failure(
                    $"Adapter '{adapterName}' not found",
                    ErrorCode.AdapterNotFound);
            }

            return Result<NetworkAdapter>.Success(adapter);
        }

        public Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_cacheLock)
                    {
                        _cachedAdapters = null;
                        _lastRefresh = DateTime.MinValue;
                    }
                    return Result.Success();
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to refresh adapters");
                }
            }, cancellationToken);
        }

        public async Task<Result<NetworkConfiguration>> GetCurrentConfigurationAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            var adapterResult = await GetAdapterByNameAsync(adapterName, cancellationToken);
            if (!adapterResult.IsSuccess)
            {
                return Result<NetworkConfiguration>.Failure(adapterResult.Error, adapterResult.ErrorCode);
            }

            return Result<NetworkConfiguration>.Success(adapterResult.Value.CurrentConfiguration);
        }

        private List<NetworkAdapter> GetAdaptersInternal(bool includeLoopback, bool includeDisabled)
        {
            // Check cache
            lock (_cacheLock)
            {
                if (_cachedAdapters != null && DateTime.Now - _lastRefresh < _cacheExpiry)
                {
                    return FilterAdapters(_cachedAdapters, includeLoopback, includeDisabled);
                }
            }

            var adapters = new List<NetworkAdapter>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            int id = 0;

            foreach (var ni in interfaces)
            {
                try
                {
                    // Skip tunnel/loopback types unless requested
                    if (!includeLoopback && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    // Skip certain virtual adapter types
                    if (IsVirtualAdapter(ni))
                        continue;

                    var ipProps = ni.GetIPProperties();
                    var ipv4Props = GetIPv4Properties(ipProps);

                    // Get IPv4 address info
                    var ipv4Address = GetIPv4Address(ipProps);
                    var gateway = GetIPv4Gateway(ipProps);
                    var (dns1, dns2) = GetDnsServers(ipProps);

                    // Determine if DHCP is enabled
                    var isDhcp = ipv4Props?.IsDhcpEnabled ?? false;

                    // Determine if this is the "active" adapter
                    var isActive = ni.OperationalStatus == OperationalStatus.Up &&
                                   !string.IsNullOrEmpty(ipv4Address) &&
                                   !string.IsNullOrEmpty(gateway);

                    // Create configuration from current state
                    var config = NetworkConfiguration.FromAdapterState(
                        ipv4Address,
                        GetSubnetMask(ipProps),
                        gateway,
                        dns1,
                        dns2,
                        isDhcp);

                    var adapter = NetworkAdapter.Create(
                        id++,
                        ni.Name,
                        ni.Description,
                        ni.NetworkInterfaceType,
                        ni.OperationalStatus,
                        FormatMacAddress(ni.GetPhysicalAddress()),
                        ni.Speed,
                        isDhcp,
                        isActive,
                        config);

                    adapters.Add(adapter);
                }
                catch (Exception)
                {
                    // Skip adapters that throw exceptions (some virtual adapters do)
                    continue;
                }
            }

            // Update cache
            lock (_cacheLock)
            {
                _cachedAdapters = adapters;
                _lastRefresh = DateTime.Now;
            }

            return FilterAdapters(adapters, includeLoopback, includeDisabled);
        }

        private List<NetworkAdapter> FilterAdapters(List<NetworkAdapter> adapters, bool includeLoopback, bool includeDisabled)
        {
            return adapters.Where(a =>
            {
                if (!includeLoopback && a.InterfaceType == NetworkInterfaceType.Loopback)
                    return false;

                if (!includeDisabled && a.Status != OperationalStatus.Up)
                    return false;

                return true;
            }).ToList();
        }

        private bool IsVirtualAdapter(NetworkInterface ni)
        {
            var name = ni.Name.ToLowerInvariant();
            var desc = ni.Description.ToLowerInvariant();

            // Skip common virtual adapter patterns
            if (name.Contains("vmware") || desc.Contains("vmware"))
                return true;
            if (name.Contains("virtualbox") || desc.Contains("virtualbox"))
                return true;
            if (name.Contains("hyper-v") || desc.Contains("hyper-v"))
                return true;
            if (desc.Contains("virtual ethernet"))
                return true;
            if (desc.Contains("bluetooth"))
                return false; // Keep Bluetooth adapters, they're real
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                return true;

            return false;
        }

        private IPv4InterfaceProperties GetIPv4Properties(IPInterfaceProperties ipProps)
        {
            try
            {
                return ipProps.GetIPv4Properties();
            }
            catch
            {
                return null;
            }
        }

        private string GetIPv4Address(IPInterfaceProperties ipProps)
        {
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return addr.Address.ToString();
                }
            }
            return string.Empty;
        }

        private string GetSubnetMask(IPInterfaceProperties ipProps)
        {
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    try
                    {
                        return addr.IPv4Mask?.ToString() ?? "255.255.255.0";
                    }
                    catch
                    {
                        return "255.255.255.0";
                    }
                }
            }
            return string.Empty;
        }

        private string GetIPv4Gateway(IPInterfaceProperties ipProps)
        {
            foreach (var gateway in ipProps.GatewayAddresses)
            {
                if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return gateway.Address.ToString();
                }
            }
            return string.Empty;
        }

        private (string dns1, string dns2) GetDnsServers(IPInterfaceProperties ipProps)
        {
            var dns1 = string.Empty;
            var dns2 = string.Empty;
            var count = 0;

            foreach (var dns in ipProps.DnsAddresses)
            {
                if (dns.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (count == 0)
                        dns1 = dns.ToString();
                    else if (count == 1)
                        dns2 = dns.ToString();
                    else
                        break;

                    count++;
                }
            }

            return (dns1, dns2);
        }

        private string FormatMacAddress(PhysicalAddress mac)
        {
            var bytes = mac.GetAddressBytes();
            if (bytes.Length == 0)
                return string.Empty;

            return string.Join(":", bytes.Select(b => b.ToString("X2")));
        }
    }
}
