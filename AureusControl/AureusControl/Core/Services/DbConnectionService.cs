using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AureusControl.Core.Models;
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

        public async Task<(bool Success, string Message, IReadOnlyList<string> LiveTables, IReadOnlyList<string> TestnetTables)> GetBotTablesAsync(
            DbConnectionSettings settings,
            CancellationToken cancellationToken = default)
        {
            var validation = await TestConnectionAsync(settings, cancellationToken);
            if (!validation.IsConnected)
                return (false, validation.Message, Array.Empty<string>(), Array.Empty<string>());

            var liveTables = new List<string>();
            var testnetTables = new List<string>();

            try
            {
                await using var connection = new MySqlConnection(BuildConnectionString(settings));
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT TABLE_NAME
                    FROM information_schema.tables
                    WHERE TABLE_SCHEMA = @schema
                    ORDER BY TABLE_NAME;";

                await using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@schema", settings.Database);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var tableName = reader.GetString(0);
                    if (tableName.EndsWith("_testnet", StringComparison.OrdinalIgnoreCase))
                        testnetTables.Add(tableName);
                    else
                        liveTables.Add(tableName);
                }

                return (true, "Tablas cargadas.", liveTables, testnetTables);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, Array.Empty<string>(), Array.Empty<string>());
            }
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
