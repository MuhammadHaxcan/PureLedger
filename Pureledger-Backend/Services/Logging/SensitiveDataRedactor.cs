using System.Text.Json;
using System.Text.RegularExpressions;

namespace PureLedger.Services.Logging
{
    public static class SensitiveDataRedactor
    {
        private const int MaxPayloadLength = 12000;
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "confirmPassword",
            "token",
            "refreshToken",
            "accessToken",
            "secret",
            "authorization",
            "cookie",
            "xsrfToken",
            "x-xsrf-token"
        };

        public static string RedactText(string value)
        {
            var truncated = value.Length <= MaxPayloadLength ? value : value[..MaxPayloadLength];

            foreach (var key in SensitiveKeys)
            {
                truncated = Regex.Replace(
                    truncated,
                    $"(?i)(\"?{Regex.Escape(key)}\"?\\s*[:=]\\s*\")([^\"]*)(\")",
                    "$1[REDACTED]$3");

                truncated = Regex.Replace(
                    truncated,
                    $"(?i)(\"?{Regex.Escape(key)}\"?\\s*[:=]\\s*)([^,;\\s}}]+)",
                    "$1[REDACTED]");
            }

            return truncated;
        }

        public static object? Redact(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => RedactObject(element),
                JsonValueKind.Array => element.EnumerateArray().Take(50).Select(Redact).ToList(),
                JsonValueKind.String => RedactText(element.GetString() ?? string.Empty),
                JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static Dictionary<string, object?> RedactObject(JsonElement element)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in element.EnumerateObject().Take(100))
            {
                result[property.Name] = SensitiveKeys.Contains(property.Name)
                    ? "[REDACTED]"
                    : Redact(property.Value);
            }

            return result;
        }

        public static string? SanitizeRequestBody(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                return JsonSerializer.Serialize(Redact(document.RootElement));
            }
            catch
            {
                return RedactText(body);
            }
        }
    }
}
