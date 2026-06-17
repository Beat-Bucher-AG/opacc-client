using System.Text;
using System.Text.Json;

namespace Opacc.Client.Operations.Pagination;

internal static class PageCursor
{
    // For Query: encode/decode the #RedoData column rows
    public static string EncodeRows(string[] rows)
    {
        var json = JsonSerializer.Serialize(rows);
        return ToUrlSafeBase64(Encoding.UTF8.GetBytes(json));
    }

    public static string[] DecodeRows(string cursor)
    {
        var json = Encoding.UTF8.GetString(FromUrlSafeBase64(cursor));
        return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }

    // For GetBo: encode/decode the last ID value
    public static string EncodeId(string id)
        => ToUrlSafeBase64(Encoding.UTF8.GetBytes(id));

    public static string DecodeId(string cursor)
        => Encoding.UTF8.GetString(FromUrlSafeBase64(cursor));

    private static string ToUrlSafeBase64(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromUrlSafeBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
        return Convert.FromBase64String(s);
    }
}
