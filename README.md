# NetworkConfigApp

A portable Windows network configuration utility with Windows Forms GUI. Manage IP addresses, DNS servers, network adapters, presets, and more.

## Features

- **Network Adapter Management**
  - Auto-detect all network adapters
  - Active adapter indication (connected with gateway)
  - Display adapter details: name, type, status, MAC, speed

- **IP Configuration**
  - View current configuration (IP, subnet, gateway, DNS)
  - Apply static IP configuration
  - Enable DHCP (automatic)
  - Release/Renew DHCP lease
  - Flush DNS cache
  - Real-time input validation

- **Presets & Profiles**
  - Save/load common configurations (Home, Office, etc.)
  - DPAPI encryption for sensitive presets
  - Import/Export presets to JSON

- **Backup & Restore**
  - Automatic backup before changes
  - Manual backup creation
  - Restore from any backup
  - Configurable retention

- **Network Diagnostics**
  - Ping test
  - Traceroute
  - DNS resolution test
  - Comprehensive connectivity test

- **MAC Address Spoofing**
  - View current/permanent MAC
  - Generate random MAC
  - Select manufacturer (OUI database)
  - Restore original MAC

- **Command Line Interface**
  - Batch mode for automation
  - Silent operation
  - All features accessible via CLI

## Requirements

- Windows 10/11
- .NET Framework 4.8 (pre-installed on Windows 10/11)
- Administrator privileges (for network changes)

## Installation

No installation required. Download and run `NetworkConfigApp.exe`.

For portable mode, create a `portable.txt` file next to the executable to store settings locally instead of in AppData.

## Usage

### GUI Mode

Simply run `NetworkConfigApp.exe`. The application will request administrator privileges if needed.

**Keyboard Shortcuts:**
- `Ctrl+A` - Apply static configuration
- `Ctrl+D` - Set DHCP
- `Ctrl+R` - Release/Renew
- `Ctrl+F` - Flush DNS
- `Ctrl+T` - Test connectivity
- `Ctrl+S` - Save preset
- `Ctrl+L` - Load preset
- `Ctrl+Z` - Undo last change

### Command Line Mode

```bash
# Set static IP
NetworkConfigApp.exe /adapter:"Ethernet" /static:192.168.1.100/24/192.168.1.1 /dns:8.8.8.8,8.8.4.4 /silent

# Enable DHCP
NetworkConfigApp.exe /adapter:"Wi-Fi" /dhcp /silent

# Apply preset
NetworkConfigApp.exe /preset:"Office" /silent

# Release and renew DHCP
NetworkConfigApp.exe /adapter:"Ethernet" /release /renew /silent

# Flush DNS
NetworkConfigApp.exe /flushdns

# Run diagnostics
NetworkConfigApp.exe /adapter:"Ethernet" /diagnose

# Show help
NetworkConfigApp.exe /help
```

**CLI Options:**
| Option | Description |
|--------|-------------|
| `/adapter:"<name>"` | Select network adapter |
| `/static:<ip>/<prefix>/<gw>` | Set static IP (e.g., `/static:192.168.1.100/24/192.168.1.1`) |
| `/dns:<primary>[,<secondary>]` | Set DNS servers |
| `/dhcp` | Enable DHCP |
| `/preset:"<name>"` | Apply saved preset |
| `/release` | Release DHCP lease |
| `/renew` | Renew DHCP lease |
| `/flushdns` | Flush DNS cache |
| `/diagnose` | Run connectivity diagnostics |
| `/silent`, `/s` | No GUI, exit after operation |
| `/help`, `/?` | Show help |

## Building from Source

```bash
# Clone the repository
git clone https://github.com/BizzyMo/NetworkConfigApp.git
cd NetworkConfigApp

# Build
dotnet build -c Release

# Run tests
dotnet test

# Publish single executable
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## Project Structure

```
NetworkConfigApp/
├── src/
│   ├── NetworkConfigApp/           # WinForms UI
│   │   ├── Forms/                  # Form classes
│   │   ├── Controls/               # Custom controls
│   │   └── Resources/              # Icons, OUI database
│   └── NetworkConfigApp.Core/      # Business logic
│       ├── Models/                 # Data models
│       ├── Services/               # Network services
│       ├── Validators/             # Input validation
│       ├── Commands/               # CLI parsing
│       └── Utilities/              # Helpers
└── tests/
    └── NetworkConfigApp.Tests/     # Unit tests
```

## Storage Locations

**Standard Mode:**
```
%APPDATA%\NetworkConfigApp\
├── presets/      # Saved presets
├── backups/      # Configuration backups
├── logs/         # Application logs
└── settings.json # User settings
```

**Portable Mode:**
Settings stored next to the executable when `portable.txt` exists.

## Security Considerations

- **Administrator Privileges**: Required for network configuration changes
- **MAC Spoofing**: Modifies Windows Registry; use responsibly
- **Preset Encryption**: Uses Windows DPAPI (user-scoped)
- **No Credentials Storage**: Only stores network configuration data

## License

MIT License - See [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## Changelog

### v1.0.0
- Initial release
- Full GUI with all features
- Command line support
- Preset management with encryption
- Backup/restore functionality
- MAC address spoofing
- Network diagnostics

## Support

- [GitHub Issues](https://github.com/BizzyMo/NetworkConfigApp/issues)

---

Made with care by [BizzyMo](https://github.com/BizzyMo)
