using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;
using NetworkConfigApp.Core.Utilities;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// MAC address spoofing form with manufacturer selection.
    /// </summary>
    public class MacSpoofForm : Form
    {
        private readonly IMacService _macService;
        private readonly NetworkAdapter _adapter;

        private Label lblCurrentMac;
        private Label lblManufacturer;
        private TextBox txtNewMac;
        private ComboBox cmbManufacturer;
        private Button btnGenerate;
        private Button btnRandomize;
        private Button btnApply;
        private Button btnRestore;
        private Button btnClose;
        private Label lblWarning;
        private ProgressBar progressBar;

        public MacSpoofForm(IMacService macService, NetworkAdapter adapter)
        {
            _macService = macService;
            _adapter = adapter;

            InitializeComponent();
            LoadManufacturers();
        }

        private void InitializeComponent()
        {
            Text = "MAC Address Changer";
            Size = new Size(450, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            // Current MAC
            var grpCurrent = new GroupBox
            {
                Text = "Current MAC Address",
                Location = new Point(10, 10),
                Size = new Size(420, 80)
            };

            lblCurrentMac = new Label
            {
                Text = _adapter?.MacAddress ?? "N/A",
                Location = new Point(10, 25),
                Size = new Size(200, 20),
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };

            lblManufacturer = new Label
            {
                Text = $"Manufacturer: {_macService.GetManufacturer(_adapter?.MacAddress ?? "")}",
                Location = new Point(10, 50),
                Size = new Size(400, 20),
                ForeColor = Color.DarkGray
            };

            grpCurrent.Controls.AddRange(new Control[] { lblCurrentMac, lblManufacturer });

            // New MAC
            var grpNew = new GroupBox
            {
                Text = "New MAC Address",
                Location = new Point(10, 100),
                Size = new Size(420, 120)
            };

            var lblNew = new Label
            {
                Text = "MAC Address:",
                Location = new Point(10, 25),
                AutoSize = true
            };

            txtNewMac = new TextBox
            {
                Location = new Point(100, 22),
                Size = new Size(180, 23)
            };
            txtNewMac.TextChanged += TxtNewMac_TextChanged;

            btnGenerate = new Button
            {
                Text = "Generate",
                Location = new Point(290, 20),
                Size = new Size(80, 28)
            };
            btnGenerate.Click += BtnGenerate_Click;

            var lblManu = new Label
            {
                Text = "Manufacturer:",
                Location = new Point(10, 60),
                AutoSize = true
            };

            cmbManufacturer = new ComboBox
            {
                Location = new Point(100, 57),
                Size = new Size(180, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            btnRandomize = new Button
            {
                Text = "Random",
                Location = new Point(290, 55),
                Size = new Size(80, 28)
            };
            btnRandomize.Click += BtnRandomize_Click;

            var lblNewManu = new Label
            {
                Location = new Point(10, 90),
                Size = new Size(400, 20),
                ForeColor = Color.DarkGray
            };
            txtNewMac.Tag = lblNewManu; // Store reference for updating

            grpNew.Controls.AddRange(new Control[]
            {
                lblNew, txtNewMac, btnGenerate,
                lblManu, cmbManufacturer, btnRandomize, lblNewManu
            });

            // Warning
            lblWarning = new Label
            {
                Text = "Warning: Changing MAC address requires administrator privileges and will temporarily\n" +
                       "disconnect the network adapter. Some networks may detect MAC changes.",
                Location = new Point(10, 230),
                Size = new Size(420, 40),
                ForeColor = Color.OrangeRed
            };

            // Progress
            progressBar = new ProgressBar
            {
                Location = new Point(10, 275),
                Size = new Size(420, 20),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false
            };

            // Buttons
            btnApply = new Button
            {
                Text = "Apply",
                Location = new Point(170, 280),
                Size = new Size(80, 28)
            };
            btnApply.Click += async (s, e) => await ApplyMac();

            btnRestore = new Button
            {
                Text = "Restore Original",
                Location = new Point(255, 280),
                Size = new Size(100, 28)
            };
            btnRestore.Click += async (s, e) => await RestoreMac();

            btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                Location = new Point(360, 280),
                Size = new Size(70, 28)
            };

            Controls.AddRange(new Control[]
            {
                grpCurrent, grpNew, lblWarning, progressBar,
                btnApply, btnRestore, btnClose
            });

            CancelButton = btnClose;
        }

        private void LoadManufacturers()
        {
            cmbManufacturer.Items.Clear();
            cmbManufacturer.Items.Add("(Any)");

            foreach (var entry in _macService.GetCommonManufacturers())
            {
                cmbManufacturer.Items.Add(entry);
            }

            cmbManufacturer.SelectedIndex = 0;
        }

        private void TxtNewMac_TextChanged(object sender, EventArgs e)
        {
            var mac = txtNewMac.Text;
            var lblManu = txtNewMac.Tag as Label;

            if (_macService.IsValidMac(mac))
            {
                txtNewMac.BackColor = SystemColors.Window;
                var manufacturer = _macService.GetManufacturer(mac);
                if (lblManu != null)
                {
                    lblManu.Text = $"New manufacturer: {manufacturer}";
                }
            }
            else
            {
                txtNewMac.BackColor = Color.MistyRose;
                if (lblManu != null)
                {
                    lblManu.Text = "Invalid MAC address format";
                }
            }
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            string manufacturer = null;
            if (cmbManufacturer.SelectedIndex > 0)
            {
                var entry = cmbManufacturer.SelectedItem as ManufacturerEntry;
                manufacturer = entry?.Manufacturer;
            }

            var newMac = _macService.GenerateRandomMac(manufacturer);
            txtNewMac.Text = newMac;
        }

        private void BtnRandomize_Click(object sender, EventArgs e)
        {
            // Generate completely random locally-administered MAC
            var random = new Random();
            var bytes = new byte[6];
            random.NextBytes(bytes);
            bytes[0] = (byte)((bytes[0] & 0xFE) | 0x02); // Set locally administered bit

            txtNewMac.Text = string.Join(":", Array.ConvertAll(bytes, b => b.ToString("X2")));
        }

        private async Task ApplyMac()
        {
            if (_adapter == null)
            {
                MessageBox.Show("No adapter selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var newMac = txtNewMac.Text.Trim();
            if (!_macService.IsValidMac(newMac))
            {
                MessageBox.Show("Invalid MAC address format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"Change MAC address of {_adapter.Name} to {newMac}?\n\n" +
                "This will temporarily disconnect the network adapter.",
                "Confirm MAC Change",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetLoading(true);

            try
            {
                var result = await _macService.ChangeMacAsync(_adapter.Name, newMac);

                if (result.IsSuccess)
                {
                    var actualMac = await _macService.GetCurrentMacAsync(_adapter.Name);
                    lblCurrentMac.Text = actualMac.Value ?? newMac;
                    lblManufacturer.Text = $"Manufacturer: {_macService.GetManufacturer(actualMac.Value ?? newMac)}";

                    MessageBox.Show(
                        "MAC address changed successfully.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to change MAC address:\n\n{result.Error}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task RestoreMac()
        {
            if (_adapter == null)
            {
                MessageBox.Show("No adapter selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"Restore original MAC address of {_adapter.Name}?\n\n" +
                "This will temporarily disconnect the network adapter.",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetLoading(true);

            try
            {
                var result = await _macService.RestoreOriginalMacAsync(_adapter.Name);

                if (result.IsSuccess)
                {
                    var actualMac = await _macService.GetCurrentMacAsync(_adapter.Name);
                    lblCurrentMac.Text = actualMac.Value ?? "Restored";
                    lblManufacturer.Text = $"Manufacturer: {_macService.GetManufacturer(actualMac.Value ?? "")}";

                    MessageBox.Show(
                        "Original MAC address restored.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to restore MAC address:\n\n{result.Error}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool loading)
        {
            progressBar.Visible = loading;
            btnApply.Enabled = !loading;
            btnRestore.Enabled = !loading;
            btnGenerate.Enabled = !loading;
            btnRandomize.Enabled = !loading;
        }
    }
}
