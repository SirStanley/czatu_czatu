using System.Windows;
using System.Runtime.Versioning;
using CzatuCzatu.Services;
using CzatuCzatu.Models;
using MySqlConnector;
using System.Windows.Input;

namespace CzatuCzatu.Views
{
    using MessageBox = System.Windows.MessageBox;
    using MouseEventArgs = System.Windows.Input.MouseEventArgs;
    [SupportedOSPlatform("windows")]
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

                    // 1. Pobieramy ID, Nazwę i HASH hasła dla podanego loginu
                    string sql = "SELECT id, username, password_hash FROM users WHERE username = @user";

                    int loggedUserId = -1;
                    string loggedUsername = "";
                    string storedHash = "";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", user);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                loggedUserId = reader.GetInt32("id");
                                loggedUsername = reader.GetString("username");
                                storedHash = reader.GetString("password_hash");
                            }
                        }
                    }
                    // 2. Weryfikacja hasła za pomocą BCrypt
                    if (loggedUserId != -1 && PasswordService.VerifyPassword(pass, storedHash))
                    {
                        // Logowanie pomyślne - zapisujemy sesję
                        UserSession.CurrentUserId = loggedUserId;
                        UserSession.CurrentUsername = loggedUsername;

                        // Aktualizacja statusu online
                        string updateStatusSql = "UPDATE users SET is_online = 1 WHERE id = @id";
                        using (var updateCmd = new MySqlCommand(updateStatusSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@id", loggedUserId);
                            updateCmd.ExecuteNonQuery();
                        }

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

        private void BtnGoToRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow regWin = new RegisterWindow();
            regWin.Show();
            this.Close();
        }

        private void BtnShowPassword_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TxtVisiblePassword.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtVisiblePassword.Visibility = Visibility.Visible;
        }

        private void BtnShowPassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HidePassword();
        }

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
    }
}