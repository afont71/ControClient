using System.Text.RegularExpressions;

namespace ControClient.Domain.Auditing;

/// <summary>
/// Redacción de material sensible antes de persistir texto en auditoría o logs
/// (SEC-07). No sustituye a "no registrar secretos": es la última barrera.
/// </summary>
public static class Redactor
{
    private static readonly Regex SensitivePairs = new(
        @"(?<key>\b(?:password|passwd|pwd|contrasena|contraseña|clave|token|secret|api[_-]?key|apikey)\b\s*[:=]\s*)(?<value>[^\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlCredentials = new(
        @"(?<scheme>[a-z][a-z0-9+.-]*://)(?<user>[^:@/\s]+):(?<pass>[^@/\s]+)@",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var result = SensitivePairs.Replace(text, m => $"{m.Groups["key"].Value}***");
        result = UrlCredentials.Replace(result, m => $"{m.Groups["scheme"].Value}***:***@");
        return result;
    }
}

