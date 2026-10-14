using System;
using System.Security.Cryptography;

namespace GroceryPOS.Services
{
    /// <summary>PIN hashing (PBKDF2 + random salt).</summary>
    public static class Security
    {
        const int Iterations = 20000;

        public static string HashPin(string pin)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash;
            using (var kdf = new Rfc2898DeriveBytes(pin, salt, Iterations)) hash = kdf.GetBytes(32);
            return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string pin, string stored)
        {
            if (string.IsNullOrEmpty(stored) || pin == null) return false;
            var parts = stored.Split(':');
            if (parts.Length != 2) return false;
            byte[] salt, expected;
            try { salt = Convert.FromBase64String(parts[0]); expected = Convert.FromBase64String(parts[1]); }
            catch (FormatException) { return false; }

            byte[] actual;
            using (var kdf = new Rfc2898DeriveBytes(pin, salt, Iterations)) actual = kdf.GetBytes(expected.Length);

            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }
    }
}
