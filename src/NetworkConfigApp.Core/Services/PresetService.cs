using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkConfigApp.Core.Models;
using Newtonsoft.Json;

namespace NetworkConfigApp.Core.Services
{
    /// <summary>
    /// Manages network configuration presets with optional DPAPI encryption.
    ///
    /// Algorithm: Presets are stored as JSON files in the presets directory.
    /// Encrypted presets use Windows DPAPI (user-scoped) for protection.
    ///
    /// Performance: File I/O is async where possible. Presets are loaded
    /// on demand, not cached, to ensure freshness.
    ///
    /// Security: DPAPI encryption is tied to the current Windows user account.
    /// Encrypted preset files cannot be read by other users or on other machines.
    /// </summary>
    public class PresetService : IPresetService
    {
        private readonly string _presetsDirectory;
        private const string ENCRYPTED_EXTENSION = ".enc";
        private const string PLAIN_EXTENSION = ".json";

        public PresetService() : this(GetDefaultPresetsDirectory())
        {
        }

        public PresetService(string presetsDirectory)
        {
            _presetsDirectory = presetsDirectory ?? GetDefaultPresetsDirectory();
            EnsureDirectoryExists();
        }

        public async Task<Result<IReadOnlyList<Preset>>> GetAllPresetsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var presets = new List<Preset>();
                    var files = Directory.GetFiles(_presetsDirectory, "*.*")
                        .Where(f => f.EndsWith(PLAIN_EXTENSION) || f.EndsWith(ENCRYPTED_EXTENSION + PLAIN_EXTENSION));

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var preset = LoadPresetFromFile(file);
                            if (preset != null)
                            {
                                presets.Add(preset);
                            }
                        }
                        catch
                        {
                            // Skip corrupted files
                            LoggingService.Instance?.Warning($"Skipping corrupted preset file: {file}");
                        }
                    }

                    return Result<IReadOnlyList<Preset>>.Success(
                        presets.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList().AsReadOnly());
                }
                catch (Exception ex)
                {
                    return Result<IReadOnlyList<Preset>>.FromException(ex, "Failed to load presets");
                }
            }, cancellationToken);
        }

        public async Task<Result<Preset>> GetPresetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Result<Preset>.Failure("Preset name is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var safeName = GetSafeFileName(name);

                    // Try encrypted file first
                    var encryptedPath = Path.Combine(_presetsDirectory, safeName + ENCRYPTED_EXTENSION + PLAIN_EXTENSION);
                    if (File.Exists(encryptedPath))
                    {
                        var preset = LoadPresetFromFile(encryptedPath);
                        if (preset != null)
                        {
                            return Result<Preset>.Success(preset);
                        }
                    }

                    // Try plain file
                    var plainPath = Path.Combine(_presetsDirectory, safeName + PLAIN_EXTENSION);
                    if (File.Exists(plainPath))
                    {
                        var preset = LoadPresetFromFile(plainPath);
                        if (preset != null)
                        {
                            return Result<Preset>.Success(preset);
                        }
                    }

                    return Result<Preset>.Failure($"Preset '{name}' not found", ErrorCode.IoError);
                }
                catch (Exception ex)
                {
                    return Result<Preset>.FromException(ex, "Failed to load preset");
                }
            }, cancellationToken);
        }

        public async Task<Result<Preset>> SavePresetAsync(Preset preset, CancellationToken cancellationToken = default)
        {
            if (preset == null)
            {
                return Result<Preset>.Failure("Preset is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var safeName = GetSafeFileName(preset.Name);
                    var extension = preset.IsEncrypted
                        ? ENCRYPTED_EXTENSION + PLAIN_EXTENSION
                        : PLAIN_EXTENSION;
                    var filePath = Path.Combine(_presetsDirectory, safeName + extension);

                    // Delete any existing file with different encryption
                    DeleteExistingPresetFiles(safeName);

                    // Serialize
                    var json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                    var content = preset.IsEncrypted ? Encrypt(json) : json;

                    File.WriteAllText(filePath, content, Encoding.UTF8);

                    LoggingService.Instance?.Info($"Saved preset: {preset.Name}");
                    return Result<Preset>.Success(preset);
                }
                catch (Exception ex)
                {
                    return Result<Preset>.FromException(ex, "Failed to save preset");
                }
            }, cancellationToken);
        }

        public async Task<Result> DeletePresetAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Result.Failure("Preset name is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var safeName = GetSafeFileName(name);
                    var deleted = DeleteExistingPresetFiles(safeName);

                    if (!deleted)
                    {
                        return Result.Failure($"Preset '{name}' not found", ErrorCode.IoError);
                    }

                    LoggingService.Instance?.Info($"Deleted preset: {name}");
                    return Result.Success();
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to delete preset");
                }
            }, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<Preset>>> ImportPresetsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Result<IReadOnlyList<Preset>>.Failure("File not found", ErrorCode.IoError);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    var presets = JsonConvert.DeserializeObject<List<Preset>>(json);

                    if (presets == null || presets.Count == 0)
                    {
                        return Result<IReadOnlyList<Preset>>.Failure("No presets found in file", ErrorCode.InvalidInput);
                    }

                    // Save each imported preset
                    var imported = new List<Preset>();
                    foreach (var preset in presets)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var saveResult = SavePresetAsync(preset, cancellationToken).Result;
                        if (saveResult.IsSuccess)
                        {
                            imported.Add(preset);
                        }
                    }

                    LoggingService.Instance?.Info($"Imported {imported.Count} presets from {filePath}");
                    return Result<IReadOnlyList<Preset>>.Success(imported.AsReadOnly());
                }
                catch (JsonException)
                {
                    return Result<IReadOnlyList<Preset>>.Failure("Invalid preset file format", ErrorCode.InvalidInput);
                }
                catch (Exception ex)
                {
                    return Result<IReadOnlyList<Preset>>.FromException(ex, "Failed to import presets");
                }
            }, cancellationToken);
        }

        public async Task<Result> ExportPresetsAsync(
            string filePath,
            IEnumerable<Preset> presets = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return Result.Failure("File path is required", ErrorCode.InvalidInput);
            }

            return await Task.Run(async () =>
            {
                try
                {
                    // If no presets specified, export all
                    if (presets == null)
                    {
                        var allResult = await GetAllPresetsAsync(cancellationToken);
                        if (!allResult.IsSuccess)
                        {
                            return Result.Failure(allResult.Error, allResult.ErrorCode);
                        }
                        presets = allResult.Value;
                    }

                    var presetList = presets.ToList();
                    if (presetList.Count == 0)
                    {
                        return Result.Failure("No presets to export", ErrorCode.InvalidInput);
                    }

                    // Serialize without encryption for export
                    var exportPresets = presetList.Select(p => p.WithEncryption(false)).ToList();
                    var json = JsonConvert.SerializeObject(exportPresets, Formatting.Indented);

                    File.WriteAllText(filePath, json, Encoding.UTF8);

                    LoggingService.Instance?.Info($"Exported {presetList.Count} presets to {filePath}");
                    return Result.Success();
                }
                catch (Exception ex)
                {
                    return Result.FromException(ex, "Failed to export presets");
                }
            }, cancellationToken);
        }

        public async Task<Result<Preset>> CreateFromCurrentAsync(
            string name,
            string adapterName,
            NetworkConfiguration config,
            bool encrypt = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Result<Preset>.Failure("Preset name is required", ErrorCode.InvalidInput);
            }

            if (config == null)
            {
                return Result<Preset>.Failure("Configuration is required", ErrorCode.InvalidInput);
            }

            var preset = Preset.Create(
                name,
                config,
                $"Created from {adapterName} on {DateTime.Now:g}",
                adapterName,
                encrypt);

            return await SavePresetAsync(preset, cancellationToken);
        }

        private Preset LoadPresetFromFile(string filePath)
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);

            // Check if encrypted
            if (filePath.EndsWith(ENCRYPTED_EXTENSION + PLAIN_EXTENSION))
            {
                content = Decrypt(content);
            }

            return JsonConvert.DeserializeObject<Preset>(content);
        }

        private bool DeleteExistingPresetFiles(string safeName)
        {
            var deleted = false;

            var encryptedPath = Path.Combine(_presetsDirectory, safeName + ENCRYPTED_EXTENSION + PLAIN_EXTENSION);
            if (File.Exists(encryptedPath))
            {
                File.Delete(encryptedPath);
                deleted = true;
            }

            var plainPath = Path.Combine(_presetsDirectory, safeName + PLAIN_EXTENSION);
            if (File.Exists(plainPath))
            {
                File.Delete(plainPath);
                deleted = true;
            }

            return deleted;
        }

        private string Encrypt(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private string Decrypt(string encryptedText)
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private string GetSafeFileName(string name)
        {
            var safe = name.ToLowerInvariant();
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }
            safe = safe.Replace(' ', '_');
            return safe;
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_presetsDirectory))
                {
                    Directory.CreateDirectory(_presetsDirectory);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance?.Error("Failed to create presets directory", ex);
            }
        }

        private static string GetDefaultPresetsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NetworkConfigApp", "presets");
        }
    }
}
