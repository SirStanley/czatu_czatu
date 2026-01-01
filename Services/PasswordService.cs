using BCrypt.Net;

namespace CzatuCzatu.Services
{
    public static class PasswordService
    {
        // Zamienia zwykłe hasło na bezpieczny hash
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Sprawdza, czy wpisane hasło pasuje do hasha z bazy
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}