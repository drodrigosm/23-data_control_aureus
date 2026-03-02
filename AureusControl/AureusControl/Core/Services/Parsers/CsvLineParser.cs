// CsvLineParser.cs
// Parser CSV por línea con soporte de comillas. Auto-detecta delimitador entre ',' y ';' usando la cabecera.

using System;
using System.Collections.Generic;

namespace AureusControl.Core.Services.Parsers
{
    public static class CsvLineParser
    {
        public static char DetectDelimiter(string headerLine)
        {
            var commas = CountChar(headerLine, ',');
            var semis = CountChar(headerLine, ';');
            return semis > commas ? ';' : ',';
        }

        private static int CountChar(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == c) n++;
            return n;
        }

        public static string[] Parse(string line, char delimiter)
        {
            var fields = new List<string>(32);
            if (line == null) return Array.Empty<string>();

            var current = "";
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += "\"";
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (!inQuotes && ch == delimiter)
                {
                    fields.Add(current);
                    current = "";
                    continue;
                }

                current += ch;
            }

            fields.Add(current);
            return fields.ToArray();
        }
    }
}