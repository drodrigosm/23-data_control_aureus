using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AureusControl.Core.Models;

namespace AureusControl.Core.Services
{
    public sealed class DbConnectionSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _settingsFilePath;

        public DbConnectionSettingsStore()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AureusControl");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "dbsettings.json");
        }

        public DbConnectionSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return new DbConnectionSettings();

                var json = File.ReadAllText(_settingsFilePath);
                var persisted = JsonSerializer.Deserialize<PersistedDbConnectionSettings>(json, JsonOptions);
                var settings = persisted?.ToRuntimeSettings();
                return settings ?? new DbConnectionSettings();
            }
            catch
            {
                return new DbConnectionSettings();
            }
        }

        public void Save(DbConnectionSettings settings)
        {
            var persisted = PersistedDbConnectionSettings.FromRuntimeSettings(settings);
            var json = JsonSerializer.Serialize(persisted, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        private sealed class PersistedDbConnectionSettings
        {
            public string Host { get; set; } = "192.168.1.43";

            public int Port { get; set; } = 3306;

            public string Database { get; set; } = "db_botaureus";

            public string Username { get; set; } = "app_readonly";

            public string EncryptedPassword { get; set; } = string.Empty;

            public bool AutoConnectOnStartup { get; set; } = true;

            public static PersistedDbConnectionSettings FromRuntimeSettings(DbConnectionSettings settings)
            {
                return new PersistedDbConnectionSettings
                {
                    Host = settings.Host,
                    Port = settings.Port,
                    Database = settings.Database,
                    Username = settings.Username,
                    EncryptedPassword = EncryptPassword(settings.Password),
                    AutoConnectOnStartup = settings.AutoConnectOnStartup
                };
            }

            public DbConnectionSettings ToRuntimeSettings()
            {
                return new DbConnectionSettings
                {
                    Host = Host,
                    Port = Port,
                    Database = Database,
                    Username = Username,
                    Password = DecryptPassword(EncryptedPassword),
                    AutoConnectOnStartup = AutoConnectOnStartup
                };
            }
        }

        private static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            var plainBytes = Encoding.UTF8.GetBytes(password);
            var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrWhiteSpace(encryptedPassword))
                return string.Empty;

            try
            {
                var protectedBytes = Convert.FromBase64String(encryptedPassword);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
