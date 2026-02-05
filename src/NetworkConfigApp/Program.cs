using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetworkConfigApp.Core.Commands;
using NetworkConfigApp.Core.Models;
using NetworkConfigApp.Core.Services;
using NetworkConfigApp.Core.Utilities;
using NetworkConfigApp.Forms;

namespace NetworkConfigApp
{
    /// <summary>
    /// Application entry point with admin elevation and CLI support.
    ///
    /// Algorithm:
    /// 1. Check for command line arguments
    /// 2. If CLI mode, execute commands and exit
    /// 3. If GUI mode, check admin privileges and launch form
    ///
    /// Security: Requests admin elevation via manifest for network operations.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            // Initialize services
            InitializeServices();

            // Check for command line mode
            if (args.Length > 0)
            {
                return HandleCommandLine(args);
            }

            // GUI mode - check admin and run form
            return RunGui();
        }

        private static void InitializeServices()
        {
            try
            {
                // Initialize settings first (determines portable mode)
                var settings = SettingsService.Instance.GetSettings();

                // Initialize logging with settings
                var logDir = settings.PortableMode
                    ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")
                    : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetworkConfigApp", "logs");

                LoggingService.Initialize(logDir, settings.MaxLogSizeMb, settings.LogLevel);
                LoggingService.Instance.Info("NetworkConfigApp starting...");
            }
            catch (Exception ex)
            {
                // Log to console if logging fails
                Console.Error.WriteLine($"Failed to initialize services: {ex.Message}");
            }
        }

        private static int HandleCommandLine(string[] args)
        {
            var parser = new CommandLineParser(args);

            if (parser.IsHelpRequested)
            {
                Console.WriteLine(CommandLineParser.GetHelpText());
                return 0;
            }

            if (!parser.IsValid)
            {
                Console.Error.WriteLine("Error: " + string.Join("; ", parser.Errors));
                Console.Error.WriteLine("Use /help for usage information.");
                return 1;
            }

            // Check admin for operations that require it
            if (!AdminHelper.IsRunningAsAdmin() &&
                parser.Command != CommandLineCommand.Help &&
                parser.Command != CommandLineCommand.Diagnose)
            {
                Console.Error.WriteLine("Error: Administrator privileges required for this operation.");
                Console.Error.WriteLine("Please run from an elevated command prompt.");
                return 2;
            }

            var requestResult = parser.CreateRequest();
            if (!requestResult.IsSuccess)
            {
                Console.Error.WriteLine($"Error: {requestResult.Error}");
                return 1;
            }

            return ExecuteCommandLineRequest(requestResult.Value).Result;
        }

        private static async Task<int> ExecuteCommandLineRequest(CommandLineRequest request)
        {
            var adapterService = new AdapterService();
            var networkService = new NetworkService();
            var presetService = new PresetService();

            try
            {
                switch (request.Command)
                {
                    case CommandLineCommand.SetStatic:
                        return await ExecuteSetStatic(networkService, request);

                    case CommandLineCommand.SetDhcp:
                        return await ExecuteSetDhcp(networkService, request);

                    case CommandLineCommand.ApplyPreset:
                        return await ExecuteApplyPreset(networkService, presetService, adapterService, request);

                    case CommandLineCommand.Release:
                        return await ExecuteRelease(networkService, request);

                    case CommandLineCommand.Renew:
                        return await ExecuteRenew(networkService, request);

                    case CommandLineCommand.ReleaseRenew:
                        return await ExecuteReleaseRenew(networkService, request);

                    case CommandLineCommand.FlushDns:
                        return await ExecuteFlushDns(networkService);

                    case CommandLineCommand.Diagnose:
                        return await ExecuteDiagnose(networkService, adapterService, request);

                    default:
                        Console.Error.WriteLine("No command specified.");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                LoggingService.Instance?.Error("CLI execution failed", ex);
                return 1;
            }
        }

        private static async Task<int> ExecuteSetStatic(NetworkService networkService, CommandLineRequest request)
        {
            var config = request.GetConfigurationWithDns();
            if (config == null)
            {
                Console.Error.WriteLine("Error: Invalid static configuration");
                return 1;
            }

            Console.WriteLine($"Setting static IP {config.IpAddress}/{config.GetCidrPrefix()} on {request.AdapterName}...");
            var result = await networkService.ApplyStaticConfigurationAsync(request.AdapterName, config);

            if (result.IsSuccess)
            {
                Console.WriteLine("Configuration applied successfully.");
                LoggingService.Instance?.LogOperation("SetStatic", request.AdapterName, true);
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            LoggingService.Instance?.LogOperation("SetStatic", request.AdapterName, false, result.Error);
            return 1;
        }

        private static async Task<int> ExecuteSetDhcp(NetworkService networkService, CommandLineRequest request)
        {
            Console.WriteLine($"Setting DHCP on {request.AdapterName}...");
            var result = await networkService.SetDhcpAsync(request.AdapterName);

            if (result.IsSuccess)
            {
                Console.WriteLine("DHCP enabled successfully.");
                LoggingService.Instance?.LogOperation("SetDhcp", request.AdapterName, true);
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            LoggingService.Instance?.LogOperation("SetDhcp", request.AdapterName, false, result.Error);
            return 1;
        }

        private static async Task<int> ExecuteApplyPreset(
            NetworkService networkService,
            PresetService presetService,
            AdapterService adapterService,
            CommandLineRequest request)
        {
            Console.WriteLine($"Applying preset '{request.PresetName}'...");

            var presetResult = await presetService.GetPresetByNameAsync(request.PresetName);
            if (!presetResult.IsSuccess)
            {
                Console.Error.WriteLine($"Error: {presetResult.Error}");
                return 1;
            }

            var preset = presetResult.Value;
            var adapterName = !string.IsNullOrEmpty(request.AdapterName)
                ? request.AdapterName
                : preset.AdapterName;

            if (string.IsNullOrEmpty(adapterName))
            {
                // Try to get active adapter
                var activeResult = await adapterService.GetActiveAdapterAsync();
                if (activeResult.IsSuccess)
                {
                    adapterName = activeResult.Value.Name;
                }
                else
                {
                    Console.Error.WriteLine("Error: No adapter specified and no active adapter found");
                    return 1;
                }
            }

            Result result;
            if (preset.Configuration.IsDhcp)
            {
                result = await networkService.SetDhcpAsync(adapterName);
            }
            else
            {
                result = await networkService.ApplyStaticConfigurationAsync(adapterName, preset.Configuration);
            }

            if (result.IsSuccess)
            {
                Console.WriteLine($"Preset '{request.PresetName}' applied successfully to {adapterName}.");
                LoggingService.Instance?.LogOperation("ApplyPreset", adapterName, true, request.PresetName);
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            LoggingService.Instance?.LogOperation("ApplyPreset", adapterName, false, result.Error);
            return 1;
        }

        private static async Task<int> ExecuteRelease(NetworkService networkService, CommandLineRequest request)
        {
            Console.WriteLine($"Releasing DHCP lease on {request.AdapterName}...");
            var result = await networkService.ReleaseAsync(request.AdapterName);

            if (result.IsSuccess)
            {
                Console.WriteLine("DHCP lease released.");
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            return 1;
        }

        private static async Task<int> ExecuteRenew(NetworkService networkService, CommandLineRequest request)
        {
            Console.WriteLine($"Renewing DHCP lease on {request.AdapterName}...");
            var result = await networkService.RenewAsync(request.AdapterName);

            if (result.IsSuccess)
            {
                Console.WriteLine("DHCP lease renewed.");
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            return 1;
        }

        private static async Task<int> ExecuteReleaseRenew(NetworkService networkService, CommandLineRequest request)
        {
            Console.WriteLine($"Releasing and renewing DHCP lease on {request.AdapterName}...");
            var result = await networkService.ReleaseRenewAsync(request.AdapterName);

            if (result.IsSuccess)
            {
                Console.WriteLine("DHCP lease released and renewed.");
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            return 1;
        }

        private static async Task<int> ExecuteFlushDns(NetworkService networkService)
        {
            Console.WriteLine("Flushing DNS cache...");
            var result = await networkService.FlushDnsAsync();

            if (result.IsSuccess)
            {
                Console.WriteLine("DNS cache flushed successfully.");
                return 0;
            }

            Console.Error.WriteLine($"Error: {result.Error}");
            return 1;
        }

        private static async Task<int> ExecuteDiagnose(
            NetworkService networkService,
            AdapterService adapterService,
            CommandLineRequest request)
        {
            string adapterName = request.AdapterName;
            string gateway = string.Empty;
            string dns = string.Empty;

            if (string.IsNullOrEmpty(adapterName))
            {
                var activeResult = await adapterService.GetActiveAdapterAsync();
                if (activeResult.IsSuccess)
                {
                    adapterName = activeResult.Value.Name;
                    gateway = activeResult.Value.CurrentConfiguration.Gateway;
                    dns = activeResult.Value.CurrentConfiguration.Dns1;
                }
            }
            else
            {
                var adapterResult = await adapterService.GetAdapterByNameAsync(adapterName);
                if (adapterResult.IsSuccess)
                {
                    gateway = adapterResult.Value.CurrentConfiguration.Gateway;
                    dns = adapterResult.Value.CurrentConfiguration.Dns1;
                }
            }

            Console.WriteLine($"Running diagnostics for {adapterName}...");
            Console.WriteLine();

            var testResult = await networkService.TestConnectivityAsync(gateway, dns);
            if (testResult.IsSuccess)
            {
                var result = testResult.Value;
                Console.WriteLine($"Gateway ({gateway}): {(result.GatewayReachable ? "OK" : "FAILED")} {(result.GatewayReachable ? $"({result.GatewayLatencyMs}ms)" : "")}");
                Console.WriteLine($"DNS ({dns}): {(result.DnsReachable ? "OK" : "FAILED")} {(result.DnsReachable ? $"({result.DnsLatencyMs}ms)" : "")}");
                Console.WriteLine($"Internet (8.8.8.8): {(result.InternetReachable ? "OK" : "FAILED")} {(result.InternetReachable ? $"({result.InternetLatencyMs}ms)" : "")}");
                Console.WriteLine();
                Console.WriteLine(result.IsFullyConnected ? "All tests passed." : "Some tests failed.");
                return result.IsFullyConnected ? 0 : 1;
            }

            Console.Error.WriteLine($"Error: {testResult.Error}");
            return 1;
        }

        private static int RunGui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check admin privileges
            if (!AdminHelper.IsRunningAsAdmin())
            {
                var result = MessageBox.Show(
                    "This application requires administrator privileges to modify network settings.\n\n" +
                    "Would you like to restart as administrator?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    if (!AdminHelper.RestartAsAdmin(true))
                    {
                        MessageBox.Show(
                            "Failed to restart as administrator.\n\n" +
                            "Please right-click the application and select 'Run as administrator'.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return 1;
                    }
                    return 0; // Exit this instance
                }

                // Continue without admin (limited functionality)
                MessageBox.Show(
                    "Running without administrator privileges.\n\n" +
                    "Some features will not be available.",
                    "Limited Mode",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            try
            {
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Error("Application crashed", ex);
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                LoggingService.Instance?.Info("NetworkConfigApp exiting...");
                LoggingService.Instance?.Dispose();
            }
        }
    }
}
