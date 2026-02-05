using System;
using System.Drawing;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Application settings form.
    /// </summary>
    public class SettingsForm : Form
    {
        private AppSettings _settings;

        private ComboBox cmbTheme;
        private ComboBox cmbLogLevel;
        private CheckBox chkAutoBackup;
        private CheckBox chkFlushDns;
        private CheckBox chkTestAfter;
        private CheckBox chkMinimizeToTray;
        private CheckBox chkStartMinimized;
        private NumericUpDown numBackupRetention;
        private NumericUpDown numMaxLogSize;
        private NumericUpDown numPingTimeout;
        private Button btnSave;
        private Button btnCancel;
        private Button btnReset;

        public SettingsForm()
        {
            _settings = SettingsService.Instance.GetSettings();
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "Settings";
            Size = new Size(450, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            // Appearance group
            var grpAppearance = new GroupBox
            {
                Text = "Appearance",
                Location = new Point(10, 10),
                Size = new Size(420, 60)
            };

            var lblTheme = new Label
            {
                Text = "Theme:",
                Location = new Point(10, 25),
                AutoSize = true
            };

            cmbTheme = new ComboBox
            {
                Location = new Point(70, 22),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTheme.Items.AddRange(new object[] { "Light", "Dark" });

            grpAppearance.Controls.AddRange(new Control[] { lblTheme, cmbTheme });

            // Behavior group
            var grpBehavior = new GroupBox
            {
                Text = "Behavior",
                Location = new Point(10, 80),
                Size = new Size(420, 130)
            };

            chkAutoBackup = new CheckBox
            {
                Text = "Automatically backup before changes",
                Location = new Point(10, 20),
                AutoSize = true
            };

            chkFlushDns = new CheckBox
            {
                Text = "Flush DNS after applying changes",
                Location = new Point(10, 45),
                AutoSize = true
            };

            chkTestAfter = new CheckBox
            {
                Text = "Test connectivity after applying changes",
                Location = new Point(10, 70),
                AutoSize = true
            };

            chkMinimizeToTray = new CheckBox
            {
                Text = "Minimize to system tray",
                Location = new Point(10, 95),
                AutoSize = true
            };

            chkStartMinimized = new CheckBox
            {
                Text = "Start minimized",
                Location = new Point(220, 95),
                AutoSize = true
            };

            grpBehavior.Controls.AddRange(new Control[]
            {
                chkAutoBackup, chkFlushDns, chkTestAfter, chkMinimizeToTray, chkStartMinimized
            });

            // Logging group
            var grpLogging = new GroupBox
            {
                Text = "Logging",
                Location = new Point(10, 220),
                Size = new Size(420, 70)
            };

            var lblLogLevel = new Label
            {
                Text = "Log Level:",
                Location = new Point(10, 25),
                AutoSize = true
            };

            cmbLogLevel = new ComboBox
            {
                Location = new Point(80, 22),
                Size = new Size(100, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbLogLevel.Items.AddRange(new object[] { "Minimal", "Normal", "Verbose" });

            var lblMaxLog = new Label
            {
                Text = "Max log size (MB):",
                Location = new Point(200, 25),
                AutoSize = true
            };

            numMaxLogSize = new NumericUpDown
            {
                Location = new Point(320, 22),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 100,
                Value = 10
            };

            grpLogging.Controls.AddRange(new Control[]
            {
                lblLogLevel, cmbLogLevel, lblMaxLog, numMaxLogSize
            });

            // Data group
            var grpData = new GroupBox
            {
                Text = "Data",
                Location = new Point(10, 300),
                Size = new Size(420, 40)
            };

            var lblRetention = new Label
            {
                Text = "Backup retention:",
                Location = new Point(10, 15),
                AutoSize = true
            };

            numBackupRetention = new NumericUpDown
            {
                Location = new Point(110, 12),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 100,
                Value = 10
            };

            grpData.Controls.AddRange(new Control[] { lblRetention, numBackupRetention });

            // Buttons
            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(190, 350),
                Size = new Size(80, 28)
            };
            btnSave.Click += BtnSave_Click;

            btnReset = new Button
            {
                Text = "Reset",
                Location = new Point(275, 350),
                Size = new Size(80, 28)
            };
            btnReset.Click += BtnReset_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(360, 350),
                Size = new Size(80, 28)
            };

            Controls.AddRange(new Control[]
            {
                grpAppearance, grpBehavior, grpLogging, grpData,
                btnSave, btnReset, btnCancel
            });

            CancelButton = btnCancel;
        }

        private void LoadSettings()
        {
            cmbTheme.SelectedIndex = _settings.Theme == AppTheme.Dark ? 1 : 0;
            cmbLogLevel.SelectedIndex = (int)_settings.LogLevel;
            chkAutoBackup.Checked = _settings.AutoBackup;
            chkFlushDns.Checked = _settings.FlushDnsAfterChanges;
            chkTestAfter.Checked = _settings.TestAfterApply;
            chkMinimizeToTray.Checked = _settings.MinimizeToTray;
            chkStartMinimized.Checked = _settings.StartMinimized;
            numBackupRetention.Value = _settings.BackupRetentionCount;
            numMaxLogSize.Value = _settings.MaxLogSizeMb;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var newSettings = AppSettings.Default()
                .WithTheme(cmbTheme.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light)
                .WithLogLevel((LogLevel)cmbLogLevel.SelectedIndex)
                .WithAutoBackup(chkAutoBackup.Checked)
                .WithFlushDnsAfterChanges(chkFlushDns.Checked)
                .WithTestAfterApply(chkTestAfter.Checked)
                .WithMinimizeToTray(chkMinimizeToTray.Checked);

            SettingsService.Instance.UpdateSettings(newSettings);
            SettingsService.Instance.SaveNow();

            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset all settings to defaults?",
                "Confirm Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                _settings = AppSettings.Default();
                LoadSettings();
            }
        }
    }
}
