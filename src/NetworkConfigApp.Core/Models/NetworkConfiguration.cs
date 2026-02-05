using System;
using Newtonsoft.Json;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Represents an IP network configuration (IP, subnet, gateway, DNS).
    /// Immutable data model supporting both DHCP and static configurations.
    ///
    /// Algorithm: Simple value object pattern with immutability for thread safety.
    /// Data Structure: Read-only properties with fluent builder methods.
    /// Security: IP addresses are validated before use; no credential storage.
    /// </summary>
    public sealed class NetworkConfiguration
    {
        /// <summary>IPv4 address (e.g., "192.168.1.100"). Empty for DHCP.</summary>
        [JsonProperty("ipAddress")]
        public string IpAddress { get; }

        /// <summary>Subnet mask (e.g., "255.255.255.0"). Empty for DHCP.</summary>
        [JsonProperty("subnetMask")]
        public string SubnetMask { get; }

        /// <summary>Default gateway (e.g., "192.168.1.1"). Can be empty.</summary>
        [JsonProperty("gateway")]
        public string Gateway { get; }

        /// <summary>Primary DNS server. Can be empty.</summary>
        [JsonProperty("dns1")]
        public string Dns1 { get; }

        /// <summary>Secondary DNS server. Can be empty.</summary>
        [JsonProperty("dns2")]
        public string Dns2 { get; }

        /// <summary>True if this represents a DHCP configuration.</summary>
        [JsonProperty("isDhcp")]
        public bool IsDhcp { get; }

        /// <summary>Optional description/comment for this configuration.</summary>
        [JsonProperty("description")]
        public string Description { get; }

        /// <summary>Timestamp when this configuration was captured/created.</summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; }

        [JsonConstructor]
        private NetworkConfiguration(
            string ipAddress,
            string subnetMask,
            string gateway,
            string dns1,
            string dns2,
            bool isDhcp,
            string description,
            DateTime timestamp)
        {
            IpAddress = ipAddress ?? string.Empty;
            SubnetMask = subnetMask ?? string.Empty;
            Gateway = gateway ?? string.Empty;
            Dns1 = dns1 ?? string.Empty;
            Dns2 = dns2 ?? string.Empty;
            IsDhcp = isDhcp;
            Description = description ?? string.Empty;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Creates an empty/unknown configuration.
        /// </summary>
        public static NetworkConfiguration Empty()
        {
            return new NetworkConfiguration(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                DateTime.Now);
        }

        /// <summary>
        /// Creates a DHCP configuration.
        /// </summary>
        public static NetworkConfiguration Dhcp(string description = "")
        {
            return new NetworkConfiguration(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true,
                description,
                DateTime.Now);
        }

        /// <summary>
        /// Creates a static IP configuration.
        /// </summary>
        public static NetworkConfiguration Static(
            string ipAddress,
            string subnetMask,
            string gateway,
            string dns1 = "",
            string dns2 = "",
            string description = "")
        {
            return new NetworkConfiguration(
                ipAddress,
                subnetMask,
                gateway,
                dns1,
                dns2,
                false,
                description,
                DateTime.Now);
        }

        /// <summary>
        /// Creates a configuration from current adapter state.
        /// </summary>
        public static NetworkConfiguration FromAdapterState(
            string ipAddress,
            string subnetMask,
            string gateway,
            string dns1,
            string dns2,
            bool isDhcp)
        {
            return new NetworkConfiguration(
                ipAddress,
                subnetMask,
                gateway,
                dns1,
                dns2,
                isDhcp,
                string.Empty,
                DateTime.Now);
        }

        /// <summary>Creates a copy with updated IP address.</summary>
        public NetworkConfiguration WithIpAddress(string ip)
        {
            return new NetworkConfiguration(ip, SubnetMask, Gateway, Dns1, Dns2, false, Description, DateTime.Now);
        }

        /// <summary>Creates a copy with updated subnet mask.</summary>
        public NetworkConfiguration WithSubnetMask(string mask)
        {
            return new NetworkConfiguration(IpAddress, mask, Gateway, Dns1, Dns2, IsDhcp, Description, DateTime.Now);
        }

        /// <summary>Creates a copy with updated gateway.</summary>
        public NetworkConfiguration WithGateway(string gw)
        {
            return new NetworkConfiguration(IpAddress, SubnetMask, gw, Dns1, Dns2, IsDhcp, Description, DateTime.Now);
        }

        /// <summary>Creates a copy with updated DNS servers.</summary>
        public NetworkConfiguration WithDns(string primary, string secondary = "")
        {
            return new NetworkConfiguration(IpAddress, SubnetMask, Gateway, primary, secondary, IsDhcp, Description, DateTime.Now);
        }

        /// <summary>Creates a copy with updated description.</summary>
        public NetworkConfiguration WithDescription(string desc)
        {
            return new NetworkConfiguration(IpAddress, SubnetMask, Gateway, Dns1, Dns2, IsDhcp, desc, Timestamp);
        }

        /// <summary>
        /// Calculates CIDR prefix length from subnet mask.
        /// </summary>
        public int GetCidrPrefix()
        {
            if (string.IsNullOrEmpty(SubnetMask))
                return 0;

            try
            {
                var parts = SubnetMask.Split('.');
                if (parts.Length != 4)
                    return 0;

                int prefix = 0;
                foreach (var part in parts)
                {
                    if (!byte.TryParse(part, out byte b))
                        return 0;

                    while (b > 0)
                    {
                        prefix += (b & 1);
                        b >>= 1;
                    }
                }
                return prefix;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets subnet mask from CIDR prefix.
        /// </summary>
        public static string CidrToSubnetMask(int prefix)
        {
            if (prefix < 0 || prefix > 32)
                return "255.255.255.0";

            uint mask = prefix == 0 ? 0 : 0xFFFFFFFF << (32 - prefix);
            return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
        }

        /// <summary>
        /// Checks if this configuration has all required fields for static IP.
        /// </summary>
        public bool IsValidForStatic()
        {
            return !string.IsNullOrWhiteSpace(IpAddress) &&
                   !string.IsNullOrWhiteSpace(SubnetMask);
        }

        /// <summary>
        /// Creates a display summary of this configuration.
        /// </summary>
        public string ToDisplayString()
        {
            if (IsDhcp)
                return "DHCP (Automatic)";

            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(IpAddress))
                parts.Add($"IP: {IpAddress}/{GetCidrPrefix()}");

            if (!string.IsNullOrEmpty(Gateway))
                parts.Add($"GW: {Gateway}");

            if (!string.IsNullOrEmpty(Dns1))
                parts.Add($"DNS: {Dns1}");

            return string.Join(" | ", parts);
        }

        public override bool Equals(object obj)
        {
            if (obj is NetworkConfiguration other)
            {
                return IpAddress == other.IpAddress &&
                       SubnetMask == other.SubnetMask &&
                       Gateway == other.Gateway &&
                       Dns1 == other.Dns1 &&
                       Dns2 == other.Dns2 &&
                       IsDhcp == other.IsDhcp;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (IpAddress?.GetHashCode() ?? 0);
                hash = hash * 31 + (SubnetMask?.GetHashCode() ?? 0);
                hash = hash * 31 + (Gateway?.GetHashCode() ?? 0);
                hash = hash * 31 + (Dns1?.GetHashCode() ?? 0);
                hash = hash * 31 + (Dns2?.GetHashCode() ?? 0);
                hash = hash * 31 + IsDhcp.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return ToDisplayString();
        }
    }
}
