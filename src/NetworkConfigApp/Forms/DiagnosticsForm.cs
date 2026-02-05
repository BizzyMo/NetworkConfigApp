using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Network diagnostics window with ping, traceroute, and DNS testing.
    /// </summary>
    public class DiagnosticsForm : Form
    {
        private readonly INetworkService _networkService;
        private readonly NetworkAdapter _adapter;
        private CancellationTokenSource _cts;

        private TextBox txtHost;
        private Button btnPing;
        private Button btnTrace;
        private Button btnDns;
        private Button btnFullTest;
        private Button btnStop;
        private TextBox txtResults;
        private ProgressBar progressBar;

        public DiagnosticsForm(INetworkService networkService, NetworkAdapter adapter)
        {
            _networkService = networkService;
            _adapter = adapter;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Network Diagnostics";
            Size = new Size(600, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblHost = new Label
            {
                Text = "Host/IP:",
                Location = new Point(10, 15),
                AutoSize = true
            };

            txtHost = new TextBox
            {
                Location = new Point(70, 12),
                Size = new Size(200, 23),
                Text = "8.8.8.8"
            };

            btnPing = new Button
            {
                Text = "Ping",
                Location = new Point(280, 10),
                Size = new Size(70, 28)
            };
            btnPing.Click += async (s, e) => await RunPing();

            btnTrace = new Button
            {
                Text = "Trace",
                Location = new Point(355, 10),
                Size = new Size(70, 28)
            };
            btnTrace.Click += async (s, e) => await RunTrace();

            btnDns = new Button
            {
                Text = "DNS",
                Location = new Point(430, 10),
                Size = new Size(70, 28)
            };
            btnDns.Click += async (s, e) => await RunDns();

            btnFullTest = new Button
            {
                Text = "Full Test",
                Location = new Point(505, 10),
                Size = new Size(75, 28)
            };
            btnFullTest.Click += async (s, e) => await RunFullTest();

            btnStop = new Button
            {
                Text = "Stop",
                Location = new Point(505, 45),
                Size = new Size(75, 28),
                Enabled = false
            };
            btnStop.Click += (s, e) => _cts?.Cancel();

            progressBar = new ProgressBar
            {
                Location = new Point(10, 45),
                Size = new Size(490, 20),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false
            };

            txtResults = new TextBox
            {
                Location = new Point(10, 75),
                Size = new Size(565, 375),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                WordWrap = false
            };

            Controls.AddRange(new Control[]
            {
                lblHost, txtHost, btnPing, btnTrace, btnDns, btnFullTest, btnStop,
                progressBar, txtResults
            });

            // Pre-fill with adapter info
            if (_adapter != null)
            {
                AppendResult($"Adapter: {_adapter.Name}");
                AppendResult($"IP: {_adapter.CurrentConfiguration.IpAddress}");
                AppendResult($"Gateway: {_adapter.CurrentConfiguration.Gateway}");
                AppendResult($"DNS: {_adapter.CurrentConfiguration.Dns1}");
                AppendResult(new string('-', 50));
            }
        }

        private async Task RunPing()
        {
            var host = txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a host or IP address.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetRunning(true);
            _cts = new CancellationTokenSource();

            try
            {
                AppendResult($"\nPinging {host}...\n");

                for (int i = 0; i < 4 && !_cts.Token.IsCancellationRequested; i++)
                {
                    var result = await _networkService.PingAsync(host, 3000, _cts.Token);
                    AppendResult(result.Message);
                    await Task.Delay(1000, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                AppendResult("Ping cancelled.");
            }
            finally
            {
                SetRunning(false);
            }
        }

        private async Task RunTrace()
        {
            var host = txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a host or IP address.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetRunning(true);
            _cts = new CancellationTokenSource();

            try
            {
                AppendResult($"\nTracing route to {host}...\n");

                var result = await _networkService.TraceRouteAsync(host, 30, 3000, _cts.Token);

                foreach (var hop in result.Hops)
                {
                    AppendResult(hop.ToString());
                }

                AppendResult($"\n{result.Message}");
            }
            catch (OperationCanceledException)
            {
                AppendResult("Trace cancelled.");
            }
            finally
            {
                SetRunning(false);
            }
        }

        private async Task RunDns()
        {
            var host = txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a hostname.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetRunning(true);
            _cts = new CancellationTokenSource();

            try
            {
                AppendResult($"\nResolving {host}...\n");

                var result = await _networkService.TestDnsAsync(host, _cts.Token);
                AppendResult(result.Message);
                AppendResult($"Time: {result.RoundTripTimeMs}ms");
            }
            catch (OperationCanceledException)
            {
                AppendResult("DNS lookup cancelled.");
            }
            finally
            {
                SetRunning(false);
            }
        }

        private async Task RunFullTest()
        {
            SetRunning(true);
            _cts = new CancellationTokenSource();

            try
            {
                AppendResult("\n=== Full Connectivity Test ===\n");

                var gateway = _adapter?.CurrentConfiguration.Gateway ?? string.Empty;
                var dns = _adapter?.CurrentConfiguration.Dns1 ?? "8.8.8.8";

                // Test gateway
                if (!string.IsNullOrEmpty(gateway))
                {
                    AppendResult($"Testing gateway ({gateway})...");
                    var gwResult = await _networkService.PingAsync(gateway, 3000, _cts.Token);
                    AppendResult($"  {(gwResult.IsSuccess ? "OK" : "FAILED")} - {gwResult.Message}");
                }
                else
                {
                    AppendResult("No gateway configured.");
                }

                // Test DNS server
                AppendResult($"\nTesting DNS server ({dns})...");
                var dnsResult = await _networkService.PingAsync(dns, 3000, _cts.Token);
                AppendResult($"  {(dnsResult.IsSuccess ? "OK" : "FAILED")} - {dnsResult.Message}");

                // Test DNS resolution
                AppendResult("\nTesting DNS resolution (google.com)...");
                var resolveResult = await _networkService.TestDnsAsync("google.com", _cts.Token);
                AppendResult($"  {(resolveResult.IsSuccess ? "OK" : "FAILED")} - {resolveResult.Message}");

                // Test internet connectivity
                AppendResult("\nTesting internet (8.8.8.8)...");
                var inetResult = await _networkService.PingAsync("8.8.8.8", 3000, _cts.Token);
                AppendResult($"  {(inetResult.IsSuccess ? "OK" : "FAILED")} - {inetResult.Message}");

                AppendResult("\n=== Test Complete ===");
            }
            catch (OperationCanceledException)
            {
                AppendResult("Test cancelled.");
            }
            finally
            {
                SetRunning(false);
            }
        }

        private void SetRunning(bool running)
        {
            progressBar.Visible = running;
            btnPing.Enabled = !running;
            btnTrace.Enabled = !running;
            btnDns.Enabled = !running;
            btnFullTest.Enabled = !running;
            btnStop.Enabled = running;
        }

        private void AppendResult(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendResult(text)));
                return;
            }

            txtResults.AppendText(text + Environment.NewLine);
            txtResults.SelectionStart = txtResults.TextLength;
            txtResults.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}
