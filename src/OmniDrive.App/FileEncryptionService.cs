using System.Security.Cryptography;

namespace OmniDrive.App;

/// <summary>
/// Streaming password-based encryption for OmniDrive packages.
/// Format: magic (8), salt (16), IV (16), AES-CBC ciphertext, HMAC-SHA256 (32).
/// PBKDF2 derives separate encryption and authentication keys.
/// </summary>
internal static class FileEncryptionService
{
    private static readonly byte[] Magic = "ODENC001"u8.ToArray();
    private const int SaltLength = 16;
    private const int IvLength = 16;
    private const int MacLength = 32;
    private const int Iterations = 600_000;

    public static async Task EncryptFileAsync(string sourcePath, string destinationPath, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("A senha de criptografia não pode ficar vazia.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var keys = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 64);
        try
        {
            using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                await output.WriteAsync(Magic);
                await output.WriteAsync(salt);
                await output.WriteAsync(iv);
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = keys.AsSpan(0, 32).ToArray();
                    aes.IV = iv;
                    using var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                    await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
                    await input.CopyToAsync(crypto);
                    crypto.FlushFinalBlock();
                }

                output.Flush();
                output.Position = 0;
                using var hmac = new HMACSHA256(keys.AsSpan(32, 32).ToArray());
                var mac = await hmac.ComputeHashAsync(new LimitedReadStream(output, output.Length));
                output.Position = output.Length;
                await output.WriteAsync(mac);
                await output.FlushAsync();
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }
    }

    public static async Task DecryptFileAsync(string sourcePath, string destinationPath, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Informe a senha para descriptografar o arquivo.", nameof(password));

        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        if (input.Length < Magic.Length + SaltLength + IvLength + MacLength)
            throw new InvalidDataException("O arquivo não é um pacote criptografado válido do OmniDrive.");

        var magic = new byte[Magic.Length];
        await input.ReadExactlyAsync(magic);
        if (!CryptographicOperations.FixedTimeEquals(magic, Magic))
            throw new InvalidDataException("O arquivo não tem o formato criptografado do OmniDrive.");

        var salt = new byte[SaltLength];
        var iv = new byte[IvLength];
        await input.ReadExactlyAsync(salt);
        await input.ReadExactlyAsync(iv);
        var cipherLength = input.Length - Magic.Length - SaltLength - IvLength - MacLength;
        if (cipherLength <= 0)
            throw new InvalidDataException("O pacote criptografado está vazio ou incompleto.");

        var keys = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 64);
        try
        {
            input.Position = 0;
            using var hmac = new HMACSHA256(keys.AsSpan(32, 32).ToArray());
            var calculatedMac = await hmac.ComputeHashAsync(new LimitedReadStream(input, input.Length - MacLength));
            input.Position = input.Length - MacLength;
            var storedMac = new byte[MacLength];
            await input.ReadExactlyAsync(storedMac);
            if (!CryptographicOperations.FixedTimeEquals(calculatedMac, storedMac))
                throw new CryptographicException("Senha incorreta ou arquivo criptografado danificado.");

            input.Position = Magic.Length + SaltLength + IvLength;
            await using var limitedCipher = new LimitedReadStream(input, cipherLength);
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = keys.AsSpan(0, 32).ToArray();
            aes.IV = iv;
            using var crypto = new CryptoStream(limitedCipher, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true);
            await crypto.CopyToAsync(output);
        }
        catch
        {
            try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }
    }

    public static bool IsEncryptedPackage(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[Magic.Length];
            return stream.Read(header, 0, header.Length) == header.Length
                && CryptographicOperations.FixedTimeEquals(header, Magic);
        }
        catch { return false; }
    }

    private sealed class LimitedReadStream(Stream inner, long remaining) : Stream
    {
        private long _remaining = remaining;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => remaining;
        public override long Position { get => remaining - _remaining; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            var read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0) return 0;
            var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
