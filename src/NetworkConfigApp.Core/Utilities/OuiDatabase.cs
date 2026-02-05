using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NetworkConfigApp.Core.Utilities
{
    /// <summary>
    /// MAC address manufacturer lookup database (OUI - Organizationally Unique Identifier).
    ///
    /// Algorithm: Loads OUI database from embedded resource on first use.
    /// Lookup is O(1) using dictionary keyed by first 3 bytes of MAC.
    ///
    /// Performance: Database is loaded lazily and cached in memory (~50KB for common entries).
    /// Lookup operations are constant time.
    ///
    /// Security: Read-only database, no external network calls.
    /// </summary>
    public class OuiDatabase
    {
        private static readonly Lazy<OuiDatabase> _instance = new Lazy<OuiDatabase>(() => new OuiDatabase());
        private readonly Dictionary<string, string> _ouiToManufacturer;
        private readonly List<ManufacturerEntry> _allManufacturers;
        private bool _isLoaded;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static OuiDatabase Instance => _instance.Value;

        private OuiDatabase()
        {
            _ouiToManufacturer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _allManufacturers = new List<ManufacturerEntry>();
            LoadDatabase();
        }

        /// <summary>
        /// Looks up the manufacturer for a MAC address.
        /// </summary>
        /// <param name="macAddress">MAC address in any common format</param>
        /// <returns>Manufacturer name or "Unknown" if not found</returns>
        public string GetManufacturer(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return "Unknown";

            var prefix = GetOuiPrefix(macAddress);
            if (string.IsNullOrEmpty(prefix))
                return "Unknown";

            return _ouiToManufacturer.TryGetValue(prefix, out var manufacturer)
                ? manufacturer
                : "Unknown";
        }

        /// <summary>
        /// Gets a random OUI prefix for a specific manufacturer.
        /// </summary>
        public string GetRandomOuiForManufacturer(string manufacturerName)
        {
            if (string.IsNullOrEmpty(manufacturerName))
                return null;

            var matches = _allManufacturers
                .Where(m => m.Manufacturer.IndexOf(manufacturerName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 0)
                return null;

            var random = new Random();
            return matches[random.Next(matches.Count)].Prefix;
        }

        /// <summary>
        /// Gets all known manufacturers.
        /// </summary>
        public IReadOnlyList<string> GetAllManufacturers()
        {
            return _allManufacturers
                .Select(m => m.Manufacturer)
                .Distinct()
                .OrderBy(m => m)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets popular/common manufacturers for dropdown selection.
        /// </summary>
        public IReadOnlyList<ManufacturerEntry> GetCommonManufacturers()
        {
            var common = new[]
            {
                "Intel", "Realtek", "Qualcomm", "Broadcom", "Apple", "Dell",
                "HP", "Lenovo", "ASUS", "Acer", "Microsoft", "Samsung",
                "LG", "Sony", "Cisco", "TP-Link", "Netgear", "D-Link"
            };

            return _allManufacturers
                .Where(m => common.Any(c => m.Manufacturer.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                .GroupBy(m => GetPrimaryManufacturer(m.Manufacturer))
                .Select(g => g.First())
                .OrderBy(m => m.Manufacturer)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Generates a random MAC address with a valid OUI from a specific manufacturer.
        /// </summary>
        public string GenerateRandomMac(string manufacturerName = null)
        {
            var random = new Random();
            string prefix;

            if (!string.IsNullOrEmpty(manufacturerName))
            {
                prefix = GetRandomOuiForManufacturer(manufacturerName);
                if (prefix == null)
                {
                    // Fall back to random if manufacturer not found
                    prefix = _allManufacturers[random.Next(_allManufacturers.Count)].Prefix;
                }
            }
            else
            {
                // Random from database
                prefix = _allManufacturers[random.Next(_allManufacturers.Count)].Prefix;
            }

            // Generate random last 3 bytes
            var suffix = new byte[3];
            random.NextBytes(suffix);

            var prefixParts = prefix.Split(':');
            return $"{prefixParts[0]}:{prefixParts[1]}:{prefixParts[2]}:{suffix[0]:X2}:{suffix[1]:X2}:{suffix[2]:X2}";
        }

        /// <summary>
        /// Generates a completely random MAC address (no OUI validation).
        /// </summary>
        public string GenerateCompletelyRandomMac()
        {
            var random = new Random();
            var bytes = new byte[6];
            random.NextBytes(bytes);

            // Clear multicast bit (bit 0 of first byte)
            bytes[0] &= 0xFE;
            // Set locally administered bit (bit 1 of first byte)
            bytes[0] |= 0x02;

            return string.Join(":", bytes.Select(b => b.ToString("X2")));
        }

        /// <summary>
        /// Validates a MAC address format.
        /// </summary>
        public bool IsValidMacFormat(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return false;

            // Support formats: XX:XX:XX:XX:XX:XX, XX-XX-XX-XX-XX-XX, XXXXXXXXXXXX
            var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "");
            return cleanMac.Length == 12 && Regex.IsMatch(cleanMac, "^[0-9A-Fa-f]{12}$");
        }

        /// <summary>
        /// Normalizes MAC address to colon-separated format.
        /// </summary>
        public string NormalizeMac(string macAddress)
        {
            if (!IsValidMacFormat(macAddress))
                return macAddress;

            var clean = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper();
            return string.Join(":", Enumerable.Range(0, 6).Select(i => clean.Substring(i * 2, 2)));
        }

        private string GetOuiPrefix(string macAddress)
        {
            var normalized = NormalizeMac(macAddress);
            if (string.IsNullOrEmpty(normalized) || normalized.Length < 8)
                return null;

            // Return first 3 octets (XX:XX:XX)
            return normalized.Substring(0, 8);
        }

        private string GetPrimaryManufacturer(string fullName)
        {
            // Extract primary company name (before comma or parenthesis)
            var idx = fullName.IndexOfAny(new[] { ',', '(', '-' });
            return idx > 0 ? fullName.Substring(0, idx).Trim() : fullName;
        }

        private void LoadDatabase()
        {
            try
            {
                // Load embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "NetworkConfigApp.Core.Resources.oui_database.txt";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            ParseDatabase(reader);
                        }
                    }
                }

                // If embedded resource not found, use built-in common entries
                if (_ouiToManufacturer.Count == 0)
                {
                    LoadBuiltInEntries();
                }

                _isLoaded = true;
            }
            catch
            {
                // Fall back to built-in entries on any error
                LoadBuiltInEntries();
            }
        }

        private void ParseDatabase(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                // Format: XX:XX:XX=Manufacturer Name
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var prefix = parts[0].Trim().ToUpper();
                    var manufacturer = parts[1].Trim();

                    if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(manufacturer))
                    {
                        _ouiToManufacturer[prefix] = manufacturer;
                        _allManufacturers.Add(new ManufacturerEntry(prefix, manufacturer));
                    }
                }
            }
        }

        private void LoadBuiltInEntries()
        {
            // Built-in common entries as fallback
            var entries = new Dictionary<string, string>
            {
                // Intel
                { "00:1B:21", "Intel Corporate" },
                { "00:1C:C0", "Intel Corporate" },
                { "00:1E:64", "Intel Corporate" },
                { "00:1F:3B", "Intel Corporate" },
                { "00:21:5D", "Intel Corporate" },
                { "00:22:FA", "Intel Corporate" },
                { "00:24:D7", "Intel Corporate" },
                { "3C:A9:F4", "Intel Corporate" },
                { "48:51:B7", "Intel Corporate" },
                { "5C:87:9C", "Intel Corporate" },
                { "68:05:CA", "Intel Corporate" },
                { "80:86:F2", "Intel Corporate" },
                { "94:65:9C", "Intel Corporate" },
                { "A4:C4:94", "Intel Corporate" },
                { "B4:6B:FC", "Intel Corporate" },
                { "DC:71:96", "Intel Corporate" },
                { "F8:16:54", "Intel Corporate" },

                // Realtek
                { "00:E0:4C", "Realtek Semiconductor" },
                { "52:54:00", "Realtek Semiconductor" },
                { "08:00:27", "Realtek Semiconductor" },

                // Apple
                { "00:03:93", "Apple Inc." },
                { "00:0A:27", "Apple Inc." },
                { "00:0A:95", "Apple Inc." },
                { "00:0D:93", "Apple Inc." },
                { "00:11:24", "Apple Inc." },
                { "00:14:51", "Apple Inc." },
                { "00:16:CB", "Apple Inc." },
                { "00:17:F2", "Apple Inc." },
                { "00:19:E3", "Apple Inc." },
                { "00:1B:63", "Apple Inc." },
                { "00:1C:B3", "Apple Inc." },
                { "00:1D:4F", "Apple Inc." },
                { "00:1E:52", "Apple Inc." },
                { "00:1E:C2", "Apple Inc." },
                { "00:1F:5B", "Apple Inc." },
                { "00:1F:F3", "Apple Inc." },
                { "00:21:E9", "Apple Inc." },
                { "00:22:41", "Apple Inc." },
                { "00:23:12", "Apple Inc." },
                { "00:23:32", "Apple Inc." },
                { "00:23:6C", "Apple Inc." },
                { "00:23:DF", "Apple Inc." },
                { "00:24:36", "Apple Inc." },
                { "00:25:00", "Apple Inc." },
                { "00:25:4B", "Apple Inc." },
                { "00:25:BC", "Apple Inc." },
                { "00:26:08", "Apple Inc." },
                { "00:26:4A", "Apple Inc." },
                { "00:26:B0", "Apple Inc." },
                { "00:26:BB", "Apple Inc." },

                // Dell
                { "00:06:5B", "Dell Inc." },
                { "00:08:74", "Dell Inc." },
                { "00:0B:DB", "Dell Inc." },
                { "00:0D:56", "Dell Inc." },
                { "00:0F:1F", "Dell Inc." },
                { "00:11:43", "Dell Inc." },
                { "00:12:3F", "Dell Inc." },
                { "00:13:72", "Dell Inc." },
                { "00:14:22", "Dell Inc." },
                { "00:15:C5", "Dell Inc." },
                { "00:18:8B", "Dell Inc." },
                { "00:19:B9", "Dell Inc." },
                { "00:1A:A0", "Dell Inc." },

                // HP
                { "00:0B:CD", "HP Inc." },
                { "00:0D:9D", "HP Inc." },
                { "00:0F:20", "HP Inc." },
                { "00:0F:61", "HP Inc." },
                { "00:10:83", "HP Inc." },
                { "00:11:0A", "HP Inc." },
                { "00:11:85", "HP Inc." },
                { "00:12:79", "HP Inc." },
                { "00:13:21", "HP Inc." },
                { "00:14:38", "HP Inc." },
                { "00:14:C2", "HP Inc." },
                { "00:15:60", "HP Inc." },
                { "00:16:35", "HP Inc." },
                { "00:17:08", "HP Inc." },
                { "00:17:A4", "HP Inc." },

                // Lenovo
                { "00:06:1B", "Lenovo" },
                { "00:09:2D", "Lenovo" },
                { "00:0B:2F", "Lenovo" },
                { "00:0C:F1", "Lenovo" },
                { "00:0E:9B", "Lenovo" },
                { "00:10:C6", "Lenovo" },
                { "00:12:FE", "Lenovo" },
                { "00:14:5E", "Lenovo" },

                // Microsoft
                { "00:03:FF", "Microsoft Corporation" },
                { "00:0D:3A", "Microsoft Corporation" },
                { "00:12:5A", "Microsoft Corporation" },
                { "00:15:5D", "Microsoft Corporation" },
                { "00:17:FA", "Microsoft Corporation" },
                { "00:1D:D8", "Microsoft Corporation" },
                { "00:22:48", "Microsoft Corporation" },
                { "00:25:AE", "Microsoft Corporation" },
                { "00:50:F2", "Microsoft Corporation" },

                // Cisco
                { "00:00:0C", "Cisco Systems" },
                { "00:01:42", "Cisco Systems" },
                { "00:01:43", "Cisco Systems" },
                { "00:01:63", "Cisco Systems" },
                { "00:01:64", "Cisco Systems" },
                { "00:01:96", "Cisco Systems" },
                { "00:01:97", "Cisco Systems" },
                { "00:01:C7", "Cisco Systems" },
                { "00:01:C9", "Cisco Systems" },
                { "00:02:16", "Cisco Systems" },
                { "00:02:17", "Cisco Systems" },

                // Samsung
                { "00:00:F0", "Samsung Electronics" },
                { "00:02:78", "Samsung Electronics" },
                { "00:07:AB", "Samsung Electronics" },
                { "00:09:18", "Samsung Electronics" },
                { "00:0D:AE", "Samsung Electronics" },
                { "00:0F:73", "Samsung Electronics" },
                { "00:12:47", "Samsung Electronics" },
                { "00:12:FB", "Samsung Electronics" },
                { "00:13:77", "Samsung Electronics" },

                // TP-Link
                { "00:1D:0F", "TP-Link Technologies" },
                { "00:23:CD", "TP-Link Technologies" },
                { "00:25:86", "TP-Link Technologies" },
                { "00:27:19", "TP-Link Technologies" },
                { "14:CC:20", "TP-Link Technologies" },
                { "14:E6:E4", "TP-Link Technologies" },

                // Qualcomm/Atheros
                { "00:03:7F", "Qualcomm Atheros" },
                { "00:09:5B", "Qualcomm Atheros" },
                { "00:0B:6B", "Qualcomm Atheros" },
                { "00:0C:41", "Qualcomm Atheros" },
                { "00:0C:42", "Qualcomm Atheros" },
                { "00:0C:43", "Qualcomm Atheros" },
                { "00:0E:6D", "Qualcomm Atheros" },
                { "00:11:F5", "Qualcomm Atheros" },
                { "00:13:74", "Qualcomm Atheros" },

                // Broadcom
                { "00:10:18", "Broadcom Inc." },
                { "00:0A:F7", "Broadcom Inc." },
                { "00:05:B5", "Broadcom Inc." },
            };

            foreach (var entry in entries)
            {
                _ouiToManufacturer[entry.Key] = entry.Value;
                _allManufacturers.Add(new ManufacturerEntry(entry.Key, entry.Value));
            }
        }
    }

    /// <summary>
    /// Represents a manufacturer entry in the OUI database.
    /// </summary>
    public sealed class ManufacturerEntry
    {
        public string Prefix { get; }
        public string Manufacturer { get; }

        public ManufacturerEntry(string prefix, string manufacturer)
        {
            Prefix = prefix;
            Manufacturer = manufacturer;
        }

        public override string ToString()
        {
            return $"{Manufacturer} ({Prefix})";
        }
    }
}
