using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AureusControl.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using MySqlConnector;

namespace AureusControl.Core.Services
{
    public sealed class DbConnectionService
    {
        public async Task<(bool IsConnected, string Message)> TestConnectionAsync(DbConnectionSettings settings, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(settings.Host))
                return (false, "El host es obligatorio.");

            if (settings.Port <= 0)
                return (false, "El puerto debe ser mayor que cero.");

            if (string.IsNullOrWhiteSpace(settings.Database))
                return (false, "La base de datos es obligatoria.");

            if (string.IsNullOrWhiteSpace(settings.Username))
                return (false, "El usuario es obligatorio.");

            var connectionString = BuildConnectionString(settings);

            try
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await connection.CloseAsync();

                return (true, "Conexión correcta.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message, IReadOnlyList<SummaryRecord> Records)> GetRunningInstancesSummaryAsync(
            DbConnectionSettings settings,
            bool liveMode,
            CancellationToken cancellationToken = default)
        {
            var validation = await TestConnectionAsync(settings, cancellationToken);
            if (!validation.IsConnected)
                return (false, validation.Message, Array.Empty<SummaryRecord>());

            var records = new List<SummaryRecord>();
            var tableName = liveMode ? "bot_instances" : "bot_instances_testnet";
            var hiddenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ip", "tailscale_ip", "api_port" };

            try
            {
                await using var connection = new MySqlConnection(BuildConnectionString(settings));
                await connection.OpenAsync(cancellationToken);

                var sql = $@"
                    SELECT *
                    FROM `{tableName}`
                    WHERE LOWER(COALESCE(status, '')) = 'running'";

                await using var cmd = new MySqlCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var fields = new List<SummaryField>();
                    string? botId = null;
                    string? executionName = null;
                    string status = string.Empty;

                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var key = reader.GetName(i);
                        var rawValue = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        var value = rawValue?.ToString() ?? "NULL";
                        if (!hiddenFields.Contains(key))
                            fields.Add(new SummaryField { Key = key, Value = value });

                        if (string.Equals(key, "bot_id", StringComparison.OrdinalIgnoreCase))
                            botId = value;
                        else if (string.Equals(key, "execution_name", StringComparison.OrdinalIgnoreCase))
                            executionName = value;
                        else if (string.Equals(key, "status", StringComparison.OrdinalIgnoreCase))
                            status = value;
                    }

                    var title = !string.IsNullOrWhiteSpace(executionName)
                        ? executionName!
                        : (!string.IsNullOrWhiteSpace(botId) ? $"Bot {botId}" : "Instancia running");

                    records.Add(new SummaryRecord
                    {
                        Title = title,
                        Status = status,
                        StatusBrush = ResolveStatusBrush(status),
                        Fields = fields
                    });
                }

                return (true, "Resumen cargado.", records);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, Array.Empty<SummaryRecord>());
            }
        }


        private static Brush ResolveStatusBrush(string? status)
        {
            if (string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Colors.LimeGreen);

            if (string.Equals(status, "stopped", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Colors.Red);

            return new SolidColorBrush(Colors.Orange);
        }

        private static string BuildConnectionString(DbConnectionSettings settings)
        {
            var connectionStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = settings.Host,
                Port = (uint)settings.Port,
                Database = settings.Database,
                UserID = settings.Username,
                Password = settings.Password,
                ConnectionTimeout = 5,
                DefaultCommandTimeout = 10,
                Pooling = true
            };

            return connectionStringBuilder.ConnectionString;
        }
    }
}
