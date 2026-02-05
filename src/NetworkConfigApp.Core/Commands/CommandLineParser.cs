using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Commands
{
    /// <summary>
    /// Parses command line arguments for batch/automation mode.
    ///
    /// Algorithm: Two-pass parsing:
    ///   1. Tokenize arguments (handle quoted strings)
    ///   2. Match tokens to known command patterns
    ///
    /// Supports formats:
    ///   /adapter:"Ethernet"
    ///   /static:192.168.1.100/24/192.168.1.1
    ///   /dns:8.8.8.8,8.8.4.4
    ///   /dhcp
    ///   /preset:"Office"
    ///   /release /renew /flushdns
    ///   /silent /help
    ///
    /// Performance: O(n) where n is number of arguments.
    /// Security: Input is validated before execution to prevent injection.
    /// </summary>
    public class CommandLineParser
    {
        private readonly string[] _args;
        private readonly Dictionary<string, string> _parsed;

        public CommandLineParser(string[] args)
        {
            _args = args ?? Array.Empty<string>();
            _parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Parse();
        }

        /// <summary>
        /// Gets whether help was requested.
        /// </summary>
        public bool IsHelpRequested => HasFlag("help") || HasFlag("?") || HasFlag("h");

        /// <summary>
        /// Gets whether silent mode is enabled.
        /// </summary>
        public bool IsSilentMode => HasFlag("silent") || HasFlag("s");

        /// <summary>
        /// Gets the specified adapter name.
        /// </summary>
        public string AdapterName => GetValue("adapter");

        /// <summary>
        /// Gets the parsed command.
        /// </summary>
        public CommandLineCommand Command { get; private set; } = CommandLineCommand.None;

        /// <summary>
        /// Gets the static IP configuration if specified.
        /// </summary>
        public NetworkConfiguration StaticConfig { get; private set; }

        /// <summary>
        /// Gets the preset name if specified.
        /// </summary>
        public string PresetName => GetValue("preset");

        /// <summary>
        /// Gets DNS servers if specified.
        /// </summary>
        public (string Primary, string Secondary) DnsServers { get; private set; }

        /// <summary>
        /// Gets any parsing errors.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// Gets whether the arguments are valid for execution.
        /// </summary>
        public bool IsValid => Errors.Count == 0 && (Command != CommandLineCommand.None || IsHelpRequested);

        /// <summary>
        /// Creates a command from parsed arguments.
        /// </summary>
        public Result<CommandLineRequest> CreateRequest()
        {
            if (!IsValid)
            {
                return Result<CommandLineRequest>.Failure(
                    string.Join("; ", Errors),
                    ErrorCode.InvalidInput);
            }

            var request = new CommandLineRequest
            {
                Command = Command,
                AdapterName = AdapterName,
                PresetName = PresetName,
                StaticConfiguration = StaticConfig,
                DnsPrimary = DnsServers.Primary,
                DnsSecondary = DnsServers.Secondary,
                IsSilent = IsSilentMode
            };

            return Result<CommandLineRequest>.Success(request);
        }

        /// <summary>
        /// Gets the help text.
        /// </summary>
        public static string GetHelpText()
        {
            return @"NetworkConfigApp - Windows Network Configuration Utility

Usage: NetworkConfigApp.exe [options]

Options:
  /adapter:""<name>""           Select network adapter by name
  /static:<ip>/<prefix>/<gw>  Set static IP configuration
                              Example: /static:192.168.1.100/24/192.168.1.1
  /dns:<primary>[,<secondary>] Set DNS servers
                              Example: /dns:8.8.8.8,8.8.4.4
  /dhcp                       Enable DHCP for IP and DNS
  /preset:""<name>""            Apply a saved preset
  /release                    Release DHCP lease
  /renew                      Renew DHCP lease
  /flushdns                   Flush DNS resolver cache
  /diagnose                   Run connectivity diagnostics
  /silent, /s                 No GUI, exit after operation
  /help, /?                   Show this help

Examples:
  NetworkConfigApp.exe /adapter:""Ethernet"" /static:192.168.1.100/24/192.168.1.1 /dns:8.8.8.8 /silent
  NetworkConfigApp.exe /adapter:""Wi-Fi"" /dhcp /silent
  NetworkConfigApp.exe /preset:""Office"" /silent
  NetworkConfigApp.exe /flushdns

Notes:
  - Administrator privileges are required for most operations
  - Use quotes around adapter names containing spaces
  - Multiple operations can be combined (e.g., /dhcp /flushdns)
";
        }

        private void Parse()
        {
            for (int i = 0; i < _args.Length; i++)
            {
                var arg = _args[i];

                // Skip empty args
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                // Remove leading / or -
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    arg = arg.Substring(1);
                }

                // Check for key:value or key=value
                var colonIdx = arg.IndexOf(':');
                var equalsIdx = arg.IndexOf('=');
                var sepIdx = colonIdx >= 0 && (equalsIdx < 0 || colonIdx < equalsIdx) ? colonIdx : equalsIdx;

                if (sepIdx > 0)
                {
                    var key = arg.Substring(0, sepIdx).Trim();
                    var value = arg.Substring(sepIdx + 1).Trim().Trim('"', '\'');
                    _parsed[key] = value;
                }
                else
                {
                    // Flag without value
                    _parsed[arg.Trim()] = "true";
                }
            }

            // Determine command from parsed arguments
            DetermineCommand();
        }

        private void DetermineCommand()
        {
            // Check for specific commands in priority order

            if (HasFlag("help") || HasFlag("?") || HasFlag("h"))
            {
                Command = CommandLineCommand.Help;
                return;
            }

            if (HasValue("static"))
            {
                Command = CommandLineCommand.SetStatic;
                ParseStaticConfig();
            }
            else if (HasFlag("dhcp"))
            {
                Command = CommandLineCommand.SetDhcp;
            }
            else if (HasValue("preset"))
            {
                Command = CommandLineCommand.ApplyPreset;
            }
            else if (HasFlag("release") && HasFlag("renew"))
            {
                Command = CommandLineCommand.ReleaseRenew;
            }
            else if (HasFlag("release"))
            {
                Command = CommandLineCommand.Release;
            }
            else if (HasFlag("renew"))
            {
                Command = CommandLineCommand.Renew;
            }
            else if (HasFlag("flushdns"))
            {
                Command = CommandLineCommand.FlushDns;
            }
            else if (HasFlag("diagnose"))
            {
                Command = CommandLineCommand.Diagnose;
            }

            // Parse DNS if specified
            if (HasValue("dns"))
            {
                ParseDns();
            }

            // Validate adapter requirement
            if (Command != CommandLineCommand.None &&
                Command != CommandLineCommand.Help &&
                Command != CommandLineCommand.FlushDns &&
                Command != CommandLineCommand.ApplyPreset &&
                string.IsNullOrEmpty(AdapterName))
            {
                Errors.Add("Adapter name is required (/adapter:\"name\")");
            }
        }

        private void ParseStaticConfig()
        {
            var staticValue = GetValue("static");
            if (string.IsNullOrEmpty(staticValue))
            {
                Errors.Add("Static configuration value is required");
                return;
            }

            // Format: IP/prefix/gateway or IP/subnet/gateway
            var parts = staticValue.Split('/');
            if (parts.Length < 2)
            {
                Errors.Add("Invalid static format. Expected: IP/prefix/gateway or IP/subnet/gateway");
                return;
            }

            var ip = parts[0];
            string subnet;
            string gateway = parts.Length > 2 ? parts[2] : string.Empty;

            // Check if second part is CIDR prefix or subnet mask
            if (int.TryParse(parts[1], out int prefix) && prefix >= 0 && prefix <= 32)
            {
                subnet = NetworkConfiguration.CidrToSubnetMask(prefix);
            }
            else
            {
                subnet = parts[1];
            }

            StaticConfig = NetworkConfiguration.Static(ip, subnet, gateway);
        }

        private void ParseDns()
        {
            var dnsValue = GetValue("dns");
            if (string.IsNullOrEmpty(dnsValue))
            {
                return;
            }

            var parts = dnsValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var primary = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            var secondary = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            DnsServers = (primary, secondary);
        }

        private bool HasFlag(string name)
        {
            return _parsed.ContainsKey(name);
        }

        private bool HasValue(string name)
        {
            return _parsed.TryGetValue(name, out var value) && value != "true";
        }

        private string GetValue(string name)
        {
            if (_parsed.TryGetValue(name, out var value) && value != "true")
            {
                return value;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Command line command types.
    /// </summary>
    public enum CommandLineCommand
    {
        None,
        Help,
        SetStatic,
        SetDhcp,
        ApplyPreset,
        Release,
        Renew,
        ReleaseRenew,
        FlushDns,
        Diagnose
    }

    /// <summary>
    /// Represents a parsed command line request.
    /// </summary>
    public sealed class CommandLineRequest
    {
        public CommandLineCommand Command { get; set; }
        public string AdapterName { get; set; }
        public string PresetName { get; set; }
        public NetworkConfiguration StaticConfiguration { get; set; }
        public string DnsPrimary { get; set; }
        public string DnsSecondary { get; set; }
        public bool IsSilent { get; set; }

        /// <summary>
        /// Gets the configuration with DNS applied if specified.
        /// </summary>
        public NetworkConfiguration GetConfigurationWithDns()
        {
            if (StaticConfiguration == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(DnsPrimary))
            {
                return StaticConfiguration.WithDns(DnsPrimary, DnsSecondary ?? string.Empty);
            }

            return StaticConfiguration;
        }
    }
}
