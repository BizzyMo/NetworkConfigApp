using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Help form displaying comprehensive usage instructions.
    ///
    /// Algorithm: Tab-based organization of help content.
    /// Each tab contains a RichTextBox with formatted help text.
    ///
    /// Performance: Content is generated once at form creation.
    /// No external resources or file loading required.
    /// </summary>
    public class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "User Guide - Network Configuration App";
            Size = new Size(650, 550);
            MinimumSize = new Size(500, 400);
            StartPosition = FormStartPosition.CenterParent;
            Icon = SystemIcons.Question;

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 5)
            };

            // Tab 1: Getting Started
            var tabStart = new TabPage("Getting Started");
            tabStart.Controls.Add(CreateContentPanel(GetGettingStartedText()));

            // Tab 2: Configuration
            var tabConfig = new TabPage("Configuration");
            tabConfig.Controls.Add(CreateContentPanel(GetConfigurationText()));

            // Tab 3: Presets
            var tabPresets = new TabPage("Presets");
            tabPresets.Controls.Add(CreateContentPanel(GetPresetsText()));

            // Tab 4: Advanced
            var tabAdvanced = new TabPage("Advanced");
            tabAdvanced.Controls.Add(CreateContentPanel(GetAdvancedText()));

            // Tab 5: Keyboard Shortcuts
            var tabShortcuts = new TabPage("Shortcuts");
            tabShortcuts.Controls.Add(CreateContentPanel(GetShortcutsText()));

            tabControl.TabPages.AddRange(new TabPage[] { tabStart, tabConfig, tabPresets, tabAdvanced, tabShortcuts });
            Controls.Add(tabControl);

            // Close button
            var btnClose = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnClose.Location = new Point(ClientSize.Width - btnClose.Width - 15, ClientSize.Height - btnClose.Height - 10);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Controls.Add(btnClose);
            btnClose.BringToFront();

            AcceptButton = btnClose;
        }

        private Panel CreateContentPanel(string content)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            var richText = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Window,
                Font = new Font("Segoe UI", 10),
                Text = content
            };

            panel.Controls.Add(richText);
            return panel;
        }

        private string GetGettingStartedText()
        {
            return @"GETTING STARTED
================

Welcome to Network Configuration App!

This utility helps you manage network adapter settings quickly and easily.


REQUIREMENTS
------------
• Windows 10 or Windows 11
• Administrator privileges (required for network changes)


RUNNING AS ADMINISTRATOR
------------------------
The application requires administrator privileges to modify network settings.
When you launch the app, Windows will prompt you to allow elevated access.

You can check your admin status in the bottom-right corner of the status bar:
• 'Admin: Yes' (green) - Full functionality available
• 'Admin: No' (red) - Limited functionality, some features won't work


INTERFACE OVERVIEW
------------------
1. ADAPTER SELECTION
   Select your network adapter from the dropdown. Active adapters are marked.
   Click 'Refresh' to update the adapter list.

2. CURRENT CONFIGURATION (Left Panel)
   Shows the current IP settings of the selected adapter (read-only).
   Displays whether DHCP is enabled or static IP is configured.

3. NEW CONFIGURATION (Right Panel)
   Enter the new IP settings you want to apply.
   Use 'Copy Current' to start with existing values.
   Use 'Google' or 'Quad9' buttons to quickly set DNS servers.

4. ACTION BUTTONS
   Apply your changes using the operation buttons.

5. LOG / STATUS
   Monitor operations and view detailed status messages.


FIRST STEPS
-----------
1. Select your network adapter from the dropdown
2. Review the current configuration
3. Enter new settings in the 'New Configuration' panel
4. Click 'Apply Static' to apply changes, or 'Set DHCP' for automatic configuration
";
        }

        private string GetConfigurationText()
        {
            return @"NETWORK CONFIGURATION
=====================


SETTING A STATIC IP ADDRESS
---------------------------
1. Select the network adapter you want to configure
2. In the 'New Configuration' panel, enter:
   • IP Address: Your desired IP (e.g., 192.168.1.100)
   • Subnet Mask: Usually 255.255.255.0 for home networks
   • Gateway: Your router's IP (e.g., 192.168.1.1)
   • DNS 1 & DNS 2: DNS server addresses

3. Click 'Apply Static' to apply the configuration

TIPS:
• Use the 'Copy Current' button to start with your existing settings
• The IP address field validates input - red background means invalid
• Leave Gateway empty if not needed (local network only)


ENABLING DHCP (Automatic Configuration)
---------------------------------------
DHCP lets your router automatically assign IP settings.

1. Select the network adapter
2. Click 'Set DHCP'
3. Wait for the operation to complete
4. Your adapter will obtain settings from your router


RELEASE / RENEW DHCP
--------------------
If you're having connectivity issues with DHCP:
1. Click 'Release/Renew' to request a fresh IP lease
2. This is useful when changing networks or troubleshooting


FLUSH DNS CACHE
---------------
Clears your computer's DNS cache. Useful when:
• Websites aren't loading correctly
• You've changed DNS servers
• DNS records have changed but aren't updating


DNS SERVER OPTIONS
------------------
Quick-select buttons are available for popular DNS providers:

GOOGLE DNS (Click 'Google' button)
• Primary: 8.8.8.8
• Secondary: 8.8.4.4
• Fast, reliable, global coverage

QUAD9 DNS (Click 'Quad9' button)
• Primary: 9.9.9.9
• Secondary: 149.112.112.112
• Privacy-focused, blocks malicious domains


TEST CONNECTIVITY
-----------------
Click 'Test Connectivity' to verify your network connection:
• Gateway test - checks connection to your router
• DNS test - checks DNS server response
• Internet test - checks external connectivity
";
        }

        private string GetPresetsText()
        {
            return @"PRESETS & PROFILES
==================

Presets let you save and quickly switch between different network configurations.


SAVING A PRESET
---------------
1. Configure your desired settings in the 'New Configuration' panel
2. Click 'Save Preset'
3. Enter a descriptive name (e.g., 'Home WiFi', 'Office Static')
4. Click OK

The preset will be saved to your user profile.


LOADING A PRESET
----------------
1. Click 'Load Preset'
2. Select the preset from the list
3. Click OK

The preset's settings will be loaded into the 'New Configuration' panel.
Click 'Apply Static' to actually apply them to your adapter.


MANAGING PRESETS
----------------
Go to Presets menu > Manage Presets to:
• View all saved presets
• Delete presets you no longer need
• See preset details


IMPORT / EXPORT PRESETS
-----------------------
Share presets between computers or create backups:

EXPORT:
• File menu > Export Presets
• Choose a location and filename
• Presets are saved as a JSON file

IMPORT:
• File menu > Import Presets
• Select the JSON file
• Imported presets are added to your collection


BACKUP & RESTORE
----------------
The app can automatically backup your configuration before changes.

AUTO-BACKUP:
• Enable 'Auto-backup before changes' checkbox
• A backup is created each time you apply new settings

MANUAL BACKUP:
• Click 'Backup Now' to create a backup of current settings

RESTORE:
• Click 'Restore' to view available backups
• Select a backup and click OK to load its settings


UNDO
----
Made a mistake? Click 'Undo' to restore the previous configuration.
This works immediately after applying changes.
";
        }

        private string GetAdvancedText()
        {
            return @"ADVANCED FEATURES
=================


MAC ADDRESS CHANGER
-------------------
Change your network adapter's MAC (hardware) address.

ACCESS: Tools menu > MAC Address Changer

FEATURES:
• View current MAC address and manufacturer
• Generate random MAC addresses
• Select from known manufacturer prefixes
• Restore original MAC address

WARNING: Changing MAC address temporarily disconnects your network.
Some networks use MAC filtering - changing it may prevent connection.


NETWORK DIAGNOSTICS
-------------------
Run detailed network tests to troubleshoot connectivity issues.

ACCESS: Tools menu > Diagnostics

TESTS AVAILABLE:
• Ping test - test connectivity to any IP or hostname
• Traceroute - trace the path to a destination
• DNS lookup - resolve hostnames
• Gateway check - verify router connectivity


COMMAND LINE INTERFACE (CLI)
----------------------------
Run the app from command line for automation and scripting.

SYNTAX:
  NetworkConfigApp.exe [options]

OPTIONS:
  /adapter:""Name""         Select adapter by name
  /static:IP/Prefix/GW    Set static IP (e.g., /static:192.168.1.100/24/192.168.1.1)
  /dns:Primary,Secondary  Set DNS servers
  /dhcp                   Enable DHCP
  /preset:""Name""          Apply a saved preset
  /release                Release DHCP lease
  /renew                  Renew DHCP lease
  /flushdns               Flush DNS cache
  /silent                 No GUI, exit after operation

EXAMPLES:
  NetworkConfigApp.exe /adapter:""Ethernet"" /dhcp /silent
  NetworkConfigApp.exe /adapter:""Wi-Fi"" /static:10.0.0.50/24/10.0.0.1 /dns:8.8.8.8,8.8.4.4


SYSTEM TRAY
-----------
The app can minimize to the system tray:
• Double-click the tray icon to restore the window
• Right-click for quick actions (Set DHCP, Flush DNS, Exit)

Enable 'Minimize to tray' in Settings to use this feature.


SETTINGS
--------
ACCESS: Tools menu > Settings

OPTIONS:
• Theme: Light or Dark mode
• Auto-backup: Enable automatic backups
• Flush DNS after changes: Automatically clear DNS cache
• Test after apply: Run connectivity test after changes
• Minimize to tray: Enable system tray behavior
• Log level: Control logging verbosity
";
        }

        private string GetShortcutsText()
        {
            return @"KEYBOARD SHORTCUTS
==================


MAIN OPERATIONS
---------------
Ctrl + A        Apply Static Configuration
Ctrl + D        Set DHCP
Ctrl + R        Release / Renew DHCP
Ctrl + F        Flush DNS Cache
Ctrl + T        Test Connectivity


PRESETS
-------
Ctrl + S        Save Preset
Ctrl + L        Load Preset
Ctrl + Z        Undo (restore previous config)


HELP
----
F1              Open User Guide (this window)


MENU ACCESS
-----------
Alt + F         File menu
Alt + P         Presets menu
Alt + T         Tools menu
Alt + H         Help menu


DIALOG SHORTCUTS
----------------
Enter           Confirm / OK
Escape          Cancel / Close


TIPS
----
• All main operation buttons have tooltips - hover to see the shortcut
• Keyboard shortcuts work when the main window is focused
• Use Tab to navigate between input fields
";
        }
    }
}
