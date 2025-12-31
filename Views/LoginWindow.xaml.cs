using System.Windows;
using CzatuCzatu.Services;
using CzatuCzatu.Models;
using MySqlConnector;

namespace CzatuCzatu.Views;

public partial class LoginWindow : Window
{
    private DatabaseService _dbService = new DatabaseService();

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        string user = TxtUsername.Text.Trim();
        string pass = TxtPassword.Password;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            MessageBox.Show("Podaj login i hasło!");
            return;
        }

        try
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();

                // 1. Sprawdzamy czy użytkownik istnieje
                string sql = "SELECT id, username FROM users WHERE username = @user AND password_hash = @pass";
                int loggedUserId = -1;
                string loggedUsername = "";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@pass", pass);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            loggedUserId = reader.GetInt32("id");
                            loggedUsername = reader.GetString("username");
                        }
                    }
                }

                // 2. Jeśli logowanie się powiodło (znaleźliśmy ID)
                if (loggedUserId != -1)
                {
                    // Zapisujemy dane do sesji statycznej
                    UserSession.CurrentUserId = loggedUserId;
                    UserSession.CurrentUsername = loggedUsername;

                    // --- NOWOŚĆ: AKTUALIZACJA STATUSU ONLINE ---
                    string updateStatusSql = "UPDATE users SET is_online = 1 WHERE id = @id";
                    using (var updateCmd = new MySqlCommand(updateStatusSql, conn))
                    {
                        updateCmd.Parameters.AddWithValue("@id", loggedUserId);
                        updateCmd.ExecuteNonQuery();
                    }

                    MessageBox.Show($"Witaj {UserSession.CurrentUsername}! Logowanie pomyślne.");

                    // Otwieramy główne okno
                    MainWindow chatWin = new MainWindow();
                    chatWin.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Błędny login lub hasło.");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd podczas logowania: " + ex.Message);
        }
    }
    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        new WelcomeWindow().Show();
        this.Close();
    }
}