using BCrypt.Net;

namespace CzatuCzatu.Services
{
    public static class PasswordService
    {
        // Zamiana na hash
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Sprawdza  hasha z bazy
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}