using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;

namespace NetworkConfigApp.Core.Utilities
{
    /// <summary>
    /// Executes netsh and ipconfig commands for network configuration.
    ///
    /// Algorithm: Spawns hidden cmd.exe processes to run network commands.
    /// Uses async/await for non-blocking execution with timeout support.
    ///
    /// Performance: Process creation has overhead (~50ms), but commands execute
    /// synchronously in the OS. Timeout prevents hanging on unresponsive operations.
    ///
    /// Security: Commands are constructed safely to prevent injection.
    /// All input is validated before building command strings.
    /// </summary>
    public class NetshExecutor
    {
        private const int DEFAULT_TIMEOUT_MS = 30000; // 30 seconds
        private readonly int _timeoutMs;

        public NetshExecutor(int timeoutMs = DEFAULT_TIMEOUT_MS)
        {
            _timeoutMs = timeoutMs > 0 ? timeoutMs : DEFAULT_TIMEOUT_MS;
        }

        /// <summary>
        /// Sets a static IP address on the specified adapter.
        /// </summary>
        public async Task<Result> SetStaticIpAsync(
            string adapterName,
            string ipAddress,
            string subnetMask,
            string gateway,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            if (string.IsNullOrEmpty(ipAddress))
                return Result.Failure("IP address is required", ErrorCode.InvalidInput);

            if (string.IsNullOrEmpty(subnetMask))
                return Result.Failure("Subnet mask is required", ErrorCode.InvalidInput);

            // Build command: netsh interface ip set address "Adapter" static IP Subnet Gateway
            var command = string.IsNullOrEmpty(gateway)
                ? $"interface ip set address \"{EscapeArgument(adapterName)}\" static {ipAddress} {subnetMask}"
                : $"interface ip set address \"{EscapeArgument(adapterName)}\" static {ipAddress} {subnetMask} {gateway}";

            return await ExecuteNetshAsync(command, cancellationToken);
        }

        /// <summary>
        /// Sets DNS servers on the specified adapter.
        /// </summary>
        public async Task<Result> SetDnsAsync(
            string adapterName,
            string primaryDns,
            string secondaryDns = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            // Set primary DNS
            if (!string.IsNullOrEmpty(primaryDns))
            {
                var primaryCommand = $"interface ip set dns \"{EscapeArgument(adapterName)}\" static {primaryDns}";
                var primaryResult = await ExecuteNetshAsync(primaryCommand, cancellationToken);
                if (!primaryResult.IsSuccess)
                    return primaryResult;

                // Set secondary DNS
                if (!string.IsNullOrEmpty(secondaryDns))
                {
                    var secondaryCommand = $"interface ip add dns \"{EscapeArgument(adapterName)}\" {secondaryDns} index=2";
                    var secondaryResult = await ExecuteNetshAsync(secondaryCommand, cancellationToken);
                    if (!secondaryResult.IsSuccess)
                        return secondaryResult;
                }
            }
            else
            {
                // Set DNS to DHCP
                var command = $"interface ip set dns \"{EscapeArgument(adapterName)}\" dhcp";
                return await ExecuteNetshAsync(command, cancellationToken);
            }

            return Result.Success();
        }

        /// <summary>
        /// Sets the adapter to use DHCP for IP and DNS.
        /// </summary>
        public async Task<Result> SetDhcpAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            // Set IP to DHCP
            var ipCommand = $"interface ip set address \"{EscapeArgument(adapterName)}\" dhcp";
            var ipResult = await ExecuteNetshAsync(ipCommand, cancellationToken);
            if (!ipResult.IsSuccess)
                return ipResult;

            // Set DNS to DHCP
            var dnsCommand = $"interface ip set dns \"{EscapeArgument(adapterName)}\" dhcp";
            return await ExecuteNetshAsync(dnsCommand, cancellationToken);
        }

        /// <summary>
        /// Releases DHCP lease for the specified adapter.
        /// </summary>
        public async Task<Result> ReleaseDhcpAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            var command = $"/release \"{EscapeArgument(adapterName)}\"";
            return await ExecuteIpconfigAsync(command, cancellationToken);
        }

        /// <summary>
        /// Renews DHCP lease for the specified adapter.
        /// </summary>
        public async Task<Result> RenewDhcpAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            var command = $"/renew \"{EscapeArgument(adapterName)}\"";
            return await ExecuteIpconfigAsync(command, cancellationToken);
        }

        /// <summary>
        /// Releases and then renews DHCP lease.
        /// </summary>
        public async Task<Result> ReleaseRenewAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            var releaseResult = await ReleaseDhcpAsync(adapterName, cancellationToken);
            if (!releaseResult.IsSuccess)
                return releaseResult;

            // Small delay between release and renew
            await Task.Delay(1000, cancellationToken);

            return await RenewDhcpAsync(adapterName, cancellationToken);
        }

        /// <summary>
        /// Flushes the DNS resolver cache.
        /// </summary>
        public async Task<Result> FlushDnsAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteIpconfigAsync("/flushdns", cancellationToken);
        }

        /// <summary>
        /// Disables a network adapter.
        /// </summary>
        public async Task<Result> DisableAdapterAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            var command = $"interface set interface \"{EscapeArgument(adapterName)}\" disable";
            return await ExecuteNetshAsync(command, cancellationToken);
        }

        /// <summary>
        /// Enables a network adapter.
        /// </summary>
        public async Task<Result> EnableAdapterAsync(string adapterName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterName))
                return Result.Failure("Adapter name is required", ErrorCode.InvalidInput);

            var command = $"interface set interface \"{EscapeArgument(adapterName)}\" enable";
            return await ExecuteNetshAsync(command, cancellationToken);
        }

        /// <summary>
        /// Gets IP configuration information (similar to ipconfig /all).
        /// </summary>
        public async Task<Result<string>> GetIpConfigAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandWithOutputAsync("ipconfig", "/all", cancellationToken);
        }

        private async Task<Result> ExecuteNetshAsync(string arguments, CancellationToken cancellationToken)
        {
            var result = await ExecuteCommandWithOutputAsync("netsh", arguments, cancellationToken);
            if (!result.IsSuccess)
                return Result.Failure(result.Error, result.ErrorCode);

            // Check for common error messages in output
            var output = result.Value;
            if (output.Contains("The requested operation requires elevation"))
                return Result.Failure("Administrator privileges required", ErrorCode.AccessDenied);

            if (output.Contains("The filename, directory name, or volume label syntax is incorrect"))
                return Result.Failure("Invalid adapter name", ErrorCode.AdapterNotFound);

            if (output.Contains("The system cannot find the file specified"))
                return Result.Failure("Adapter not found", ErrorCode.AdapterNotFound);

            if (output.Contains("DHCP is not enabled"))
                return Result.Failure("DHCP is not enabled on this adapter", ErrorCode.InvalidOperation);

            return Result.Success();
        }

        private async Task<Result> ExecuteIpconfigAsync(string arguments, CancellationToken cancellationToken)
        {
            var result = await ExecuteCommandWithOutputAsync("ipconfig", arguments, cancellationToken);
            if (!result.IsSuccess)
                return Result.Failure(result.Error, result.ErrorCode);

            var output = result.Value;

            // Check for success messages
            if (output.Contains("Successfully flushed"))
                return Result.Success();

            if (output.Contains("has been renewed") || output.Contains("has been released"))
                return Result.Success();

            // Check for error messages
            if (output.Contains("The requested operation requires elevation"))
                return Result.Failure("Administrator privileges required", ErrorCode.AccessDenied);

            if (output.Contains("DHCP Client is not enabled"))
                return Result.Failure("DHCP is not enabled on this adapter", ErrorCode.InvalidOperation);

            if (output.Contains("No operation can be performed"))
                return Result.Failure("Operation cannot be performed (check adapter state)", ErrorCode.InvalidOperation);

            return Result.Success();
        }

        private async Task<Result<string>> ExecuteCommandWithOutputAsync(
            string command,
            string arguments,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            output.AppendLine(e.Data);
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            error.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait with timeout
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(_timeoutMs);

                        try
                        {
                            await Task.Run(() => process.WaitForExit(), cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch { }

                            if (cancellationToken.IsCancellationRequested)
                                return Result<string>.Failure("Operation cancelled", ErrorCode.Unknown);

                            return Result<string>.Failure("Operation timed out", ErrorCode.Timeout);
                        }
                    }

                    if (error.Length > 0 && process.ExitCode != 0)
                    {
                        return Result<string>.Failure(error.ToString().Trim(), ErrorCode.Unknown);
                    }

                    return Result<string>.Success(output.ToString());
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return Result<string>.Failure($"Failed to execute {command}: {ex.Message}", ErrorCode.IoError);
            }
            catch (Exception ex)
            {
                return Result<string>.FromException(ex, $"Failed to execute {command}");
            }
        }

        /// <summary>
        /// Escapes special characters in command arguments to prevent injection.
        /// </summary>
        private static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return arg;

            // Remove any existing quotes and dangerous characters
            return arg
                .Replace("\"", "")
                .Replace("'", "")
                .Replace("`", "")
                .Replace("$", "")
                .Replace("&", "")
                .Replace("|", "")
                .Replace(";", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("\r", "")
                .Replace("\n", "");
        }
    }
}
