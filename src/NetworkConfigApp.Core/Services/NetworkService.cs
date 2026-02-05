using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Utilities;
using NetworkConfigApp.Core.Validators;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Implementation of network configuration operations.
    ///
    /// Algorithm: Uses NetshExecutor for configuration changes (netsh/ipconfig commands)
    /// and System.Net.NetworkInformation for read operations (ping, DNS).
    ///
    /// Performance: Async throughout to prevent UI blocking.
    /// Configuration operations are inherently slow (OS network stack reconfiguration).
    ///
    /// Security: Requires administrator privileges for configuration changes.
    /// Input is validated before execution to prevent command injection.
    /// </summary>
    public class NetworkService : INetworkService
    {
        private readonly NetshExecutor _netsh;
        private const string DEFAULT_INTERNET_TEST_HOST = "8.8.8.8"; // Google DNS
        private const string DEFAULT_DNS_TEST_HOST = "www.google.com";

        public NetworkService()
        {
            _netsh = new NetshExecutor();
        }

        public NetworkService(int commandTimeoutMs)
        {
            _netsh = new NetshExecutor(commandTimeoutMs);
        }

        public async Task<Result> ApplyStaticConfigurationAsync(
            string adapterName,
            NetworkConfiguration config,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            if (config == null)
                return Result.Failure("Configuration is required", ErrorCode.InvalidInput);

            // Validate IP address
            var ipResult = IpAddressValidator.ValidateForStatic(config.IpAddress);
            if (!ipResult.IsValid)
                return Result.Failure($"Invalid IP address: {ipResult.Message}", ErrorCode.InvalidInput);

            // Validate subnet mask
            var maskResult = SubnetValidator.Validate(config.SubnetMask);
            if (!maskResult.IsValid)
                return Result.Failure($"Invalid subnet mask: {maskResult.Message}", ErrorCode.InvalidInput);

            // Validate gateway (optional)
            if (!string.IsNullOrEmpty(config.Gateway))
            {
                var gwResult = IpAddressValidator.ValidateForGateway(config.Gateway);
                if (!gwResult.IsValid)
                    return Result.Failure($"Invalid gateway: {gwResult.Message}", ErrorCode.InvalidInput);

                // Check gateway is in same subnet
                if (!IpAddressValidator.IsInSameSubnet(config.IpAddress, config.Gateway, config.SubnetMask))
                {
                    return Result.Failure(
                        "Gateway must be in the same subnet as the IP address",
                        ErrorCode.ConfigurationError);
                }
            }

            // Validate DNS servers (optional)
            if (!string.IsNullOrEmpty(config.Dns1))
            {
                var dnsResult = IpAddressValidator.ValidateForDns(config.Dns1);
                if (!dnsResult.IsValid)
                    return Result.Failure($"Invalid primary DNS: {dnsResult.Message}", ErrorCode.InvalidInput);
            }

            if (!string.IsNullOrEmpty(config.Dns2))
            {
                var dnsResult = IpAddressValidator.ValidateForDns(config.Dns2);
                if (!dnsResult.IsValid)
                    return Result.Failure($"Invalid secondary DNS: {dnsResult.Message}", ErrorCode.InvalidInput);
            }

            // Apply IP configuration
            var ipApplyResult = await _netsh.SetStaticIpAsync(
                adapterName,
                config.IpAddress,
                config.SubnetMask,
                config.Gateway,
                cancellationToken);

            if (!ipApplyResult.IsSuccess)
                return ipApplyResult;

            // Apply DNS configuration
            if (!string.IsNullOrEmpty(config.Dns1))
            {
                var dnsApplyResult = await _netsh.SetDnsAsync(
                    adapterName,
                    config.Dns1,
                    config.Dns2,
                    cancellationToken);

                if (!dnsApplyResult.IsSuccess)
                    return dnsApplyResult;
            }

            return Result.Success();
        }

        public async Task<Result> SetDhcpAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            return await _netsh.SetDhcpAsync(adapterName, cancellationToken);
        }

        public async Task<Result> ReleaseRenewAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            return await _netsh.ReleaseRenewAsync(adapterName, cancellationToken);
        }

        public async Task<Result> ReleaseAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            return await _netsh.ReleaseDhcpAsync(adapterName, cancellationToken);
        }

        public async Task<Result> RenewAsync(
            string adapterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            return await _netsh.RenewDhcpAsync(adapterName, cancellationToken);
        }

        public async Task<Result> FlushDnsAsync(CancellationToken cancellationToken = default)
        {
            return await _netsh.FlushDnsAsync(cancellationToken);
        }

        public async Task<DiagnosticResult> PingAsync(
            string host,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(host))
            {
                return DiagnosticResult.PingFailure("", "Host is required");
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        var reply = ping.Send(host, timeoutMs);

                        if (reply.Status == IPStatus.Success)
                        {
                            return DiagnosticResult.PingSuccess(
                                reply.Address?.ToString() ?? host,
                                reply.RoundtripTime,
                                reply.Options?.Ttl ?? 0);
                        }

                        return DiagnosticResult.PingFailure(host, GetPingStatusMessage(reply.Status));
                    }
                }
                catch (PingException ex)
                {
                    return DiagnosticResult.PingFailure(host, ex.InnerException?.Message ?? ex.Message);
                }
                catch (Exception ex)
                {
                    return DiagnosticResult.PingFailure(host, ex.Message);
                }
            }, cancellationToken);
        }

        public async Task<DiagnosticResult> TraceRouteAsync(
            string host,
            int maxHops = 30,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(host))
            {
                return DiagnosticResult.TraceRoute(host, false, new List<TraceHop>());
            }

            return await Task.Run(() =>
            {
                var hops = new List<TraceHop>();
                bool reachedDestination = false;

                try
                {
                    // Resolve hostname first
                    IPAddress destAddress;
                    try
                    {
                        var addresses = Dns.GetHostAddresses(host);
                        destAddress = addresses.Length > 0 ? addresses[0] : null;
                        if (destAddress == null)
                        {
                            return DiagnosticResult.TraceRoute(host, false, hops);
                        }
                    }
                    catch
                    {
                        // Try parsing as IP
                        if (!IPAddress.TryParse(host, out destAddress))
                        {
                            return DiagnosticResult.TraceRoute(host, false, hops);
                        }
                    }

                    using (var ping = new Ping())
                    {
                        var buffer = new byte[32];
                        new Random().NextBytes(buffer);

                        for (int ttl = 1; ttl <= maxHops && !cancellationToken.IsCancellationRequested; ttl++)
                        {
                            var options = new PingOptions(ttl, true);
                            var rtts = new List<long>();
                            string hopAddress = "*";
                            bool responsive = false;

                            // Send 3 probes per hop
                            for (int probe = 0; probe < 3; probe++)
                            {
                                try
                                {
                                    var reply = ping.Send(destAddress, timeoutMs, buffer, options);

                                    if (reply.Status == IPStatus.Success ||
                                        reply.Status == IPStatus.TtlExpired)
                                    {
                                        rtts.Add(reply.RoundtripTime);
                                        hopAddress = reply.Address?.ToString() ?? "*";
                                        responsive = true;

                                        if (reply.Status == IPStatus.Success)
                                        {
                                            reachedDestination = true;
                                        }
                                    }
                                    else
                                    {
                                        rtts.Add(-1);
                                    }
                                }
                                catch
                                {
                                    rtts.Add(-1);
                                }
                            }

                            // Try to resolve hostname
                            string hostname = hopAddress;
                            if (responsive && hopAddress != "*")
                            {
                                try
                                {
                                    var hostEntry = Dns.GetHostEntry(hopAddress);
                                    if (!string.IsNullOrEmpty(hostEntry.HostName))
                                    {
                                        hostname = hostEntry.HostName;
                                    }
                                }
                                catch
                                {
                                    hostname = hopAddress;
                                }
                            }

                            hops.Add(new TraceHop(ttl, hopAddress, hostname, rtts, responsive));

                            if (reachedDestination)
                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    // Return what we have
                }

                return DiagnosticResult.TraceRoute(host, reachedDestination, hops);
            }, cancellationToken);
        }

        public async Task<DiagnosticResult> TestDnsAsync(
            string hostname,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(hostname))
            {
                return DiagnosticResult.DnsResolution(hostname, false, "", -1);
            }

            return await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var addresses = Dns.GetHostAddresses(hostname);
                    stopwatch.Stop();

                    if (addresses.Length > 0)
                    {
                        return DiagnosticResult.DnsResolution(
                            hostname,
                            true,
                            addresses[0].ToString(),
                            stopwatch.ElapsedMilliseconds);
                    }

                    return DiagnosticResult.DnsResolution(hostname, false, "", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception)
                {
                    stopwatch.Stop();
                    return DiagnosticResult.DnsResolution(hostname, false, "", stopwatch.ElapsedMilliseconds);
                }
            }, cancellationToken);
        }

        public async Task<Result<ConnectivityTestResult>> TestConnectivityAsync(
            string gateway,
            string dns,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Test gateway
                var gatewayResult = !string.IsNullOrEmpty(gateway)
                    ? await PingAsync(gateway, 3000, cancellationToken)
                    : DiagnosticResult.PingFailure("", "No gateway");

                // Test DNS server
                var dnsTarget = !string.IsNullOrEmpty(dns) ? dns : DEFAULT_INTERNET_TEST_HOST;
                var dnsResult = await PingAsync(dnsTarget, 3000, cancellationToken);

                // Test internet (Google DNS)
                var internetResult = await PingAsync(DEFAULT_INTERNET_TEST_HOST, 3000, cancellationToken);

                var result = new ConnectivityTestResult(
                    gatewayResult.IsSuccess,
                    dnsResult.IsSuccess,
                    internetResult.IsSuccess,
                    gatewayResult.RoundTripTimeMs,
                    dnsResult.RoundTripTimeMs,
                    internetResult.RoundTripTimeMs);

                return Result<ConnectivityTestResult>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<ConnectivityTestResult>.FromException(ex, "Connectivity test failed");
            }
        }

        private string GetPingStatusMessage(IPStatus status)
        {
            switch (status)
            {
                case IPStatus.Success:
                    return "Success";
                case IPStatus.TimedOut:
                    return "Request timed out";
                case IPStatus.DestinationHostUnreachable:
                    return "Destination host unreachable";
                case IPStatus.DestinationNetworkUnreachable:
                    return "Destination network unreachable";
                case IPStatus.DestinationPortUnreachable:
                    return "Destination port unreachable";
                case IPStatus.NoResources:
                    return "No resources available";
                case IPStatus.BadOption:
                    return "Bad option";
                case IPStatus.HardwareError:
                    return "Hardware error";
                case IPStatus.PacketTooBig:
                    return "Packet too big";
                case IPStatus.TtlExpired:
                    return "TTL expired in transit";
                case IPStatus.TtlReassemblyTimeExceeded:
                    return "TTL reassembly time exceeded";
                case IPStatus.ParameterProblem:
                    return "Parameter problem";
                case IPStatus.SourceQuench:
                    return "Source quench";
                case IPStatus.BadDestination:
                    return "Bad destination";
                case IPStatus.DestinationUnreachable:
                    return "Destination unreachable";
                case IPStatus.Unknown:
                default:
                    return $"Unknown error ({status})";
            }
        }
    }
}
