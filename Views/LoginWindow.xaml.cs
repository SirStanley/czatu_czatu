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
                // Szukamy użytkownika o podanym loginie i haśle
                string sql = "SELECT id, username FROM users WHERE username = @user AND password_hash = @pass";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@pass", pass);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // SUKCES: Zapisujemy dane do sesji
                            UserSession.CurrentUserId = reader.GetInt32("id");
                            UserSession.CurrentUsername = reader.GetString("username");

                            MessageBox.Show($"Witaj {UserSession.CurrentUsername}! Logowanie pomyślne.");

                            // Tu otworzymy główne okno czatu (MainWindow)
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
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd: " + ex.Message);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        new WelcomeWindow().Show();
        this.Close();
    }
}