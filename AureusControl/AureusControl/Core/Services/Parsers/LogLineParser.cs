// LogLineParser.cs
// Parser para logs: cada línea es un texto.

namespace AureusControl.Core.Services.Parsers
{
    public static class LogLineParser
    {
        public static string[] Parse(string line)
        {
            return new[] { line ?? "" };
        }
    }
}