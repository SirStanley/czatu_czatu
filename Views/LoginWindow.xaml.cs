using System;
using System.Windows;
using CzatuCzatu.Services;
using CzatuCzatu.Models;
using MySqlConnector;
using System.Windows.Input;

namespace CzatuCzatu.Views
{
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
                MessageBox.Show("Podaj login i hasło!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    // 2. Jeśli logowanie się powiodło
                    if (loggedUserId != -1)
                    {
                        UserSession.CurrentUserId = loggedUserId;
                        UserSession.CurrentUsername = loggedUsername;

                        // Aktualizacja statusu online
                        string updateStatusSql = "UPDATE users SET is_online = 1 WHERE id = @id";
                        using (var updateCmd = new MySqlCommand(updateStatusSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@id", loggedUserId);
                            updateCmd.ExecuteNonQuery();
                        }

                        MessageBox.Show($"Witaj {UserSession.CurrentUsername}! Logowanie pomyślne.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                        MainWindow chatWin = new MainWindow();
                        chatWin.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Błędny login lub hasło.", "Błąd logowania", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas logowania: " + ex.Message, "Błąd bazy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            new WelcomeWindow().Show();
            this.Close();
        }

        private void BtnShowPassword_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TxtVisiblePassword.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtVisiblePassword.Visibility = Visibility.Visible;
        }

        // Zmiana na MouseButtonEventArgs (dla PreviewMouseUp/MouseUp)
        private void BtnShowPassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HidePassword();
        }

        // MouseEventArgs jest OK dla MouseLeave
        private void BtnShowPassword_MouseLeave(object sender, MouseEventArgs e)
        {
            HidePassword();
        }

        private void HidePassword()
        {
            TxtVisiblePassword.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
            TxtPassword.Focus();
        }
        private void BtnGoToRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow regWin = new RegisterWindow();
            regWin.Show();
            this.Close();
        }
    }
}