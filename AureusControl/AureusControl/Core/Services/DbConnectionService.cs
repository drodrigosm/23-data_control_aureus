using System;
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

            try
            {
                await using var connection = new MySqlConnection(connectionStringBuilder.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await connection.CloseAsync();

                return (true, "Conexión correcta.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
