using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmniDrive.App;

internal sealed record TelegramCredentials(string ApiId, string ApiHash, string PhoneNumber, string Name = "");

internal static class TelegramCredentialStore
{
    private static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive");

    private static readonly string CredentialsPath = Path.Combine(AppDirectory, "telegram-credentials.bin");
    internal static string SessionPath => Path.Combine(AppDirectory, "telegram.session");

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OmniDrive.Telegram.Credentials.v1");

    internal static TelegramCredentials? Load()
    {
        if (!File.Exists(CredentialsPath))
            return null;

        try
        {
            var protectedBytes = File.ReadAllBytes(CredentialsPath);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TelegramCredentials>(clearBytes);
        }
        catch (CryptographicException)
        {
            // Credentials encrypted for another Windows user/profile cannot be recovered here.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static void Save(TelegramCredentials credentials)
    {
        Directory.CreateDirectory(AppDirectory);
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(credentials);
        var protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(CredentialsPath, protectedBytes);
        CryptographicOperations.ZeroMemory(clearBytes);
    }

    internal static void ClearCredentials()
    {
        if (File.Exists(CredentialsPath))
            File.Delete(CredentialsPath);
    }

    internal static void ClearSession()
    {
        if (File.Exists(SessionPath))
            File.Delete(SessionPath);
    }
}
