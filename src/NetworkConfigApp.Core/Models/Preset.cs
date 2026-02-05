using System;
using Newtonsoft.Json;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Represents a saved network configuration preset (profile).
    /// Immutable data model for storing reusable configurations.
    ///
    /// Algorithm: Simple value object with metadata for user organization.
    /// Data Structure: Read-only properties with JSON serialization support.
    /// Security: Presets can be encrypted via DPAPI when saved to disk.
    /// </summary>
    public sealed class Preset
    {
        /// <summary>Unique identifier for this preset.</summary>
        [JsonProperty("id")]
        public Guid Id { get; }

        /// <summary>User-friendly name (e.g., "Home", "Office", "VPN").</summary>
        [JsonProperty("name")]
        public string Name { get; }

        /// <summary>Optional description of when/why to use this preset.</summary>
        [JsonProperty("description")]
        public string Description { get; }

        /// <summary>The network configuration stored in this preset.</summary>
        [JsonProperty("configuration")]
        public NetworkConfiguration Configuration { get; }

        /// <summary>Optional adapter name this preset was created for.</summary>
        [JsonProperty("adapterName")]
        public string AdapterName { get; }

        /// <summary>When this preset was created.</summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; }

        /// <summary>When this preset was last modified.</summary>
        [JsonProperty("modifiedAt")]
        public DateTime ModifiedAt { get; }

        /// <summary>When this preset was last applied (null if never).</summary>
        [JsonProperty("lastAppliedAt")]
        public DateTime? LastAppliedAt { get; }

        /// <summary>True if this preset should be encrypted when saved.</summary>
        [JsonProperty("isEncrypted")]
        public bool IsEncrypted { get; }

        /// <summary>Sort order for display (lower = first).</summary>
        [JsonProperty("sortOrder")]
        public int SortOrder { get; }

        [JsonConstructor]
        private Preset(
            Guid id,
            string name,
            string description,
            NetworkConfiguration configuration,
            string adapterName,
            DateTime createdAt,
            DateTime modifiedAt,
            DateTime? lastAppliedAt,
            bool isEncrypted,
            int sortOrder)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            AdapterName = adapterName ?? string.Empty;
            CreatedAt = createdAt;
            ModifiedAt = modifiedAt;
            LastAppliedAt = lastAppliedAt;
            IsEncrypted = isEncrypted;
            SortOrder = sortOrder;
        }

        /// <summary>
        /// Creates a new preset with the given configuration.
        /// </summary>
        public static Preset Create(
            string name,
            NetworkConfiguration configuration,
            string description = "",
            string adapterName = "",
            bool isEncrypted = false,
            int sortOrder = 0)
        {
            var now = DateTime.Now;
            return new Preset(
                Guid.NewGuid(),
                name,
                description,
                configuration,
                adapterName,
                now,
                now,
                null,
                isEncrypted,
                sortOrder);
        }

        /// <summary>Creates a copy with updated name.</summary>
        public Preset WithName(string name)
        {
            return new Preset(Id, name, Description, Configuration, AdapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, IsEncrypted, SortOrder);
        }

        /// <summary>Creates a copy with updated description.</summary>
        public Preset WithDescription(string description)
        {
            return new Preset(Id, Name, description, Configuration, AdapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, IsEncrypted, SortOrder);
        }

        /// <summary>Creates a copy with updated configuration.</summary>
        public Preset WithConfiguration(NetworkConfiguration config)
        {
            return new Preset(Id, Name, Description, config, AdapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, IsEncrypted, SortOrder);
        }

        /// <summary>Creates a copy with updated adapter name.</summary>
        public Preset WithAdapterName(string adapterName)
        {
            return new Preset(Id, Name, Description, Configuration, adapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, IsEncrypted, SortOrder);
        }

        /// <summary>Creates a copy marked as just applied.</summary>
        public Preset WithApplied()
        {
            return new Preset(Id, Name, Description, Configuration, AdapterName,
                CreatedAt, DateTime.Now, DateTime.Now, IsEncrypted, SortOrder);
        }

        /// <summary>Creates a copy with updated encryption setting.</summary>
        public Preset WithEncryption(bool encrypted)
        {
            return new Preset(Id, Name, Description, Configuration, AdapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, encrypted, SortOrder);
        }

        /// <summary>Creates a copy with updated sort order.</summary>
        public Preset WithSortOrder(int order)
        {
            return new Preset(Id, Name, Description, Configuration, AdapterName,
                CreatedAt, DateTime.Now, LastAppliedAt, IsEncrypted, order);
        }

        /// <summary>
        /// Gets a display summary for this preset.
        /// </summary>
        public string ToDisplayString()
        {
            var configSummary = Configuration.IsDhcp ? "DHCP" : Configuration.IpAddress;
            return $"{Name} ({configSummary})";
        }

        /// <summary>
        /// Gets a safe filename for this preset (no special characters).
        /// </summary>
        public string GetSafeFileName()
        {
            var safe = Name.ToLowerInvariant();
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }
            safe = safe.Replace(' ', '_');
            return $"{safe}.json";
        }

        public override string ToString()
        {
            return ToDisplayString();
        }

        public override bool Equals(object obj)
        {
            return obj is Preset other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
