using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NetworkConfigApp.Core.Services;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Dialog for selecting a backup to restore.
    /// </summary>
    public class BackupSelectionDialog : Form
    {
        private ListBox lstBackups;
        private TextBox txtDetails;
        private Button btnRestore;
        private Button btnCancel;
        private Button btnDelete;
        private readonly IReadOnlyList<BackupInfo> _backups;

        public BackupInfo SelectedBackup => lstBackups.SelectedItem as BackupInfo;

        public BackupSelectionDialog(IReadOnlyList<BackupInfo> backups)
        {
            _backups = backups;

            Text = "Restore Backup";
            Size = new Size(550, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblBackups = new Label
            {
                Text = "Available Backups (newest first):",
                Location = new Point(10, 10),
                AutoSize = true
            };

            lstBackups = new ListBox
            {
                Location = new Point(10, 30),
                Size = new Size(250, 280)
            };
            lstBackups.SelectedIndexChanged += LstBackups_SelectedIndexChanged;

            var lblDetails = new Label
            {
                Text = "Configuration:",
                Location = new Point(270, 10),
                AutoSize = true
            };

            txtDetails = new TextBox
            {
                Location = new Point(270, 30),
                Size = new Size(255, 280),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            btnRestore = new Button
            {
                Text = "Restore",
                DialogResult = DialogResult.OK,
                Location = new Point(280, 320),
                Size = new Size(80, 28),
                Enabled = false
            };

            btnDelete = new Button
            {
                Text = "Delete",
                Location = new Point(365, 320),
                Size = new Size(80, 28),
                Enabled = false
            };
            btnDelete.Click += BtnDelete_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(450, 320),
                Size = new Size(80, 28)
            };

            Controls.AddRange(new Control[] { lblBackups, lstBackups, lblDetails, txtDetails, btnRestore, btnDelete, btnCancel });

            AcceptButton = btnRestore;
            CancelButton = btnCancel;

            // Load backups
            foreach (var backup in _backups)
            {
                lstBackups.Items.Add(backup);
            }

            if (lstBackups.Items.Count > 0)
            {
                lstBackups.SelectedIndex = 0;
            }
        }

        private void LstBackups_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            var backup = lstBackups.SelectedItem as BackupInfo;
            btnRestore.Enabled = backup != null;
            btnDelete.Enabled = backup != null;

            if (backup != null)
            {
                var config = backup.Configuration;
                txtDetails.Text =
                    $"Adapter: {backup.AdapterName}\r\n" +
                    $"Created: {backup.CreatedAt:g}\r\n\r\n" +
                    (config.IsDhcp
                        ? "DHCP (Automatic)"
                        : $"IP: {config.IpAddress}\r\n" +
                          $"Subnet: {config.SubnetMask}\r\n" +
                          $"Gateway: {config.Gateway}\r\n" +
                          $"DNS 1: {config.Dns1}\r\n" +
                          $"DNS 2: {config.Dns2}") +
                    (string.IsNullOrEmpty(backup.Description) ? "" : $"\r\n\r\n{backup.Description}");
            }
        }

        private void BtnDelete_Click(object sender, System.EventArgs e)
        {
            var backup = lstBackups.SelectedItem as BackupInfo;
            if (backup == null) return;

            var result = MessageBox.Show(
                $"Delete backup from {backup.CreatedAt:g}?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                lstBackups.Items.Remove(backup);
                txtDetails.Clear();
                // Note: Actual deletion happens when dialog closes or via async call
            }
        }
    }
}
