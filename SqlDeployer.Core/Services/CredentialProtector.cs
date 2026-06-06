using System.Security.Cryptography;
using System.Text;

namespace SqlDeployer.Services;

// Encrypts/decrypts a saved password. On Windows it uses DPAPI (CurrentUser),
// so the blob is only decryptable by the same Windows user and is never stored
// as plaintext. A base64 fallback keeps it usable off Windows (e.g. CI/tests).
public static class CredentialProtector
{
    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        if (OperatingSystem.IsWindows())
            bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(bytes);
    }

    public static string Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            if (OperatingSystem.IsWindows())
                bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // Blob from another user/machine, or corrupt — treat as no saved password.
            return string.Empty;
        }
    }
}
