using System;
using System.Diagnostics;
using System.Security.Principal;

namespace NetworkConfigApp.Core.Utilities
{
    /// <summary>
    /// Provides utilities for checking and requesting administrator privileges.
    ///
    /// Algorithm: Uses Windows security APIs to check current privilege level
    /// and Process APIs to relaunch with elevation if needed.
    ///
    /// Security: Does not store credentials; relies on Windows UAC for elevation.
    /// </summary>
    public static class AdminHelper
    {
        /// <summary>
        /// Checks if the current process is running with administrator privileges.
        /// </summary>
        /// <returns>True if running as administrator</returns>
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restarts the current application with administrator privileges.
        /// The current process will exit after launching the elevated process.
        /// </summary>
        /// <param name="exitCurrentProcess">If true, exits the current process</param>
        /// <returns>True if elevated process was launched successfully</returns>
        public static bool RestartAsAdmin(bool exitCurrentProcess = true)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? GetExecutablePath(),
                    Verb = "runas", // Request elevation
                    Arguments = GetCommandLineArguments()
                };

                Process.Start(processInfo);

                if (exitCurrentProcess)
                {
                    Environment.Exit(0);
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User declined UAC prompt (ERROR_CANCELLED)
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if admin privileges are required for the requested operation.
        /// </summary>
        /// <param name="operation">Operation type</param>
        /// <returns>True if admin is required</returns>
        public static bool IsAdminRequiredFor(NetworkOperation operation)
        {
            switch (operation)
            {
                case NetworkOperation.SetStaticIp:
                case NetworkOperation.SetDhcp:
                case NetworkOperation.ReleaseRenew:
                case NetworkOperation.FlushDns:
                case NetworkOperation.ChangeMac:
                    return true;

                case NetworkOperation.ReadConfiguration:
                case NetworkOperation.Ping:
                case NetworkOperation.TraceRoute:
                case NetworkOperation.ReadPresets:
                    return false;

                default:
                    return true; // Assume admin required for unknown operations
            }
        }

        /// <summary>
        /// Shows a message explaining why admin rights are needed.
        /// </summary>
        public static string GetAdminRequiredMessage(NetworkOperation operation)
        {
            switch (operation)
            {
                case NetworkOperation.SetStaticIp:
                    return "Administrator privileges are required to change IP address settings.";
                case NetworkOperation.SetDhcp:
                    return "Administrator privileges are required to switch to DHCP.";
                case NetworkOperation.ReleaseRenew:
                    return "Administrator privileges are required to release/renew IP address.";
                case NetworkOperation.FlushDns:
                    return "Administrator privileges are required to flush DNS cache.";
                case NetworkOperation.ChangeMac:
                    return "Administrator privileges are required to change MAC address.";
                default:
                    return "Administrator privileges are required for this operation.";
            }
        }

        private static string GetExecutablePath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }

        private static string GetCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 1)
            {
                return string.Empty;
            }

            // Skip the executable path (first argument)
            return string.Join(" ", args, 1, args.Length - 1);
        }
    }

    /// <summary>
    /// Types of network operations for permission checking.
    /// </summary>
    public enum NetworkOperation
    {
        ReadConfiguration,
        SetStaticIp,
        SetDhcp,
        ReleaseRenew,
        FlushDns,
        ChangeMac,
        Ping,
        TraceRoute,
        ReadPresets
    }
}
