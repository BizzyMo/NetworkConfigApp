using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Dialog for selecting a preset from the list.
    /// </summary>
    public class PresetSelectionDialog : Form
    {
        private ListBox lstPresets;
        private TextBox txtDetails;
        private Button btnOk;
        private Button btnCancel;
        private readonly IReadOnlyList<Preset> _presets;

        public Preset SelectedPreset => lstPresets.SelectedItem as Preset;

        public PresetSelectionDialog(IReadOnlyList<Preset> presets)
        {
            _presets = presets;

            Text = "Select Preset";
            Size = new Size(500, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblPresets = new Label
            {
                Text = "Available Presets:",
                Location = new Point(10, 10),
                AutoSize = true
            };

            lstPresets = new ListBox
            {
                Location = new Point(10, 30),
                Size = new Size(200, 280)
            };
            lstPresets.SelectedIndexChanged += LstPresets_SelectedIndexChanged;

            var lblDetails = new Label
            {
                Text = "Details:",
                Location = new Point(220, 10),
                AutoSize = true
            };

            txtDetails = new TextBox
            {
                Location = new Point(220, 30),
                Size = new Size(255, 280),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            btnOk = new Button
            {
                Text = "Load",
                DialogResult = DialogResult.OK,
                Location = new Point(310, 320),
                Size = new Size(80, 28),
                Enabled = false
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(395, 320),
                Size = new Size(80, 28)
            };

            Controls.AddRange(new Control[] { lblPresets, lstPresets, lblDetails, txtDetails, btnOk, btnCancel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Load presets
            foreach (var preset in _presets)
            {
                lstPresets.Items.Add(preset);
            }

            if (lstPresets.Items.Count > 0)
            {
                lstPresets.SelectedIndex = 0;
            }
        }

        private void LstPresets_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            var preset = lstPresets.SelectedItem as Preset;
            btnOk.Enabled = preset != null;

            if (preset != null)
            {
                var config = preset.Configuration;
                txtDetails.Text =
                    $"Name: {preset.Name}\r\n" +
                    $"Adapter: {preset.AdapterName}\r\n" +
                    $"Created: {preset.CreatedAt:g}\r\n" +
                    $"Last Used: {preset.LastAppliedAt?.ToString("g") ?? "Never"}\r\n\r\n" +
                    $"Configuration:\r\n" +
                    (config.IsDhcp
                        ? "  DHCP (Automatic)"
                        : $"  IP: {config.IpAddress}\r\n" +
                          $"  Subnet: {config.SubnetMask}\r\n" +
                          $"  Gateway: {config.Gateway}\r\n" +
                          $"  DNS 1: {config.Dns1}\r\n" +
                          $"  DNS 2: {config.Dns2}") +
                    (string.IsNullOrEmpty(preset.Description) ? "" : $"\r\n\r\nDescription:\r\n{preset.Description}");
            }
        }
    }
}
