// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrazyGames;
using _Core.Platform.Services.Leaderboard;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Submits scores to the CrazyGames leaderboard (<c>CrazySDK.User.SubmitScore</c>).
    /// CrazyGames has one leaderboard per game, configured and approved in the developer portal,
    /// which also provides the encryption key; the leaderboard ID argument is ignored.
    /// Scores of guests are ignored by the portal.
    /// </summary>
    public class CrazyGamesLeaderboardService : ILeaderboardService
    {
        private readonly byte[] _key;

        /// <param name="encryptionKey">Base64 AES key from the CrazyGames developer portal.</param>
        /// <exception cref="ArgumentException">The key is not a valid base64 AES-128/192/256 key.</exception>
        public CrazyGamesLeaderboardService(string encryptionKey)
        {
            _key = ParseKey(encryptionKey);
        }

        /// <summary>
        /// True when <paramref name="encryptionKey"/> is a usable base64 AES key.
        /// </summary>
        public static bool IsValidKey(string encryptionKey)
        {
            try
            {
                ParseKey(encryptionKey);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public void SubmitScore(string leaderboardID, long score)
        {
            if (!CrazySDK.IsAvailable || !CrazySDK.IsInitialized)
                return;

            try
            {
                string encrypted = EncryptScore(score, _key);
                CrazySDK.User.SubmitScore(encrypted, score);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void ShowLeaderboard(string leaderboardID)
        {
            // CrazyGames shows the leaderboard on the game page; there is no in-game UI call.
        }

        private static byte[] ParseKey(string encryptionKey)
        {
            if (string.IsNullOrWhiteSpace(encryptionKey))
                throw new ArgumentException("Leaderboard encryption key is empty.");

            byte[] key;
            try
            {
                key = Convert.FromBase64String(encryptionKey.Trim());
            }
            catch (FormatException)
            {
                throw new ArgumentException("Leaderboard encryption key is not valid base64.");
            }

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException($"Leaderboard encryption key has {key.Length} bytes; expected 16, 24 or 32.");

            return key;
        }

        /// <summary>
        /// Encrypts the score the way CrazyGames expects (AES-CTR with a random 12-byte IV,
        /// output = IV + ciphertext + 0x55 marker, base64), matching the SDK's ScoreEncryption sample.
        /// The score stays an integer: the sample's float would print "1E+07" and lose digits above 16.7 million.
        /// </summary>
        private static string EncryptScore(long score, byte[] key)
        {
            byte[] iv = new byte[12];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            byte[] plaintext = Encoding.UTF8.GetBytes(score.ToString(CultureInfo.InvariantCulture));
            byte[] ciphertext = new byte[plaintext.Length];

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] counter = new byte[16];
                    Buffer.BlockCopy(iv, 0, counter, 0, 12);
                    counter[15] = 1;

                    for (int i = 0; i < plaintext.Length; i += 16)
                    {
                        byte[] keystream = encryptor.TransformFinalBlock(counter, 0, 16);
                        int blockSize = Math.Min(16, plaintext.Length - i);
                        for (int j = 0; j < blockSize; j++)
                            ciphertext[i + j] = (byte)(plaintext[i + j] ^ keystream[j]);

                        // Increment the 32-bit big-endian block counter.
                        for (int k = 15; k >= 12; k--)
                        {
                            if (++counter[k] != 0)
                                break;
                        }
                    }
                }
            }

            byte[] result = new byte[iv.Length + ciphertext.Length + 1];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
            result[result.Length - 1] = 0x55;

            return Convert.ToBase64String(result);
        }
    }
}
#endif
