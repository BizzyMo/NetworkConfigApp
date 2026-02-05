using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NetworkConfigApp.Core.Validators
{
    /// <summary>
    /// Validates subnet masks for network configuration.
    ///
    /// Algorithm: Validates subnet masks in two formats:
    ///   1. Dotted decimal (255.255.255.0)
    ///   2. CIDR prefix (/24)
    ///
    /// A valid subnet mask must have all 1s followed by all 0s in binary.
    /// For example: 255.255.255.0 = 11111111.11111111.11111111.00000000
    ///
    /// Performance: O(1) - fixed lookup for common masks, O(1) binary check for others
    /// Security: Prevents invalid masks that could cause network misconfiguration
    /// </summary>
    public static class SubnetValidator
    {
        // Valid subnet masks mapped to their CIDR prefix
        private static readonly Dictionary<string, int> ValidMasks = new Dictionary<string, int>
        {
            { "0.0.0.0", 0 },
            { "128.0.0.0", 1 },
            { "192.0.0.0", 2 },
            { "224.0.0.0", 3 },
            { "240.0.0.0", 4 },
            { "248.0.0.0", 5 },
            { "252.0.0.0", 6 },
            { "254.0.0.0", 7 },
            { "255.0.0.0", 8 },
            { "255.128.0.0", 9 },
            { "255.192.0.0", 10 },
            { "255.224.0.0", 11 },
            { "255.240.0.0", 12 },
            { "255.248.0.0", 13 },
            { "255.252.0.0", 14 },
            { "255.254.0.0", 15 },
            { "255.255.0.0", 16 },
            { "255.255.128.0", 17 },
            { "255.255.192.0", 18 },
            { "255.255.224.0", 19 },
            { "255.255.240.0", 20 },
            { "255.255.248.0", 21 },
            { "255.255.252.0", 22 },
            { "255.255.254.0", 23 },
            { "255.255.255.0", 24 },
            { "255.255.255.128", 25 },
            { "255.255.255.192", 26 },
            { "255.255.255.224", 27 },
            { "255.255.255.240", 28 },
            { "255.255.255.248", 29 },
            { "255.255.255.252", 30 },
            { "255.255.255.254", 31 },
            { "255.255.255.255", 32 }
        };

        // Reverse lookup: prefix to mask
        private static readonly Dictionary<int, string> PrefixToMask = new Dictionary<int, string>();

        // Common masks with descriptions
        private static readonly Dictionary<int, string> CommonMaskDescriptions = new Dictionary<int, string>
        {
            { 8, "Class A (/8) - 16,777,214 hosts" },
            { 16, "Class B (/16) - 65,534 hosts" },
            { 24, "Class C (/24) - 254 hosts" },
            { 25, "/25 - 126 hosts" },
            { 26, "/26 - 62 hosts" },
            { 27, "/27 - 30 hosts" },
            { 28, "/28 - 14 hosts" },
            { 29, "/29 - 6 hosts" },
            { 30, "/30 - 2 hosts (point-to-point)" },
            { 31, "/31 - 2 hosts (RFC 3021)" },
            { 32, "/32 - Single host" }
        };

        static SubnetValidator()
        {
            // Build reverse lookup
            foreach (var kvp in ValidMasks)
            {
                PrefixToMask[kvp.Value] = kvp.Key;
            }
        }

        /// <summary>
        /// Validates a subnet mask in dotted decimal format.
        /// </summary>
        public static ValidationResult Validate(string subnetMask)
        {
            if (string.IsNullOrWhiteSpace(subnetMask))
            {
                return ValidationResult.Invalid("Subnet mask is required");
            }

            subnetMask = subnetMask.Trim();

            // Handle CIDR format
            if (subnetMask.StartsWith("/"))
            {
                return ValidateCidr(subnetMask.Substring(1));
            }

            // Check if it's a known valid mask (fast path)
            if (ValidMasks.ContainsKey(subnetMask))
            {
                return ValidationResult.Valid(subnetMask);
            }

            // Validate format and check if it's a valid mask pattern
            return ValidateMaskBits(subnetMask);
        }

        /// <summary>
        /// Validates a CIDR prefix (0-32).
        /// </summary>
        public static ValidationResult ValidateCidr(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return ValidationResult.Invalid("CIDR prefix is required");
            }

            // Remove leading slash if present
            var prefixStr = prefix.TrimStart('/');

            if (!int.TryParse(prefixStr, out int cidr))
            {
                return ValidationResult.Invalid("CIDR prefix must be a number");
            }

            if (cidr < 0 || cidr > 32)
            {
                return ValidationResult.Invalid("CIDR prefix must be between 0 and 32");
            }

            // Convert to dotted decimal and return
            var mask = CidrToMask(cidr);
            return ValidationResult.Valid(mask);
        }

        /// <summary>
        /// Converts CIDR prefix to dotted decimal subnet mask.
        /// </summary>
        public static string CidrToMask(int prefix)
        {
            if (prefix < 0 || prefix > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(prefix), "Prefix must be 0-32");
            }

            if (PrefixToMask.TryGetValue(prefix, out string mask))
            {
                return mask;
            }

            // Calculate mask (should not reach here due to complete dictionary)
            uint maskValue = prefix == 0 ? 0 : 0xFFFFFFFF << (32 - prefix);
            return $"{(maskValue >> 24) & 0xFF}.{(maskValue >> 16) & 0xFF}.{(maskValue >> 8) & 0xFF}.{maskValue & 0xFF}";
        }

        /// <summary>
        /// Converts dotted decimal subnet mask to CIDR prefix.
        /// </summary>
        public static int MaskToCidr(string subnetMask)
        {
            if (string.IsNullOrEmpty(subnetMask))
            {
                return 0;
            }

            if (ValidMasks.TryGetValue(subnetMask, out int prefix))
            {
                return prefix;
            }

            // Calculate from bits (for potentially non-standard masks)
            try
            {
                var octets = subnetMask.Split('.');
                if (octets.Length != 4)
                {
                    return 0;
                }

                int cidr = 0;
                foreach (var octetStr in octets)
                {
                    if (!byte.TryParse(octetStr, out byte octet))
                    {
                        return 0;
                    }

                    // Count bits
                    while (octet > 0)
                    {
                        cidr += (octet & 1);
                        octet >>= 1;
                    }
                }
                return cidr;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets number of host addresses for a given prefix.
        /// </summary>
        public static int GetHostCount(int prefix)
        {
            if (prefix >= 31)
            {
                return prefix == 31 ? 2 : 1;
            }

            return (1 << (32 - prefix)) - 2; // 2^(32-prefix) - network - broadcast
        }

        /// <summary>
        /// Gets a human-readable description of the subnet.
        /// </summary>
        public static string GetDescription(int prefix)
        {
            if (CommonMaskDescriptions.TryGetValue(prefix, out string desc))
            {
                return desc;
            }

            var hosts = GetHostCount(prefix);
            return $"/{prefix} - {hosts:N0} hosts";
        }

        /// <summary>
        /// Calculates network address from IP and subnet mask.
        /// </summary>
        public static string GetNetworkAddress(string ipAddress, string subnetMask)
        {
            try
            {
                var ip = IpAddressValidator.ParseToUint(ipAddress);
                var mask = IpAddressValidator.ParseToUint(subnetMask);
                var network = ip & mask;
                return IpAddressValidator.UintToString(network);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Calculates broadcast address from IP and subnet mask.
        /// </summary>
        public static string GetBroadcastAddress(string ipAddress, string subnetMask)
        {
            try
            {
                var ip = IpAddressValidator.ParseToUint(ipAddress);
                var mask = IpAddressValidator.ParseToUint(subnetMask);
                var broadcast = (ip & mask) | ~mask;
                return IpAddressValidator.UintToString(broadcast);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets common subnet masks for dropdown selection.
        /// </summary>
        public static IReadOnlyList<SubnetOption> GetCommonMasks()
        {
            return new List<SubnetOption>
            {
                new SubnetOption(8, "255.0.0.0", "Class A - 16M hosts"),
                new SubnetOption(16, "255.255.0.0", "Class B - 65K hosts"),
                new SubnetOption(24, "255.255.255.0", "Class C - 254 hosts"),
                new SubnetOption(25, "255.255.255.128", "/25 - 126 hosts"),
                new SubnetOption(26, "255.255.255.192", "/26 - 62 hosts"),
                new SubnetOption(27, "255.255.255.224", "/27 - 30 hosts"),
                new SubnetOption(28, "255.255.255.240", "/28 - 14 hosts"),
                new SubnetOption(29, "255.255.255.248", "/29 - 6 hosts"),
                new SubnetOption(30, "255.255.255.252", "/30 - 2 hosts")
            };
        }

        private static ValidationResult ValidateMaskBits(string subnetMask)
        {
            var pattern = new Regex(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$");
            var match = pattern.Match(subnetMask);

            if (!match.Success)
            {
                return ValidationResult.Invalid("Invalid subnet mask format");
            }

            // Parse octets
            var octets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(match.Groups[i + 1].Value, out octets[i]))
                {
                    return ValidationResult.Invalid($"Invalid octet at position {i + 1}");
                }
            }

            // Convert to 32-bit value
            uint mask = (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);

            // A valid subnet mask must be contiguous 1s followed by contiguous 0s
            // After inverting, we should get contiguous 0s followed by contiguous 1s
            // Adding 1 to that should give us a power of 2
            uint inverted = ~mask;
            if (inverted != 0 && (inverted & (inverted + 1)) != 0)
            {
                return ValidationResult.Invalid("Invalid subnet mask: bits must be contiguous");
            }

            return ValidationResult.Valid(subnetMask);
        }
    }

    /// <summary>
    /// Represents a subnet mask option for UI selection.
    /// </summary>
    public sealed class SubnetOption
    {
        public int Prefix { get; }
        public string Mask { get; }
        public string Description { get; }

        public SubnetOption(int prefix, string mask, string description)
        {
            Prefix = prefix;
            Mask = mask;
            Description = description;
        }

        public override string ToString()
        {
            return $"{Mask} (/{Prefix}) - {Description}";
        }
    }
}
