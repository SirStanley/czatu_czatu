using MySqlConnector;
using System;

namespace CzatuCzatu.Services
{
    public class DatabaseService
    {
        // Dane do połączenia z Twoim XAMPP
        // Server=localhost -> Twój komputer
        // Database=czatu_czatu_db -> Nazwa bazy, którą stworzyłeś (lub stworzysz) w phpMyAdmin
        // Uid=root -> Domyślny użytkownik XAMPP
        // Pwd= -> Domyślnie brak hasła w XAMPP
        private readonly string _connectionString = "Server=localhost;Database=czatu_czatu_db;Uid=root;Pwd=;";

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        // Prosta metoda testowa, którą zaraz wykorzystamy
        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Jeśli nie zadziała, wypisze błąd w konsoli Visual Studio
                System.Diagnostics.Debug.WriteLine("Błąd bazy: " + ex.Message);
                return false;
            }
        }
    }
}