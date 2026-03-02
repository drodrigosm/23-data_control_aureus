using System;
using System.IO;
using System.Linq;
using AureusControl.Core.Models;

namespace AureusControl.Core.Services
{
    public class BotFileLocatorService
    {
        private readonly string _basePath = @"Z:\02-DATA_RUNNING";

        public BotExecutionFiles Locate(string botId)
        {
            var result = new BotExecutionFiles();

            if (string.IsNullOrWhiteSpace(botId) || !Directory.Exists(_basePath))
                return result;

            foreach (var machineDir in Directory.GetDirectories(_basePath))
            {
                var machineName = Path.GetFileName(machineDir);

                var logsFolder = Path.Combine(machineDir, "logs");
                var configFolder = Path.Combine(machineDir, "config_running");
                var analyzerFolder = Path.Combine(machineDir, "analyzer_logs");

                // ===== CSV + LOG (logs folder) =====
                if (Directory.Exists(logsFolder))
                {
                    foreach (var file in Directory.GetFiles(logsFolder))
                    {
                        var name = Path.GetFileName(file);

                        if (name.StartsWith(botId + "_", StringComparison.OrdinalIgnoreCase))
                        {
                            if (name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                result.CsvPath = file;

                            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                                result.LogPath = file;
                        }
                    }
                }

                // ===== CONFIG (config_running) =====
                if (Directory.Exists(configFolder))
                {
                    foreach (var file in Directory.GetFiles(configFolder))
                    {
                        var name = Path.GetFileName(file);

                        if (name.StartsWith(botId + "_", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith("_stock_aureus_config.json", StringComparison.OrdinalIgnoreCase))
                        {
                            result.ConfigPath = file;
                        }
                    }
                }

                // ===== ENTRIES (analyzer_logs) =====
                if (Directory.Exists(analyzerFolder))
                {
                    var entriesFiles = Directory.GetFiles(analyzerFolder)
                        .Where(f =>
                        {
                            var name = Path.GetFileName(f);
                            return name.StartsWith(botId + "_", StringComparison.OrdinalIgnoreCase)
                                   && name.Contains("_entries_")
                                   && name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
                        })
                        .OrderByDescending(f => f) // Último por fecha en nombre
                        .ToList();

                    if (entriesFiles.Any())
                        result.EntriesPath = entriesFiles.First();
                }

                if (result.CsvPath != null ||
                    result.LogPath != null ||
                    result.ConfigPath != null ||
                    result.EntriesPath != null)
                {
                    result.MachineName = machineName;
                    return result;
                }
            }

            return result;
        }
    }
}