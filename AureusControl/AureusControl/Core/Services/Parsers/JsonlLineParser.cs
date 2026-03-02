// JsonlLineParser.cs
// Convierte una línea JSON (JSONL) en un diccionario top-level (clave->valor string). No recursivo: rápido para millones de líneas.

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AureusControl.Core.Services.Parsers
{
    public static class JsonlLineParser
    {
        public static Dictionary<string, string> ParseToMap(string jsonLine)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(jsonLine)) return map;

            try
            {
                using var doc = JsonDocument.Parse(jsonLine);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.ToString();
                }
            }
            catch
            {
                map["__parse_error__"] = "invalid_json";
            }

            return map;
        }
    }
}