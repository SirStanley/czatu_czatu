using MySqlConnector;
using System;

namespace CzatuCzatu.Services
{
    public class DatabaseService
    {

        private readonly string _connectionString = "Server=localhost;Database=czatu_czatu_db;Uid=root;Pwd=;";

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString);
        }

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
               
                System.Diagnostics.Debug.WriteLine("Błąd bazy: " + ex.Message);
                return false;
            }
        }
    }
}