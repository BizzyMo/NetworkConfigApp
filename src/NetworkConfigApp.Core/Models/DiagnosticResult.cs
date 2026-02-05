using System;
using System.Collections.Generic;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Represents the result of a network diagnostic operation (ping, traceroute, etc.).
    /// Immutable data model for diagnostic results.
    ///
    /// Algorithm: Captures results of ICMP ping or traceroute operations.
    /// Data Structure: Read-only result with hop details for trace routes.
    /// Security: Only contains diagnostic data, no credentials or sensitive info.
    /// </summary>
    public sealed class DiagnosticResult
    {
        /// <summary>Type of diagnostic performed.</summary>
        public DiagnosticType Type { get; }

        /// <summary>Target host or IP address.</summary>
        public string Target { get; }

        /// <summary>True if the diagnostic was successful.</summary>
        public bool IsSuccess { get; }

        /// <summary>Round-trip time in milliseconds (-1 if failed).</summary>
        public long RoundTripTimeMs { get; }

        /// <summary>TTL (Time To Live) value from response.</summary>
        public int Ttl { get; }

        /// <summary>Status message or error description.</summary>
        public string Message { get; }

        /// <summary>Detailed hop information (for traceroute).</summary>
        public IReadOnlyList<TraceHop> Hops { get; }

        /// <summary>When this diagnostic was performed.</summary>
        public DateTime Timestamp { get; }

        private DiagnosticResult(
            DiagnosticType type,
            string target,
            bool isSuccess,
            long roundTripTimeMs,
            int ttl,
            string message,
            IReadOnlyList<TraceHop> hops,
            DateTime timestamp)
        {
            Type = type;
            Target = target ?? string.Empty;
            IsSuccess = isSuccess;
            RoundTripTimeMs = roundTripTimeMs;
            Ttl = ttl;
            Message = message ?? string.Empty;
            Hops = hops ?? new List<TraceHop>().AsReadOnly();
            Timestamp = timestamp;
        }

        /// <summary>Creates a successful ping result.</summary>
        public static DiagnosticResult PingSuccess(string target, long rttMs, int ttl)
        {
            return new DiagnosticResult(
                DiagnosticType.Ping,
                target,
                true,
                rttMs,
                ttl,
                $"Reply from {target}: time={rttMs}ms TTL={ttl}",
                null,
                DateTime.Now);
        }

        /// <summary>Creates a failed ping result.</summary>
        public static DiagnosticResult PingFailure(string target, string error)
        {
            return new DiagnosticResult(
                DiagnosticType.Ping,
                target,
                false,
                -1,
                0,
                error,
                null,
                DateTime.Now);
        }

        /// <summary>Creates a traceroute result.</summary>
        public static DiagnosticResult TraceRoute(string target, bool isComplete, List<TraceHop> hops)
        {
            var message = isComplete
                ? $"Trace complete to {target} in {hops.Count} hops"
                : $"Trace incomplete to {target} after {hops.Count} hops";

            return new DiagnosticResult(
                DiagnosticType.TraceRoute,
                target,
                isComplete,
                -1,
                0,
                message,
                hops.AsReadOnly(),
                DateTime.Now);
        }

        /// <summary>Creates a DNS resolution result.</summary>
        public static DiagnosticResult DnsResolution(string hostname, bool success, string resolvedIp, long timeMs)
        {
            return new DiagnosticResult(
                DiagnosticType.DnsResolution,
                hostname,
                success,
                timeMs,
                0,
                success ? $"Resolved {hostname} to {resolvedIp}" : $"Failed to resolve {hostname}",
                null,
                DateTime.Now);
        }

        /// <summary>Creates a port check result.</summary>
        public static DiagnosticResult PortCheck(string target, int port, bool isOpen, long timeMs)
        {
            return new DiagnosticResult(
                DiagnosticType.PortCheck,
                $"{target}:{port}",
                isOpen,
                timeMs,
                0,
                isOpen ? $"Port {port} is open on {target}" : $"Port {port} is closed or filtered on {target}",
                null,
                DateTime.Now);
        }

        /// <summary>
        /// Gets a color indicator for this result (for UI display).
        /// </summary>
        public string GetStatusColor()
        {
            if (IsSuccess)
            {
                if (Type == DiagnosticType.Ping && RoundTripTimeMs > 100)
                    return "Yellow"; // High latency warning

                return "Green";
            }
            return "Red";
        }

        public override string ToString()
        {
            return Message;
        }
    }

    /// <summary>
    /// Types of network diagnostics.
    /// </summary>
    public enum DiagnosticType
    {
        Ping,
        TraceRoute,
        DnsResolution,
        PortCheck
    }

    /// <summary>
    /// Represents a single hop in a traceroute.
    /// </summary>
    public sealed class TraceHop
    {
        /// <summary>Hop number (1-based).</summary>
        public int HopNumber { get; }

        /// <summary>IP address of this hop.</summary>
        public string Address { get; }

        /// <summary>Hostname if resolved (may be same as Address).</summary>
        public string Hostname { get; }

        /// <summary>Round-trip times for probes (usually 3).</summary>
        public IReadOnlyList<long> RoundTripTimesMs { get; }

        /// <summary>True if this hop responded.</summary>
        public bool IsResponsive { get; }

        public TraceHop(int hopNumber, string address, string hostname, List<long> rttMs, bool isResponsive)
        {
            HopNumber = hopNumber;
            Address = address ?? "*";
            Hostname = hostname ?? address ?? "*";
            RoundTripTimesMs = rttMs?.AsReadOnly() ?? new List<long>().AsReadOnly();
            IsResponsive = isResponsive;
        }

        /// <summary>Gets average RTT or -1 if no responses.</summary>
        public long GetAverageRtt()
        {
            if (RoundTripTimesMs == null || RoundTripTimesMs.Count == 0)
                return -1;

            long sum = 0;
            int count = 0;
            foreach (var rtt in RoundTripTimesMs)
            {
                if (rtt >= 0)
                {
                    sum += rtt;
                    count++;
                }
            }
            return count > 0 ? sum / count : -1;
        }

        public override string ToString()
        {
            if (!IsResponsive)
                return $"{HopNumber,2}  *  *  *  Request timed out.";

            var rtts = string.Join("  ", RoundTripTimesMs);
            return $"{HopNumber,2}  {rtts}ms  {Hostname} [{Address}]";
        }
    }
}
