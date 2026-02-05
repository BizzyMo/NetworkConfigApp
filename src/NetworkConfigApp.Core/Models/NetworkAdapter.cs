using System;
using System.Net.NetworkInformation;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Represents a network adapter with its properties and current configuration.
    /// Immutable data model following functional programming principles.
    ///
    /// Algorithm: Wraps System.Net.NetworkInformation data in a clean, immutable structure.
    /// Data Structure: Read-only properties with factory method for creation.
    /// Security: No sensitive data stored; MAC address is hardware info only.
    /// </summary>
    public sealed class NetworkAdapter
    {
        /// <summary>Unique identifier for this adapter (interface index).</summary>
        public int Id { get; }

        /// <summary>Display name of the adapter (e.g., "Ethernet", "Wi-Fi").</summary>
        public string Name { get; }

        /// <summary>Description from the driver/manufacturer.</summary>
        public string Description { get; }

        /// <summary>Type of adapter (Ethernet, Wireless80211, etc.).</summary>
        public NetworkInterfaceType InterfaceType { get; }

        /// <summary>Current operational status.</summary>
        public OperationalStatus Status { get; }

        /// <summary>Physical (MAC) address in colon-separated format.</summary>
        public string MacAddress { get; }

        /// <summary>Link speed in bits per second (-1 if unknown).</summary>
        public long Speed { get; }

        /// <summary>True if DHCP is enabled for this adapter.</summary>
        public bool IsDhcpEnabled { get; }

        /// <summary>True if this adapter is connected and has a gateway (likely active internet).</summary>
        public bool IsActive { get; }

        /// <summary>Current IP configuration if available.</summary>
        public NetworkConfiguration CurrentConfiguration { get; }

        private NetworkAdapter(
            int id,
            string name,
            string description,
            NetworkInterfaceType interfaceType,
            OperationalStatus status,
            string macAddress,
            long speed,
            bool isDhcpEnabled,
            bool isActive,
            NetworkConfiguration currentConfiguration)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            InterfaceType = interfaceType;
            Status = status;
            MacAddress = macAddress ?? string.Empty;
            Speed = speed;
            IsDhcpEnabled = isDhcpEnabled;
            IsActive = isActive;
            CurrentConfiguration = currentConfiguration;
        }

        /// <summary>
        /// Factory method to create a NetworkAdapter from system information.
        /// </summary>
        public static NetworkAdapter Create(
            int id,
            string name,
            string description,
            NetworkInterfaceType interfaceType,
            OperationalStatus status,
            string macAddress,
            long speed,
            bool isDhcpEnabled,
            bool isActive,
            NetworkConfiguration currentConfiguration)
        {
            return new NetworkAdapter(
                id,
                name,
                description,
                interfaceType,
                status,
                macAddress,
                speed,
                isDhcpEnabled,
                isActive,
                currentConfiguration);
        }

        /// <summary>
        /// Creates a copy with updated configuration.
        /// </summary>
        public NetworkAdapter WithConfiguration(NetworkConfiguration newConfig)
        {
            return new NetworkAdapter(
                Id,
                Name,
                Description,
                InterfaceType,
                Status,
                MacAddress,
                Speed,
                IsDhcpEnabled,
                IsActive,
                newConfig);
        }

        /// <summary>
        /// Creates a copy with updated active status.
        /// </summary>
        public NetworkAdapter WithActiveStatus(bool isActive)
        {
            return new NetworkAdapter(
                Id,
                Name,
                Description,
                InterfaceType,
                Status,
                MacAddress,
                Speed,
                IsDhcpEnabled,
                isActive,
                CurrentConfiguration);
        }

        /// <summary>
        /// Gets a human-readable speed string (e.g., "1 Gbps", "100 Mbps").
        /// </summary>
        public string GetSpeedDisplay()
        {
            if (Speed <= 0)
                return "Unknown";

            if (Speed >= 1_000_000_000)
                return $"{Speed / 1_000_000_000} Gbps";

            if (Speed >= 1_000_000)
                return $"{Speed / 1_000_000} Mbps";

            if (Speed >= 1_000)
                return $"{Speed / 1_000} Kbps";

            return $"{Speed} bps";
        }

        /// <summary>
        /// Gets a display string for the interface type.
        /// </summary>
        public string GetTypeDisplay()
        {
            switch (InterfaceType)
            {
                case NetworkInterfaceType.Ethernet:
                    return "Ethernet";
                case NetworkInterfaceType.Wireless80211:
                    return "Wi-Fi";
                case NetworkInterfaceType.Loopback:
                    return "Loopback";
                case NetworkInterfaceType.Ppp:
                    return "PPP";
                case NetworkInterfaceType.Tunnel:
                    return "VPN/Tunnel";
                default:
                    return InterfaceType.ToString();
            }
        }

        /// <summary>
        /// Gets a display string for the status.
        /// </summary>
        public string GetStatusDisplay()
        {
            switch (Status)
            {
                case OperationalStatus.Up:
                    return "Connected";
                case OperationalStatus.Down:
                    return "Disconnected";
                case OperationalStatus.LowerLayerDown:
                    return "Cable Unplugged";
                case OperationalStatus.NotPresent:
                    return "Not Present";
                case OperationalStatus.Dormant:
                    return "Dormant";
                default:
                    return Status.ToString();
            }
        }

        public override string ToString()
        {
            return IsActive ? $"{Name} (Active)" : Name;
        }
    }
}
