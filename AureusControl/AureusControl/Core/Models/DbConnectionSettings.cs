namespace AureusControl.Core.Models
{
    public sealed class DbConnectionSettings
    {
        public string Host { get; set; } = "192.168.1.43";

        public int Port { get; set; } = 3306;

        public string Database { get; set; } = "db_botaureus";

        public string Username { get; set; } = "app_readonly";

        public string Password { get; set; } = string.Empty;

        public bool AutoConnectOnStartup { get; set; } = true;
    }
}
