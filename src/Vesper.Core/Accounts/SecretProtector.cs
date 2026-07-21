using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Vesper.Core.Accounts;

public interface ISecretProtector
{
    bool IsEncrypted { get; }

    string Protect(string value);

    string? Unprotect(string value);
}

public static class SecretProtector
{
    public static ISecretProtector Create() =>
        OperatingSystem.IsWindows() ? new DpapiSecretProtector() : new UnencryptedSecretProtector();
}

[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "vesper-launcher"u8.ToArray();

    public bool IsEncrypted => true;

    public string Protect(string value) => Convert.ToBase64String(
        ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));

    public string? Unprotect(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }
}

public sealed class UnencryptedSecretProtector : ISecretProtector
{
    public bool IsEncrypted => false;

    public string Protect(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    public string? Unprotect(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
