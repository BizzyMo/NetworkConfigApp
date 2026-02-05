using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;
using NetworkConfigApp.Core.Utilities;
using NetworkConfigApp.Core.Validators;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Main application form with network configuration controls.
    ///
    /// Algorithm: Event-driven UI with async operations for network changes.
    /// Uses services for actual network operations to maintain separation of concerns.
    ///
    /// Performance: All network operations are async to keep UI responsive.
    /// Adapter list is cached and refreshed on demand.
    ///
    /// Security: Admin status is checked before operations.
    /// Input is validated before any network changes.
    /// </summary>
    public partial class MainForm : Form
    {
        // Services
        private readonly IAdapterService _adapterService;
        private readonly INetworkService _networkService;
        private readonly IPresetService _presetService;
        private readonly IBackupService _backupService;
        private readonly IMacService _macService;

        // State
        private IReadOnlyList<NetworkAdapter> _adapters;
        private NetworkAdapter _selectedAdapter;
        private NetworkConfiguration _previousConfig;
        private AppSettings _settings;
        private bool _isLoading;
        private CancellationTokenSource _operationCts;

        // UI Controls (declared here, initialized in designer)
        private ComboBox cmbAdapters;
        private Button btnRefresh;
        private Label lblAdapterInfo;

        // Current config (read-only)
        private TextBox txtCurrentIp;
        private TextBox txtCurrentSubnet;
        private TextBox txtCurrentGateway;
        private TextBox txtCurrentDns1;
        private TextBox txtCurrentDns2;
        private Label lblCurrentDhcp;

        // New config (editable)
        private TextBox txtNewIp;
        private TextBox txtNewSubnet;
        private TextBox txtNewGateway;
        private TextBox txtNewDns1;
        private TextBox txtNewDns2;

        // Buttons
        private Button btnApplyStatic;
        private Button btnSetDhcp;
        private Button btnReleaseRenew;
        private Button btnFlushDns;
        private Button btnTestConnectivity;
        private Button btnSavePreset;
        private Button btnLoadPreset;
        private Button btnUndo;
        private Button btnBackup;
        private Button btnRestore;

        // Options
        private CheckBox chkAutoBackup;
        private CheckBox chkFlushDnsAfter;
        private CheckBox chkTestAfter;
        private CheckBox chkLogOperations;

        // Status
        private TextBox txtLog;
        private ProgressBar progressBar;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripStatusLabel lblAdapter;
        private ToolStripStatusLabel lblIp;
        private ToolStripStatusLabel lblAdmin;

        // Menu
        private MenuStrip menuStrip;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;

        public MainForm()
        {
            // Initialize services
            _adapterService = new AdapterService();
            _networkService = new NetworkService();
            _presetService = new PresetService();
            _backupService = new BackupService();
            _macService = new MacService();

            _settings = SettingsService.Instance.GetSettings();

            InitializeComponent();
            InitializeCustomComponents();
            ApplyTheme();

            // Wire up events
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Form settings
            Text = "Network Configuration";
            Size = new Size(700, 750);
            MinimumSize = new Size(650, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;
            KeyPreview = true;

            // Main menu
            menuStrip = new MenuStrip();
            CreateMenuStrip();

            // Main container
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Adapter selection group
            var grpAdapter = CreateAdapterGroup();
            grpAdapter.Location = new Point(10, 30);
            grpAdapter.Size = new Size(660, 70);

            // Current config group
            var grpCurrent = CreateCurrentConfigGroup();
            grpCurrent.Location = new Point(10, 110);
            grpCurrent.Size = new Size(320, 170);

            // New config group
            var grpNew = CreateNewConfigGroup();
            grpNew.Location = new Point(350, 110);
            grpNew.Size = new Size(320, 170);

            // Primary buttons
            var pnlPrimaryButtons = CreatePrimaryButtonsPanel();
            pnlPrimaryButtons.Location = new Point(10, 290);
            pnlPrimaryButtons.Size = new Size(660, 40);

            // Secondary buttons
            var pnlSecondaryButtons = CreateSecondaryButtonsPanel();
            pnlSecondaryButtons.Location = new Point(10, 340);
            pnlSecondaryButtons.Size = new Size(660, 40);

            // Options
            var grpOptions = CreateOptionsGroup();
            grpOptions.Location = new Point(10, 390);
            grpOptions.Size = new Size(660, 50);

            // Log/status area
            var grpLog = CreateLogGroup();
            grpLog.Location = new Point(10, 450);
            grpLog.Size = new Size(660, 200);

            // Status bar
            statusStrip = CreateStatusStrip();

            // Add all controls
            mainPanel.Controls.AddRange(new Control[]
            {
                grpAdapter, grpCurrent, grpNew,
                pnlPrimaryButtons, pnlSecondaryButtons,
                grpOptions, grpLog
            });

            Controls.Add(mainPanel);
            Controls.Add(menuStrip);
            Controls.Add(statusStrip);
            MainMenuStrip = menuStrip;

            // System tray
            CreateTrayIcon();

            ResumeLayout(true);
        }

        private void CreateMenuStrip()
        {
            // File menu
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("&Import Presets...", null, (s, e) => ImportPresets());
            fileMenu.DropDownItems.Add("&Export Presets...", null, (s, e) => ExportPresets());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Export &Log...", null, (s, e) => ExportLog());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());

            // Presets menu
            var presetsMenu = new ToolStripMenuItem("&Presets");
            presetsMenu.DropDownItems.Add("&Save Current...", null, async (s, e) => await SavePreset());
            presetsMenu.DropDownItems.Add("&Load Preset...", null, async (s, e) => await LoadPreset());
            presetsMenu.DropDownItems.Add(new ToolStripSeparator());
            presetsMenu.DropDownItems.Add("&Manage Presets...", null, (s, e) => ManagePresets());

            // Tools menu
            var toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add("&Diagnostics...", null, (s, e) => ShowDiagnostics());
            toolsMenu.DropDownItems.Add("&MAC Address Changer...", null, (s, e) => ShowMacChanger());
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            toolsMenu.DropDownItems.Add("&Settings...", null, (s, e) => ShowSettings());

            // Help menu
            var helpMenu = new ToolStripMenuItem("&Help");
            var menuUserGuide = new ToolStripMenuItem("&User Guide...", null, (s, e) => ShowHelp());
            menuUserGuide.ShortcutKeys = Keys.F1;
            helpMenu.DropDownItems.Add(menuUserGuide);
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&About...", null, (s, e) => ShowAbout());

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, presetsMenu, toolsMenu, helpMenu });
        }

        private GroupBox CreateAdapterGroup()
        {
            var group = new GroupBox { Text = "Network Adapter" };

            var lblAdapter = new Label { Text = "Adapter:", Location = new Point(10, 22), AutoSize = true };

            cmbAdapters = new ComboBox
            {
                Location = new Point(70, 19),
                Size = new Size(400, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbAdapters.SelectedIndexChanged += CmbAdapters_SelectedIndexChanged;

            btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(480, 18),
                Size = new Size(75, 25)
            };
            btnRefresh.Click += async (s, e) => await RefreshAdapters();
            ApplyRoundedCorners(btnRefresh, 6);

            lblAdapterInfo = new Label
            {
                Location = new Point(10, 48),
                Size = new Size(640, 18),
                ForeColor = Color.DarkGray
            };

            group.Controls.AddRange(new Control[] { lblAdapter, cmbAdapters, btnRefresh, lblAdapterInfo });
            return group;
        }

        private GroupBox CreateCurrentConfigGroup()
        {
            var group = new GroupBox { Text = "Current Configuration (Read-Only)" };

            var y = 20;
            var lblIp = new Label { Text = "IP Address:", Location = new Point(10, y + 3), AutoSize = true };
            txtCurrentIp = new TextBox { Location = new Point(90, y), Size = new Size(120, 23), ReadOnly = true };
            lblCurrentDhcp = new Label { Location = new Point(215, y + 3), AutoSize = true, ForeColor = Color.Blue };

            y += 28;
            var lblSubnet = new Label { Text = "Subnet:", Location = new Point(10, y + 3), AutoSize = true };
            txtCurrentSubnet = new TextBox { Location = new Point(90, y), Size = new Size(120, 23), ReadOnly = true };

            y += 28;
            var lblGw = new Label { Text = "Gateway:", Location = new Point(10, y + 3), AutoSize = true };
            txtCurrentGateway = new TextBox { Location = new Point(90, y), Size = new Size(120, 23), ReadOnly = true };

            y += 28;
            var lblDns1 = new Label { Text = "DNS 1:", Location = new Point(10, y + 3), AutoSize = true };
            txtCurrentDns1 = new TextBox { Location = new Point(90, y), Size = new Size(120, 23), ReadOnly = true };

            y += 28;
            var lblDns2 = new Label { Text = "DNS 2:", Location = new Point(10, y + 3), AutoSize = true };
            txtCurrentDns2 = new TextBox { Location = new Point(90, y), Size = new Size(120, 23), ReadOnly = true };

            group.Controls.AddRange(new Control[] {
                lblIp, txtCurrentIp, lblCurrentDhcp,
                lblSubnet, txtCurrentSubnet,
                lblGw, txtCurrentGateway,
                lblDns1, txtCurrentDns1,
                lblDns2, txtCurrentDns2
            });

            return group;
        }

        private GroupBox CreateNewConfigGroup()
        {
            var group = new GroupBox { Text = "New Configuration" };

            var y = 20;
            var lblIp = new Label { Text = "IP Address:", Location = new Point(10, y + 3), AutoSize = true };
            txtNewIp = new TextBox { Location = new Point(90, y), Size = new Size(140, 23) };
            txtNewIp.TextChanged += ValidateInputs;

            y += 28;
            var lblSubnet = new Label { Text = "Subnet:", Location = new Point(10, y + 3), AutoSize = true };
            txtNewSubnet = new TextBox { Location = new Point(90, y), Size = new Size(140, 23), Text = "255.255.255.0" };
            txtNewSubnet.TextChanged += ValidateInputs;

            y += 28;
            var lblGw = new Label { Text = "Gateway:", Location = new Point(10, y + 3), AutoSize = true };
            txtNewGateway = new TextBox { Location = new Point(90, y), Size = new Size(140, 23) };
            txtNewGateway.TextChanged += ValidateInputs;

            y += 28;
            var lblDns1 = new Label { Text = "DNS 1:", Location = new Point(10, y + 3), AutoSize = true };
            txtNewDns1 = new TextBox { Location = new Point(90, y), Size = new Size(140, 23), Text = "8.8.8.8" };
            txtNewDns1.TextChanged += ValidateInputs;

            y += 28;
            var lblDns2 = new Label { Text = "DNS 2:", Location = new Point(10, y + 3), AutoSize = true };
            txtNewDns2 = new TextBox { Location = new Point(90, y), Size = new Size(140, 23), Text = "8.8.4.4" };
            txtNewDns2.TextChanged += ValidateInputs;

            // Copy from current button
            var btnCopy = new Button { Text = "Copy Current", Location = new Point(235, 20), Size = new Size(80, 25) };
            btnCopy.Click += (s, e) => CopyCurrentToNew();

            // DNS quick-select buttons
            var btnGoogleDns = new Button
            {
                Text = "Google",
                Location = new Point(235, 104),
                Size = new Size(60, 23),
                Tag = "dns-preset",
                FlatStyle = FlatStyle.Flat
            };
            btnGoogleDns.Click += (s, e) =>
            {
                txtNewDns1.Text = "8.8.8.8";
                txtNewDns2.Text = "8.8.4.4";
            };

            var btnQuad9Dns = new Button
            {
                Text = "Quad9",
                Location = new Point(235, 132),
                Size = new Size(60, 23),
                Tag = "dns-preset",
                FlatStyle = FlatStyle.Flat
            };
            btnQuad9Dns.Click += (s, e) =>
            {
                txtNewDns1.Text = "9.9.9.9";
                txtNewDns2.Text = "149.112.112.112";
            };

            // Apply rounded corners to all buttons in this group
            ApplyRoundedCorners(btnCopy, 6);
            ApplyRoundedCorners(btnGoogleDns, 6);
            ApplyRoundedCorners(btnQuad9Dns, 6);

            group.Controls.AddRange(new Control[] {
                lblIp, txtNewIp,
                lblSubnet, txtNewSubnet,
                lblGw, txtNewGateway,
                lblDns1, txtNewDns1,
                lblDns2, txtNewDns2,
                btnCopy,
                btnGoogleDns,
                btnQuad9Dns
            });

            return group;
        }

        private Panel CreatePrimaryButtonsPanel()
        {
            var panel = new Panel();

            // Calculate centered positions (panel width 660, 5 buttons with gaps)
            // Buttons: 100 + 100 + 110 + 100 + 120 = 530, gaps: 4 * 10 = 40, total = 570
            var startX = (660 - 570) / 2;  // Center offset = 45

            btnApplyStatic = new Button { Text = "Apply Static", Location = new Point(startX, 5), Size = new Size(100, 30) };
            btnApplyStatic.Click += async (s, e) => await ApplyStaticConfig();

            btnSetDhcp = new Button { Text = "Set DHCP", Location = new Point(startX + 110, 5), Size = new Size(100, 30) };
            btnSetDhcp.Click += async (s, e) => await SetDhcp();

            btnReleaseRenew = new Button { Text = "Release/Renew", Location = new Point(startX + 220, 5), Size = new Size(110, 30) };
            btnReleaseRenew.Click += async (s, e) => await ReleaseRenew();

            btnFlushDns = new Button { Text = "Flush DNS", Location = new Point(startX + 340, 5), Size = new Size(100, 30) };
            btnFlushDns.Click += async (s, e) => await FlushDns();

            btnTestConnectivity = new Button { Text = "Test Connectivity", Location = new Point(startX + 450, 5), Size = new Size(120, 30) };
            btnTestConnectivity.Click += async (s, e) => await TestConnectivity();

            // Apply rounded corners
            foreach (var btn in new[] { btnApplyStatic, btnSetDhcp, btnReleaseRenew, btnFlushDns, btnTestConnectivity })
            {
                ApplyRoundedCorners(btn, 8);
            }

            panel.Controls.AddRange(new Control[] { btnApplyStatic, btnSetDhcp, btnReleaseRenew, btnFlushDns, btnTestConnectivity });
            return panel;
        }

        private Panel CreateSecondaryButtonsPanel()
        {
            var panel = new Panel();

            // Calculate centered positions (panel width 660, 6 buttons with gaps)
            // Buttons: 100 + 100 + 80 + 100 + 100 + 80 = 560, gaps: 5 * 10 = 50, total = 610
            var startX = (660 - 610) / 2;  // Center offset = 25

            btnSavePreset = new Button { Text = "Save Preset", Location = new Point(startX, 5), Size = new Size(100, 30) };
            btnSavePreset.Click += async (s, e) => await SavePreset();

            btnLoadPreset = new Button { Text = "Load Preset", Location = new Point(startX + 110, 5), Size = new Size(100, 30) };
            btnLoadPreset.Click += async (s, e) => await LoadPreset();

            btnUndo = new Button { Text = "Undo", Location = new Point(startX + 220, 5), Size = new Size(80, 30), Enabled = false };
            btnUndo.Click += async (s, e) => await Undo();

            btnBackup = new Button { Text = "Backup Now", Location = new Point(startX + 310, 5), Size = new Size(100, 30) };
            btnBackup.Click += async (s, e) => await CreateBackup();

            btnRestore = new Button { Text = "Restore", Location = new Point(startX + 420, 5), Size = new Size(100, 30) };
            btnRestore.Click += async (s, e) => await RestoreBackup();

            var btnClose = new Button { Text = "Close", Location = new Point(startX + 530, 5), Size = new Size(80, 30) };
            btnClose.Click += (s, e) => CloseApplication();

            // Apply rounded corners
            foreach (var btn in new[] { btnSavePreset, btnLoadPreset, btnUndo, btnBackup, btnRestore, btnClose })
            {
                ApplyRoundedCorners(btn, 8);
            }

            panel.Controls.AddRange(new Control[] { btnSavePreset, btnLoadPreset, btnUndo, btnBackup, btnRestore, btnClose });
            return panel;
        }

        private GroupBox CreateOptionsGroup()
        {
            var group = new GroupBox { Text = "Options" };

            chkAutoBackup = new CheckBox
            {
                Text = "Auto-backup before changes",
                Location = new Point(10, 18),
                AutoSize = true,
                Checked = _settings.AutoBackup
            };

            chkFlushDnsAfter = new CheckBox
            {
                Text = "Flush DNS after changes",
                Location = new Point(200, 18),
                AutoSize = true,
                Checked = _settings.FlushDnsAfterChanges
            };

            chkTestAfter = new CheckBox
            {
                Text = "Test after apply",
                Location = new Point(380, 18),
                AutoSize = true,
                Checked = _settings.TestAfterApply
            };

            chkLogOperations = new CheckBox
            {
                Text = "Log operations",
                Location = new Point(510, 18),
                AutoSize = true,
                Checked = true
            };

            group.Controls.AddRange(new Control[] { chkAutoBackup, chkFlushDnsAfter, chkTestAfter, chkLogOperations });
            return group;
        }

        private GroupBox CreateLogGroup()
        {
            var group = new GroupBox { Text = "Log / Status" };

            txtLog = new TextBox
            {
                Location = new Point(10, 18),
                Size = new Size(640, 155),  // Increased height to fill box
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            progressBar = new ProgressBar
            {
                Location = new Point(10, 178),
                Size = new Size(640, 15),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            group.Controls.AddRange(new Control[] { txtLog, progressBar });
            return group;
        }

        private StatusStrip CreateStatusStrip()
        {
            var strip = new StatusStrip();

            lblStatus = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblAdapter = new ToolStripStatusLabel("Adapter: -");
            lblIp = new ToolStripStatusLabel("IP: -");
            lblAdmin = new ToolStripStatusLabel(AdminHelper.IsRunningAsAdmin() ? "Admin: Yes" : "Admin: No")
            {
                ForeColor = AdminHelper.IsRunningAsAdmin() ? Color.Green : Color.Red
            };

            strip.Items.AddRange(new ToolStripItem[] { lblStatus, lblAdapter, lblIp, lblAdmin });
            return strip;
        }

        private void CreateTrayIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Network Configuration",
                Visible = true
            };

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Set DHCP", null, async (s, e) => await SetDhcpFromTray());
            trayMenu.Items.Add("Flush DNS", null, async (s, e) => await FlushDns());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (s, e) => { notifyIcon.Visible = false; Application.Exit(); });

            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; };
        }

        private void InitializeCustomComponents()
        {
            // Add tooltips
            var toolTip = new ToolTip();
            toolTip.SetToolTip(btnApplyStatic, "Apply static IP configuration (Ctrl+A)");
            toolTip.SetToolTip(btnSetDhcp, "Enable DHCP for automatic configuration (Ctrl+D)");
            toolTip.SetToolTip(btnReleaseRenew, "Release and renew DHCP lease (Ctrl+R)");
            toolTip.SetToolTip(btnFlushDns, "Clear DNS resolver cache (Ctrl+F)");
            toolTip.SetToolTip(btnTestConnectivity, "Test network connectivity (Ctrl+T)");
            toolTip.SetToolTip(btnSavePreset, "Save current settings as a preset (Ctrl+S)");
            toolTip.SetToolTip(btnLoadPreset, "Load a saved preset (Ctrl+L)");
            toolTip.SetToolTip(btnUndo, "Restore previous configuration (Ctrl+Z)");

            // Keyboard shortcuts
            KeyDown += MainForm_KeyDown;
        }

        private void ApplyTheme()
        {
            var isDark = _settings.Theme == AppTheme.Dark;

            // Define color schemes
            Color backColor, foreColor, groupBack, textBoxBack, buttonBack, buttonFore;

            if (isDark)
            {
                backColor = Color.FromArgb(45, 45, 48);
                foreColor = Color.White;
                groupBack = Color.FromArgb(55, 55, 58);
                textBoxBack = Color.FromArgb(60, 60, 65);
                buttonBack = Color.FromArgb(0, 122, 204);
                buttonFore = Color.White;
            }
            else // Light - Modern blue/white design
            {
                backColor = Color.FromArgb(240, 245, 250);      // Light blue-gray background
                foreColor = Color.FromArgb(30, 40, 60);         // Dark slate text
                groupBack = Color.White;                         // White panels
                textBoxBack = Color.White;                       // White inputs
                buttonBack = Color.FromArgb(59, 130, 246);      // Blue buttons
                buttonFore = Color.White;                        // White button text
            }

            ApplyThemeToControl(this, backColor, foreColor, groupBack, textBoxBack, buttonBack, buttonFore, isDark);
        }

        private void ApplyThemeToControl(Control parent, Color back, Color fore, Color groupBack, Color textBack, Color btnBack, Color btnFore, bool isDark)
        {
            parent.BackColor = back;
            parent.ForeColor = fore;

            foreach (Control control in parent.Controls)
            {
                if (control is GroupBox gb)
                {
                    gb.BackColor = groupBack;
                    gb.ForeColor = fore;
                    ApplyThemeToControl(gb, groupBack, fore, groupBack, textBack, btnBack, btnFore, isDark);
                }
                else if (control is TextBox tb)
                {
                    tb.BackColor = textBack;
                    tb.ForeColor = fore;
                    if (tb.ReadOnly)
                    {
                        tb.BackColor = isDark ? Color.FromArgb(50, 50, 53) : Color.FromArgb(245, 245, 245);
                    }
                }
                else if (control is ComboBox cb)
                {
                    cb.BackColor = textBack;
                    cb.ForeColor = fore;
                }
                else if (control is Button btn)
                {
                    // DNS preset buttons get secondary styling
                    if (btn.Tag?.ToString() == "dns-preset")
                    {
                        btn.BackColor = isDark ? Color.FromArgb(70, 70, 75) : Color.FromArgb(229, 231, 235);
                        btn.ForeColor = fore;
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = isDark ? Color.FromArgb(90, 90, 95) : Color.FromArgb(203, 213, 225);
                        btn.FlatAppearance.BorderSize = 1;
                    }
                    else
                    {
                        btn.BackColor = btnBack;
                        btn.ForeColor = btnFore;
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderSize = 0;
                    }

                    // Reapply rounded corners after theme change
                    ApplyRoundedCorners(btn, btn.Height > 26 ? 8 : 6);
                }
                else if (control is CheckBox chk)
                {
                    chk.ForeColor = fore;
                }
                else if (control is Label lbl)
                {
                    // Preserve special label colors
                    if (lbl != lblCurrentDhcp && lbl != lblAdapterInfo)
                    {
                        lbl.ForeColor = fore;
                    }
                }
                else if (control is Panel pnl)
                {
                    pnl.BackColor = back;
                    ApplyThemeToControl(pnl, back, fore, groupBack, textBack, btnBack, btnFore, isDark);
                }
                else if (control is MenuStrip ms)
                {
                    ms.BackColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(249, 250, 251);
                    ms.ForeColor = fore;
                    foreach (ToolStripMenuItem item in ms.Items)
                    {
                        ApplyThemeToMenuItem(item, fore, isDark);
                    }
                }
                else if (control is StatusStrip ss)
                {
                    ss.BackColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(249, 250, 251);
                    foreach (ToolStripItem item in ss.Items)
                    {
                        if (item is ToolStripStatusLabel tsl && item != lblAdmin)
                        {
                            tsl.ForeColor = fore;
                        }
                    }
                }
                else if (control.HasChildren)
                {
                    ApplyThemeToControl(control, back, fore, groupBack, textBack, btnBack, btnFore, isDark);
                }
            }
        }

        private void ApplyThemeToMenuItem(ToolStripMenuItem item, Color fore, bool isDark)
        {
            item.ForeColor = fore;
            item.BackColor = isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(249, 250, 251);
            foreach (ToolStripItem subItem in item.DropDownItems)
            {
                if (subItem is ToolStripMenuItem subMenuItem)
                {
                    ApplyThemeToMenuItem(subMenuItem, fore, isDark);
                }
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await RefreshAdapters();
            LogMessage("Application started.");

            if (!AdminHelper.IsRunningAsAdmin())
            {
                LogMessage("WARNING: Running without administrator privileges. Some features may not work.");
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Always close properly - no minimize to tray on X click
            CloseApplication();
        }

        private void CloseApplication()
        {
            notifyIcon.Visible = false;
            _operationCts?.Cancel();
            Application.Exit();
        }

        private void ApplyRoundedCorners(Button btn, int radius)
        {
            var path = new GraphicsPath();
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);

            // Create rounded rectangle path
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseAllFigures();

            btn.Region = new Region(path);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A: btnApplyStatic.PerformClick(); break;
                    case Keys.D: btnSetDhcp.PerformClick(); break;
                    case Keys.R: btnReleaseRenew.PerformClick(); break;
                    case Keys.F: btnFlushDns.PerformClick(); break;
                    case Keys.T: btnTestConnectivity.PerformClick(); break;
                    case Keys.S: btnSavePreset.PerformClick(); break;
                    case Keys.L: btnLoadPreset.PerformClick(); break;
                    case Keys.Z: if (btnUndo.Enabled) btnUndo.PerformClick(); break;
                }
            }
        }

        private async Task RefreshAdapters()
        {
            SetLoading(true, "Refreshing adapters...");

            try
            {
                await _adapterService.RefreshAsync();
                var result = await _adapterService.GetAllAdaptersAsync(false, true);

                if (result.IsSuccess)
                {
                    _adapters = result.Value;
                    cmbAdapters.Items.Clear();

                    foreach (var adapter in _adapters)
                    {
                        cmbAdapters.Items.Add(adapter);
                    }

                    // Select previously selected or active adapter
                    var toSelect = _adapters.FirstOrDefault(a =>
                        !string.IsNullOrEmpty(_settings.LastAdapterName) &&
                        a.Name == _settings.LastAdapterName);

                    if (toSelect == null)
                    {
                        toSelect = _adapters.FirstOrDefault(a => a.IsActive);
                    }

                    if (toSelect != null)
                    {
                        cmbAdapters.SelectedItem = toSelect;
                    }
                    else if (cmbAdapters.Items.Count > 0)
                    {
                        cmbAdapters.SelectedIndex = 0;
                    }

                    LogMessage($"Found {_adapters.Count} network adapter(s).");
                }
                else
                {
                    LogMessage($"Error: {result.Error}");
                }
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void CmbAdapters_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            _selectedAdapter = cmbAdapters.SelectedItem as NetworkAdapter;
            if (_selectedAdapter != null)
            {
                UpdateCurrentConfig();
                UpdateStatusBar();

                // Save selection
                SettingsService.Instance.UpdateSetting(s => s.WithLastAdapterName(_selectedAdapter.Name));
            }
        }

        private void UpdateCurrentConfig()
        {
            if (_selectedAdapter == null) return;

            var config = _selectedAdapter.CurrentConfiguration;

            txtCurrentIp.Text = config.IpAddress;
            txtCurrentSubnet.Text = config.SubnetMask;
            txtCurrentGateway.Text = config.Gateway;
            txtCurrentDns1.Text = config.Dns1;
            txtCurrentDns2.Text = config.Dns2;
            lblCurrentDhcp.Text = config.IsDhcp ? "(DHCP)" : "(Static)";

            // Update adapter info
            lblAdapterInfo.Text = $"{_selectedAdapter.GetTypeDisplay()} | {_selectedAdapter.GetStatusDisplay()} | " +
                                  $"{_selectedAdapter.MacAddress} | {_selectedAdapter.GetSpeedDisplay()}";

            // Pre-fill new config if empty
            if (string.IsNullOrEmpty(txtNewIp.Text))
            {
                CopyCurrentToNew();
            }
        }

        private void CopyCurrentToNew()
        {
            if (_selectedAdapter == null) return;

            var config = _selectedAdapter.CurrentConfiguration;
            txtNewIp.Text = config.IpAddress;
            txtNewSubnet.Text = config.SubnetMask;
            txtNewGateway.Text = config.Gateway;
            txtNewDns1.Text = config.Dns1;
            txtNewDns2.Text = config.Dns2;
        }

        private void UpdateStatusBar()
        {
            if (_selectedAdapter != null)
            {
                lblAdapter.Text = $"Adapter: {_selectedAdapter.Name}";
                lblIp.Text = $"IP: {_selectedAdapter.CurrentConfiguration.IpAddress}";
            }
        }

        private void ValidateInputs(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var text = textBox.Text;
            bool isValid = true;

            if (textBox == txtNewIp)
            {
                isValid = string.IsNullOrEmpty(text) || IpAddressValidator.Validate(text).IsValid;
            }
            else if (textBox == txtNewSubnet)
            {
                isValid = string.IsNullOrEmpty(text) || SubnetValidator.Validate(text).IsValid;
            }
            else if (textBox == txtNewGateway || textBox == txtNewDns1 || textBox == txtNewDns2)
            {
                isValid = string.IsNullOrEmpty(text) || IpAddressValidator.Validate(text).IsValid;
            }

            textBox.BackColor = isValid ? SystemColors.Window : Color.MistyRose;
        }

        private async Task ApplyStaticConfig()
        {
            if (_selectedAdapter == null)
            {
                ShowError("Please select a network adapter.");
                return;
            }

            // Validate
            var ipResult = IpAddressValidator.ValidateForStatic(txtNewIp.Text);
            if (!ipResult.IsValid)
            {
                ShowError($"Invalid IP address: {ipResult.Message}");
                return;
            }

            var subnetResult = SubnetValidator.Validate(txtNewSubnet.Text);
            if (!subnetResult.IsValid)
            {
                ShowError($"Invalid subnet mask: {subnetResult.Message}");
                return;
            }

            var config = NetworkConfiguration.Static(
                txtNewIp.Text,
                txtNewSubnet.Text,
                txtNewGateway.Text,
                txtNewDns1.Text,
                txtNewDns2.Text);

            await ExecuteNetworkOperation("Applying static configuration...", async ct =>
            {
                // Auto-backup
                if (chkAutoBackup.Checked)
                {
                    await _backupService.CreateBackupAsync(_selectedAdapter.Name, _selectedAdapter.CurrentConfiguration,
                        "Before static IP change", ct);
                }

                // Save for undo
                _previousConfig = _selectedAdapter.CurrentConfiguration;
                btnUndo.Enabled = true;

                // Apply
                var result = await _networkService.ApplyStaticConfigurationAsync(_selectedAdapter.Name, config, ct);

                if (result.IsSuccess)
                {
                    LogMessage($"Static configuration applied: {config.IpAddress}/{config.GetCidrPrefix()}");
                    LoggingService.Instance?.LogConfigChange(_selectedAdapter.Name, _previousConfig, config);

                    // Flush DNS if option enabled
                    if (chkFlushDnsAfter.Checked)
                    {
                        await _networkService.FlushDnsAsync(ct);
                        LogMessage("DNS cache flushed.");
                    }

                    // Test connectivity if option enabled
                    if (chkTestAfter.Checked)
                    {
                        await TestConnectivityInternal(ct);
                    }

                    // Refresh adapter info
                    await RefreshAdapters();
                    return true;
                }

                ShowError(result.Error);
                return false;
            });
        }

        private async Task SetDhcp()
        {
            if (_selectedAdapter == null)
            {
                ShowError("Please select a network adapter.");
                return;
            }

            await ExecuteNetworkOperation("Setting DHCP...", async ct =>
            {
                if (chkAutoBackup.Checked)
                {
                    await _backupService.CreateBackupAsync(_selectedAdapter.Name, _selectedAdapter.CurrentConfiguration,
                        "Before DHCP change", ct);
                }

                _previousConfig = _selectedAdapter.CurrentConfiguration;
                btnUndo.Enabled = true;

                var result = await _networkService.SetDhcpAsync(_selectedAdapter.Name, ct);

                if (result.IsSuccess)
                {
                    LogMessage("DHCP enabled.");
                    LoggingService.Instance?.LogOperation("SetDhcp", _selectedAdapter.Name, true);

                    if (chkFlushDnsAfter.Checked)
                    {
                        await _networkService.FlushDnsAsync(ct);
                        LogMessage("DNS cache flushed.");
                    }

                    await RefreshAdapters();
                    return true;
                }

                ShowError(result.Error);
                return false;
            });
        }

        private async Task SetDhcpFromTray()
        {
            if (_selectedAdapter == null)
            {
                var activeResult = await _adapterService.GetActiveAdapterAsync();
                if (!activeResult.IsSuccess)
                {
                    notifyIcon.ShowBalloonTip(2000, "Error", "No active adapter found", ToolTipIcon.Error);
                    return;
                }
                _selectedAdapter = activeResult.Value;
            }

            var result = await _networkService.SetDhcpAsync(_selectedAdapter.Name);
            if (result.IsSuccess)
            {
                notifyIcon.ShowBalloonTip(2000, "Success", $"DHCP enabled on {_selectedAdapter.Name}", ToolTipIcon.Info);
            }
            else
            {
                notifyIcon.ShowBalloonTip(2000, "Error", result.Error, ToolTipIcon.Error);
            }
        }

        private async Task ReleaseRenew()
        {
            if (_selectedAdapter == null)
            {
                ShowError("Please select a network adapter.");
                return;
            }

            await ExecuteNetworkOperation("Releasing and renewing DHCP lease...", async ct =>
            {
                var result = await _networkService.ReleaseRenewAsync(_selectedAdapter.Name, ct);

                if (result.IsSuccess)
                {
                    LogMessage("DHCP lease released and renewed.");
                    await RefreshAdapters();
                    return true;
                }

                ShowError(result.Error);
                return false;
            });
        }

        private async Task FlushDns()
        {
            await ExecuteNetworkOperation("Flushing DNS cache...", async ct =>
            {
                var result = await _networkService.FlushDnsAsync(ct);

                if (result.IsSuccess)
                {
                    LogMessage("DNS cache flushed successfully.");
                    return true;
                }

                ShowError(result.Error);
                return false;
            });
        }

        private async Task TestConnectivity()
        {
            await ExecuteNetworkOperation("Testing connectivity...", async ct =>
            {
                return await TestConnectivityInternal(ct);
            });
        }

        private async Task<bool> TestConnectivityInternal(CancellationToken ct)
        {
            var gateway = _selectedAdapter?.CurrentConfiguration.Gateway ?? string.Empty;
            var dns = _selectedAdapter?.CurrentConfiguration.Dns1 ?? "8.8.8.8";

            var result = await _networkService.TestConnectivityAsync(gateway, dns, ct);

            if (result.IsSuccess)
            {
                var test = result.Value;
                LogMessage("Connectivity Test Results:");
                LogMessage($"  Gateway: {(test.GatewayReachable ? "OK" : "FAILED")} {(test.GatewayReachable ? $"({test.GatewayLatencyMs}ms)" : "")}");
                LogMessage($"  DNS:     {(test.DnsReachable ? "OK" : "FAILED")} {(test.DnsReachable ? $"({test.DnsLatencyMs}ms)" : "")}");
                LogMessage($"  Internet: {(test.InternetReachable ? "OK" : "FAILED")} {(test.InternetReachable ? $"({test.InternetLatencyMs}ms)" : "")}");
                return true;
            }

            ShowError(result.Error);
            return false;
        }

        private async Task Undo()
        {
            if (_previousConfig == null || _selectedAdapter == null)
            {
                ShowError("No previous configuration to restore.");
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Restore previous configuration?\n\n{_previousConfig.ToDisplayString()}",
                "Confirm Undo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes) return;

            await ExecuteNetworkOperation("Restoring previous configuration...", async ct =>
            {
                Result result;
                if (_previousConfig.IsDhcp)
                {
                    result = await _networkService.SetDhcpAsync(_selectedAdapter.Name, ct);
                }
                else
                {
                    result = await _networkService.ApplyStaticConfigurationAsync(_selectedAdapter.Name, _previousConfig, ct);
                }

                if (result.IsSuccess)
                {
                    LogMessage("Previous configuration restored.");
                    btnUndo.Enabled = false;
                    _previousConfig = null;
                    await RefreshAdapters();
                    return true;
                }

                ShowError(result.Error);
                return false;
            });
        }

        private async Task SavePreset()
        {
            if (_selectedAdapter == null)
            {
                ShowError("Please select a network adapter.");
                return;
            }

            using (var dialog = new InputDialog("Save Preset", "Enter preset name:"))
            {
                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    var config = NetworkConfiguration.Static(
                        txtNewIp.Text, txtNewSubnet.Text, txtNewGateway.Text, txtNewDns1.Text, txtNewDns2.Text);

                    var result = await _presetService.CreateFromCurrentAsync(
                        dialog.InputText, _selectedAdapter.Name, config, false);

                    if (result.IsSuccess)
                    {
                        LogMessage($"Preset '{dialog.InputText}' saved.");
                    }
                    else
                    {
                        ShowError(result.Error);
                    }
                }
            }
        }

        private async Task LoadPreset()
        {
            var presetsResult = await _presetService.GetAllPresetsAsync();
            if (!presetsResult.IsSuccess || presetsResult.Value.Count == 0)
            {
                ShowError("No presets found.");
                return;
            }

            using (var dialog = new PresetSelectionDialog(presetsResult.Value))
            {
                if (dialog.ShowDialog() == DialogResult.OK && dialog.SelectedPreset != null)
                {
                    var preset = dialog.SelectedPreset;
                    txtNewIp.Text = preset.Configuration.IpAddress;
                    txtNewSubnet.Text = preset.Configuration.SubnetMask;
                    txtNewGateway.Text = preset.Configuration.Gateway;
                    txtNewDns1.Text = preset.Configuration.Dns1;
                    txtNewDns2.Text = preset.Configuration.Dns2;

                    LogMessage($"Loaded preset '{preset.Name}'.");
                }
            }
        }

        private async Task CreateBackup()
        {
            if (_selectedAdapter == null)
            {
                ShowError("Please select a network adapter.");
                return;
            }

            var result = await _backupService.CreateBackupAsync(
                _selectedAdapter.Name,
                _selectedAdapter.CurrentConfiguration,
                "Manual backup");

            if (result.IsSuccess)
            {
                LogMessage($"Backup created: {result.Value.GetDisplayName()}");
            }
            else
            {
                ShowError(result.Error);
            }
        }

        private async Task RestoreBackup()
        {
            var backupsResult = await _backupService.GetAllBackupsAsync();
            if (!backupsResult.IsSuccess || backupsResult.Value.Count == 0)
            {
                ShowError("No backups found.");
                return;
            }

            using (var dialog = new BackupSelectionDialog(backupsResult.Value))
            {
                if (dialog.ShowDialog() == DialogResult.OK && dialog.SelectedBackup != null)
                {
                    var backup = dialog.SelectedBackup;
                    var restoreResult = await _backupService.RestoreFromBackupAsync(backup.Id);

                    if (restoreResult.IsSuccess)
                    {
                        var config = restoreResult.Value;
                        txtNewIp.Text = config.IpAddress;
                        txtNewSubnet.Text = config.SubnetMask;
                        txtNewGateway.Text = config.Gateway;
                        txtNewDns1.Text = config.Dns1;
                        txtNewDns2.Text = config.Dns2;

                        LogMessage($"Restored configuration from backup: {backup.GetDisplayName()}");
                    }
                    else
                    {
                        ShowError(restoreResult.Error);
                    }
                }
            }
        }

        private void ImportPresets()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.Title = "Import Presets";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Task.Run(async () =>
                    {
                        var result = await _presetService.ImportPresetsAsync(dialog.FileName);
                        Invoke(new Action(() =>
                        {
                            if (result.IsSuccess)
                            {
                                LogMessage($"Imported {result.Value.Count} preset(s).");
                            }
                            else
                            {
                                ShowError(result.Error);
                            }
                        }));
                    });
                }
            }
        }

        private void ExportPresets()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON files (*.json)|*.json";
                dialog.Title = "Export Presets";
                dialog.FileName = "network_presets.json";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Task.Run(async () =>
                    {
                        var result = await _presetService.ExportPresetsAsync(dialog.FileName);
                        Invoke(new Action(() =>
                        {
                            if (result.IsSuccess)
                            {
                                LogMessage($"Presets exported to {dialog.FileName}");
                            }
                            else
                            {
                                ShowError(result.Error);
                            }
                        }));
                    });
                }
            }
        }

        private void ExportLog()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Title = "Export Log";
                dialog.FileName = $"network_log_{DateTime.Now:yyyyMMdd}.txt";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllText(dialog.FileName, txtLog.Text);
                        LogMessage($"Log exported to {dialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to export log: {ex.Message}");
                    }
                }
            }
        }

        private void ManagePresets()
        {
            using (var form = new PresetManagerForm(_presetService))
            {
                form.ShowDialog();
            }
        }

        private void ShowDiagnostics()
        {
            using (var form = new DiagnosticsForm(_networkService, _selectedAdapter))
            {
                form.ShowDialog();
            }
        }

        private void ShowMacChanger()
        {
            using (var form = new MacSpoofForm(_macService, _selectedAdapter))
            {
                form.ShowDialog();
                // Refresh in case MAC was changed
                _ = RefreshAdapters();
            }
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _settings = SettingsService.Instance.GetSettings();
                    ApplyTheme();
                }
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Network Configuration App v1.0\n\n" +
                "A portable Windows utility for managing network adapter settings.\n\n" +
                "Features:\n" +
                "- Static IP / DHCP configuration\n" +
                "- Presets and backups\n" +
                "- Network diagnostics\n" +
                "- MAC address spoofing",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowHelp()
        {
            using (var form = new HelpForm())
            {
                form.ShowDialog(this);
            }
        }

        private async Task ExecuteNetworkOperation(string status, Func<CancellationToken, Task<bool>> operation)
        {
            SetLoading(true, status);
            _operationCts = new CancellationTokenSource();

            try
            {
                var success = await operation(_operationCts.Token);
                SetStatus(success ? "Operation completed." : "Operation failed.");
            }
            catch (OperationCanceledException)
            {
                LogMessage("Operation cancelled.");
                SetStatus("Operation cancelled.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
                SetStatus("Error occurred.");
            }
            finally
            {
                SetLoading(false);
                _operationCts = null;
            }
        }

        private void SetLoading(bool isLoading, string status = null)
        {
            _isLoading = isLoading;

            Invoke(new Action(() =>
            {
                progressBar.Visible = isLoading;
                btnApplyStatic.Enabled = !isLoading;
                btnSetDhcp.Enabled = !isLoading;
                btnReleaseRenew.Enabled = !isLoading;
                btnFlushDns.Enabled = !isLoading;
                btnTestConnectivity.Enabled = !isLoading;
                cmbAdapters.Enabled = !isLoading;

                if (status != null)
                {
                    SetStatus(status);
                }
            }));
        }

        private void SetStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStatus(status)));
                return;
            }

            lblStatus.Text = status;
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => LogMessage(message)));
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();

            if (chkLogOperations.Checked)
            {
                LoggingService.Instance?.Info(message);
            }
        }

        private void ShowError(string message)
        {
            LogMessage($"ERROR: {message}");
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
