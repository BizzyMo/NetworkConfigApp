using System;
using System.Net;
using System.Text.RegularExpressions;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Validators
{
    /// <summary>
    /// Validates IPv4 addresses for network configuration.
    ///
    /// Algorithm: Multi-stage validation:
    ///   1. Format check (regex for ###.###.###.### pattern)
    ///   2. Octet range validation (0-255)
    ///   3. Special address detection (broadcast, loopback, multicast, reserved)
    ///   4. Optional subnet compatibility check
    ///
    /// Performance: O(1) - fixed-length string parsing
    /// Security: Prevents injection via strict input validation
    /// </summary>
    public static class IpAddressValidator
    {
        // Regex pattern for IPv4 format (does not validate ranges)
        private static readonly Regex IPv4Pattern = new Regex(
            @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$",
            RegexOptions.Compiled);

        /// <summary>
        /// Validates an IPv4 address string.
        /// </summary>
        /// <param name="ipAddress">IP address to validate</param>
        /// <returns>Validation result with details</returns>
        public static ValidationResult Validate(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return ValidationResult.Invalid("IP address is required");
            }

            ipAddress = ipAddress.Trim();

            // Check format
            var match = IPv4Pattern.Match(ipAddress);
            if (!match.Success)
            {
                return ValidationResult.Invalid("Invalid IPv4 format. Expected: ###.###.###.###");
            }

            // Parse and validate each octet
            var octets = new int[4];
            for (int i = 0; i < 4; i++)
            {
                if (!int.TryParse(match.Groups[i + 1].Value, out octets[i]))
                {
                    return ValidationResult.Invalid($"Invalid octet at position {i + 1}");
                }

                if (octets[i] < 0 || octets[i] > 255)
                {
                    return ValidationResult.Invalid($"Octet {i + 1} must be between 0 and 255");
                }
            }

            // Check for special addresses
            var specialCheck = CheckSpecialAddress(octets);
            if (specialCheck != null)
            {
                return ValidationResult.Warning(ipAddress, specialCheck);
            }

            return ValidationResult.Valid(ipAddress);
        }

        /// <summary>
        /// Validates an IP address for use as a static configuration.
        /// More restrictive than basic validation.
        /// </summary>
        public static ValidationResult ValidateForStatic(string ipAddress)
        {
            var basicResult = Validate(ipAddress);
            if (!basicResult.IsValid)
            {
                return basicResult;
            }

            // Additional checks for static IP assignment
            var octets = ParseOctets(ipAddress);

            // Cannot use network address (host portion all zeros) without subnet context
            // Cannot use broadcast address (host portion all ones) without subnet context
            // These are context-dependent, so we allow them but could add subnet-aware validation

            // Check for 0.0.0.0 (not valid for static)
            if (octets[0] == 0 && octets[1] == 0 && octets[2] == 0 && octets[3] == 0)
            {
                return ValidationResult.Invalid("0.0.0.0 cannot be used as a static IP address");
            }

            // Check for 255.255.255.255 (broadcast)
            if (octets[0] == 255 && octets[1] == 255 && octets[2] == 255 && octets[3] == 255)
            {
                return ValidationResult.Invalid("255.255.255.255 (broadcast) cannot be used as a static IP address");
            }

            return ValidationResult.Valid(ipAddress);
        }

        /// <summary>
        /// Validates an IP address for use as a gateway.
        /// </summary>
        public static ValidationResult ValidateForGateway(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return ValidationResult.Valid(string.Empty); // Gateway is optional
            }

            return ValidateForStatic(ipAddress);
        }

        /// <summary>
        /// Validates an IP address for use as a DNS server.
        /// </summary>
        public static ValidationResult ValidateForDns(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return ValidationResult.Valid(string.Empty); // DNS is optional
            }

            var basicResult = Validate(ipAddress);
            if (!basicResult.IsValid)
            {
                return basicResult;
            }

            var octets = ParseOctets(ipAddress);

            // Cannot use 0.0.0.0
            if (octets[0] == 0 && octets[1] == 0 && octets[2] == 0 && octets[3] == 0)
            {
                return ValidationResult.Invalid("0.0.0.0 cannot be used as a DNS server");
            }

            return ValidationResult.Valid(ipAddress);
        }

        /// <summary>
        /// Checks if IP is in the same subnet as the gateway.
        /// </summary>
        public static bool IsInSameSubnet(string ipAddress, string gateway, string subnetMask)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(gateway) || string.IsNullOrEmpty(subnetMask))
            {
                return true; // Can't validate without all three
            }

            try
            {
                var ip = ParseToUint(ipAddress);
                var gw = ParseToUint(gateway);
                var mask = ParseToUint(subnetMask);

                return (ip & mask) == (gw & mask);
            }
            catch
            {
                return true; // Can't validate, assume OK
            }
        }

        /// <summary>
        /// Parses IP string to 4-element int array.
        /// </summary>
        public static int[] ParseOctets(string ipAddress)
        {
            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
            {
                throw new ArgumentException("Invalid IP address format");
            }

            var octets = new int[4];
            for (int i = 0; i < 4; i++)
            {
                octets[i] = int.Parse(parts[i]);
            }
            return octets;
        }

        /// <summary>
        /// Parses IP string to 32-bit unsigned integer.
        /// </summary>
        public static uint ParseToUint(string ipAddress)
        {
            var octets = ParseOctets(ipAddress);
            return (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);
        }

        /// <summary>
        /// Converts 32-bit unsigned integer to IP string.
        /// </summary>
        public static string UintToString(uint ip)
        {
            return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
        }

        private static string CheckSpecialAddress(int[] octets)
        {
            // Loopback (127.x.x.x)
            if (octets[0] == 127)
            {
                return "Loopback address - typically not used for network configuration";
            }

            // Multicast (224-239.x.x.x)
            if (octets[0] >= 224 && octets[0] <= 239)
            {
                return "Multicast address - cannot be assigned to a network interface";
            }

            // Reserved (240-255.x.x.x except broadcast)
            if (octets[0] >= 240)
            {
                return "Reserved address range";
            }

            // Link-local (169.254.x.x)
            if (octets[0] == 169 && octets[1] == 254)
            {
                return "Link-local address - usually indicates DHCP failure";
            }

            // Test-Net (192.0.2.x, 198.51.100.x, 203.0.113.x)
            if ((octets[0] == 192 && octets[1] == 0 && octets[2] == 2) ||
                (octets[0] == 198 && octets[1] == 51 && octets[2] == 100) ||
                (octets[0] == 203 && octets[1] == 0 && octets[2] == 113))
            {
                return "Documentation/Test address - not for production use";
            }

            return null;
        }
    }

    /// <summary>
    /// Result of validation operation.
    /// </summary>
    public sealed class ValidationResult
    {
        public bool IsValid { get; }
        public bool HasWarning { get; }
        public string Value { get; }
        public string Message { get; }

        private ValidationResult(bool isValid, bool hasWarning, string value, string message)
        {
            IsValid = isValid;
            HasWarning = hasWarning;
            Value = value ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static ValidationResult Valid(string value)
        {
            return new ValidationResult(true, false, value, string.Empty);
        }

        public static ValidationResult Invalid(string message)
        {
            return new ValidationResult(false, false, string.Empty, message);
        }

        public static ValidationResult Warning(string value, string message)
        {
            return new ValidationResult(true, true, value, message);
        }

        public override string ToString()
        {
            if (!IsValid)
                return $"Invalid: {Message}";
            if (HasWarning)
                return $"Valid (Warning): {Message}";
            return $"Valid: {Value}";
        }
    }
}
