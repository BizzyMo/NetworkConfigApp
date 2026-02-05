using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Form for managing saved presets (view, edit, delete).
    /// </summary>
    public class PresetManagerForm : Form
    {
        private readonly IPresetService _presetService;

        private ListBox lstPresets;
        private TextBox txtDetails;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnImport;
        private Button btnExport;
        private Button btnClose;

        public PresetManagerForm(IPresetService presetService)
        {
            _presetService = presetService;
            InitializeComponent();
            LoadPresets();
        }

        private void InitializeComponent()
        {
            Text = "Preset Manager";
            Size = new Size(600, 450);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblPresets = new Label
            {
                Text = "Saved Presets:",
                Location = new Point(10, 10),
                AutoSize = true
            };

            lstPresets = new ListBox
            {
                Location = new Point(10, 30),
                Size = new Size(220, 330)
            };
            lstPresets.SelectedIndexChanged += LstPresets_SelectedIndexChanged;

            var lblDetails = new Label
            {
                Text = "Details:",
                Location = new Point(240, 10),
                AutoSize = true
            };

            txtDetails = new TextBox
            {
                Location = new Point(240, 30),
                Size = new Size(335, 330),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            btnEdit = new Button
            {
                Text = "Rename",
                Location = new Point(10, 370),
                Size = new Size(80, 28),
                Enabled = false
            };
            btnEdit.Click += BtnEdit_Click;

            btnDelete = new Button
            {
                Text = "Delete",
                Location = new Point(95, 370),
                Size = new Size(80, 28),
                Enabled = false
            };
            btnDelete.Click += async (s, e) => await DeletePreset();

            btnImport = new Button
            {
                Text = "Import...",
                Location = new Point(320, 370),
                Size = new Size(80, 28)
            };
            btnImport.Click += BtnImport_Click;

            btnExport = new Button
            {
                Text = "Export...",
                Location = new Point(405, 370),
                Size = new Size(80, 28)
            };
            btnExport.Click += BtnExport_Click;

            btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Location = new Point(495, 370),
                Size = new Size(80, 28)
            };

            Controls.AddRange(new Control[]
            {
                lblPresets, lstPresets, lblDetails, txtDetails,
                btnEdit, btnDelete, btnImport, btnExport, btnClose
            });

            AcceptButton = btnClose;
        }

        private async void LoadPresets()
        {
            lstPresets.Items.Clear();

            var result = await _presetService.GetAllPresetsAsync();
            if (result.IsSuccess)
            {
                foreach (var preset in result.Value)
                {
                    lstPresets.Items.Add(preset);
                }
            }

            if (lstPresets.Items.Count > 0)
            {
                lstPresets.SelectedIndex = 0;
            }
        }

        private void LstPresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            var preset = lstPresets.SelectedItem as Preset;
            btnEdit.Enabled = preset != null;
            btnDelete.Enabled = preset != null;

            if (preset != null)
            {
                var config = preset.Configuration;
                txtDetails.Text =
                    $"Name: {preset.Name}\r\n" +
                    $"ID: {preset.Id}\r\n" +
                    $"Adapter: {preset.AdapterName}\r\n" +
                    $"Created: {preset.CreatedAt:g}\r\n" +
                    $"Modified: {preset.ModifiedAt:g}\r\n" +
                    $"Last Used: {preset.LastAppliedAt?.ToString("g") ?? "Never"}\r\n" +
                    $"Encrypted: {(preset.IsEncrypted ? "Yes" : "No")}\r\n\r\n" +
                    "Configuration:\r\n" +
                    (config.IsDhcp
                        ? "  Type: DHCP (Automatic)"
                        : $"  Type: Static\r\n" +
                          $"  IP Address: {config.IpAddress}\r\n" +
                          $"  Subnet Mask: {config.SubnetMask}\r\n" +
                          $"  Gateway: {config.Gateway}\r\n" +
                          $"  DNS 1: {config.Dns1}\r\n" +
                          $"  DNS 2: {config.Dns2}") +
                    (string.IsNullOrEmpty(preset.Description) ? "" : $"\r\n\r\nDescription:\r\n{preset.Description}");
            }
            else
            {
                txtDetails.Clear();
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            var preset = lstPresets.SelectedItem as Preset;
            if (preset == null) return;

            using (var dialog = new InputDialog("Rename Preset", "Enter new name:", preset.Name))
            {
                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    var newName = dialog.InputText.Trim();
                    if (newName != preset.Name)
                    {
                        Task.Run(async () =>
                        {
                            // Delete old and create with new name
                            var newPreset = preset.WithName(newName);
                            await _presetService.DeletePresetAsync(preset.Name);
                            await _presetService.SavePresetAsync(newPreset);

                            Invoke(new Action(() => LoadPresets()));
                        });
                    }
                }
            }
        }

        private async Task DeletePreset()
        {
            var preset = lstPresets.SelectedItem as Preset;
            if (preset == null) return;

            var confirm = MessageBox.Show(
                $"Delete preset '{preset.Name}'?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            var result = await _presetService.DeletePresetAsync(preset.Name);
            if (result.IsSuccess)
            {
                LoadPresets();
            }
            else
            {
                MessageBox.Show(
                    $"Failed to delete preset:\n\n{result.Error}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
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
                                MessageBox.Show(
                                    $"Imported {result.Value.Count} preset(s).",
                                    "Import Complete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                                LoadPresets();
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to import:\n\n{result.Error}",
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }));
                    });
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
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
                                MessageBox.Show(
                                    $"Presets exported to:\n{dialog.FileName}",
                                    "Export Complete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to export:\n\n{result.Error}",
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }));
                    });
                }
            }
        }
    }
}
