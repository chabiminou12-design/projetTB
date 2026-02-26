using System;
using System.Linq;
using System.Security.Cryptography;

namespace Stat.Helpers
{
    public static class PasswordHelper
    {
        public static string GenerateRandomPassword()
        {
            const string lowercaseChars = "abcdefghijklmnopqrstuvwxyz";
            const string uppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digitChars = "0123456789";
            const string specialChars = "@*#%&£=+$";
            const string allChars = lowercaseChars + uppercaseChars + digitChars + specialChars;

            const int passwordLength = 9;

            var password = new char[passwordLength];
            var random = new Random();

            // 1. Ensure the password meets all complexity requirements
            password[0] = lowercaseChars[random.Next(lowercaseChars.Length)];
            password[1] = uppercaseChars[random.Next(uppercaseChars.Length)];
            password[2] = digitChars[random.Next(digitChars.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];

            // 2. Fill the rest of the password with random characters from the full set
            for (int i = 4; i < passwordLength; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // 3. Shuffle the characters to ensure the required characters are not always at the start
            // This uses a modern version of the Fisher-Yates shuffle algorithm
            for (int i = passwordLength - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]); // Swap characters
            }

            return new string(password);
        }
    }
}