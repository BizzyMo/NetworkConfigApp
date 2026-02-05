using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Interface for network configuration operations.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Applies a static IP configuration to an adapter.
        /// </summary>
        Task<Result> ApplyStaticConfigurationAsync(
            string adapterName,
            NetworkConfiguration config,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets an adapter to use DHCP for IP and DNS.
        /// </summary>
        Task<Result> SetDhcpAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases and renews the DHCP lease.
        /// </summary>
        Task<Result> ReleaseRenewAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the DHCP lease only.
        /// </summary>
        Task<Result> ReleaseAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Renews the DHCP lease only.
        /// </summary>
        Task<Result> RenewAsync(
            string adapterName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes the DNS resolver cache.
        /// </summary>
        Task<Result> FlushDnsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests connectivity to a host.
        /// </summary>
        Task<DiagnosticResult> PingAsync(
            string host,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a traceroute to a host.
        /// </summary>
        Task<DiagnosticResult> TraceRouteAsync(
            string host,
            int maxHops = 30,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests DNS resolution for a hostname.
        /// </summary>
        Task<DiagnosticResult> TestDnsAsync(
            string hostname,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a comprehensive connectivity test.
        /// </summary>
        Task<Result<ConnectivityTestResult>> TestConnectivityAsync(
            string gateway,
            string dns,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of comprehensive connectivity test.
    /// </summary>
    public sealed class ConnectivityTestResult
    {
        public bool GatewayReachable { get; }
        public bool DnsReachable { get; }
        public bool InternetReachable { get; }
        public long GatewayLatencyMs { get; }
        public long DnsLatencyMs { get; }
        public long InternetLatencyMs { get; }
        public string Summary { get; }

        public ConnectivityTestResult(
            bool gatewayReachable,
            bool dnsReachable,
            bool internetReachable,
            long gatewayLatencyMs,
            long dnsLatencyMs,
            long internetLatencyMs)
        {
            GatewayReachable = gatewayReachable;
            DnsReachable = dnsReachable;
            InternetReachable = internetReachable;
            GatewayLatencyMs = gatewayLatencyMs;
            DnsLatencyMs = dnsLatencyMs;
            InternetLatencyMs = internetLatencyMs;

            Summary = BuildSummary();
        }

        private string BuildSummary()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (GatewayReachable)
                parts.Add($"Gateway OK ({GatewayLatencyMs}ms)");
            else
                parts.Add("Gateway FAILED");

            if (DnsReachable)
                parts.Add($"DNS OK ({DnsLatencyMs}ms)");
            else
                parts.Add("DNS FAILED");

            if (InternetReachable)
                parts.Add($"Internet OK ({InternetLatencyMs}ms)");
            else
                parts.Add("Internet FAILED");

            return string.Join(" | ", parts);
        }

        public bool IsFullyConnected => GatewayReachable && DnsReachable && InternetReachable;
    }
}
