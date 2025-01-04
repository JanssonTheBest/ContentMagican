using System.Security.Cryptography;
using System.Text;

namespace ContentMagican.Services
{
    public static class CryptService
    {
        /// <summary>
        /// Encrypts the given plaintext using AES encryption with the specified key.
        /// </summary>
        /// <param name="plainText">The plaintext to encrypt.</param>
        /// <param name="key">The encryption key as a string.</param>
        /// <returns>The encrypted text as a Base64-encoded string.</returns>
        public static string Encrypt(string plainText, string key)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            // Derive a 256-bit key from the provided key string using SHA256
            byte[] keyBytes;
            using (SHA256 sha256 = SHA256.Create())
            {
                keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.GenerateIV(); // Generate a new IV for each encryption

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // Prepend the IV to the encrypted data
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }

                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        /// <summary>
        /// Decrypts the given ciphertext using AES decryption with the specified key.
        /// </summary>
        /// <param name="cipherText">The encrypted text as a Base64-encoded string.</param>
        /// <param name="key">The decryption key as a string.</param>
        /// <returns>The decrypted plaintext.</returns>
        public static string Decrypt(string cipherText, string key)
        {
            if (cipherText == null)
                throw new ArgumentNullException(nameof(cipherText));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            byte[] fullCipher = Convert.FromBase64String(cipherText);

            // Derive a 256-bit key from the provided key string using SHA256
            byte[] keyBytes;
            using (SHA256 sha256 = SHA256.Create())
            {
                keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;

                // Extract the IV from the beginning of the cipher text
                int ivLength = aesAlg.BlockSize / 8; // 16 bytes for AES
                if (fullCipher.Length < ivLength)
                    throw new ArgumentException("Invalid cipher text. Length is too short.", nameof(cipherText));

                byte[] iv = new byte[ivLength];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aesAlg.IV = iv;

                int cipherStartIndex = iv.Length;
                int cipherLength = fullCipher.Length - cipherStartIndex;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(fullCipher, cipherStartIndex, cipherLength))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    string plaintext = srDecrypt.ReadToEnd();
                    return plaintext;
                }
            }
        }
    }
}
