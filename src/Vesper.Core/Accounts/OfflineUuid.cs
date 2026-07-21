using System.Security.Cryptography;
using System.Text;

namespace Vesper.Core.Accounts;

public static class OfflineUuid
{
    public const string Prefix = "OfflinePlayer:";

    public static string Hex(string username)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(Prefix + username));
        data[6] = (byte)((data[6] & 0x0F) | 0x30);
        data[8] = (byte)((data[8] & 0x3F) | 0x80);
        return Convert.ToHexString(data).ToLowerInvariant();
    }

    public static string Dashed(string username)
    {
        var hex = Hex(username);
        return string.Join('-', hex[..8], hex[8..12], hex[12..16], hex[16..20], hex[20..]);
    }
}
