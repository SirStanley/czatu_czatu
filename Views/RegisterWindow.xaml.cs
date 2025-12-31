using System;
using System.Windows;
using CzatuCzatu.Services;
using MySqlConnector;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace CzatuCzatu.Views
{
    public partial class RegisterWindow : Window
    {
        private DatabaseService _dbService = new DatabaseService();

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string user = TxtUsername.Text.Trim();
            string pass = TxtPassword.Password;
            string confirm = TxtConfirmPassword.Password;

            // --- TWOJA WERYFIKACJA DANYCH ---

            // 1. Czy pola nie są puste?
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Uzupełnij wszystkie pola!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Czy hasła się zgadzają?
            if (pass != confirm)
            {
                MessageBox.Show("Hasła muszą być identyczne!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Walidacja znaków (tylko litery i cyfry, min 3 znaki)
            if (!Regex.IsMatch(user, @"^[a-zA-Z0-9]{3,20}$"))
            {
                MessageBox.Show("Login musi mieć od 3 do 20 znaków (tylko litery i cyfry)!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- ZAPIS DO BAZY ---
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();

                    // Sprawdzenie czy użytkownik już istnieje (prewencja duplikatów)
                    string checkSql = "SELECT COUNT(*) FROM users WHERE username = @user";
                    using (var checkCmd = new MySqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@user", user);
                        long count = Convert.ToInt64(checkCmd.ExecuteScalar());
                        if (count > 0)
                        {
                            MessageBox.Show("Ta nazwa użytkownika jest już zajęta!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Wstawienie nowego użytkownika
                    string insertSql = "INSERT INTO users (username, password_hash) VALUES (@user, @pass)";
                    using (var insertCmd = new MySqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@user", user);
                        insertCmd.Parameters.AddWithValue("@pass", pass); // Na razie tekst jawny, potem dodamy haszowanie

                        insertCmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("Konto zostało założone pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Powrót do ekranu powitalnego
                    WelcomeWindow welcome = new WelcomeWindow();
                    welcome.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas rejestracji: " + ex.Message, "Błąd bazy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            WelcomeWindow welcome = new WelcomeWindow();
            welcome.Show();
            this.Close();
        }
        // --- Logika dla pierwszego hasła ---
        private void BtnShowPassword_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TxtVisiblePassword.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtVisiblePassword.Visibility = Visibility.Visible;
        }

        private void BtnShowPassword_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TxtVisiblePassword.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
            TxtPassword.Focus();
        }

        // --- Logika dla powtórzenia hasła ---
        private void BtnShowConfirmPassword_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TxtVisibleConfirmPassword.Text = TxtConfirmPassword.Password;
            TxtConfirmPassword.Visibility = Visibility.Collapsed;
            TxtVisibleConfirmPassword.Visibility = Visibility.Visible;
        }

        private void BtnShowConfirmPassword_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TxtVisibleConfirmPassword.Visibility = Visibility.Collapsed;
            TxtConfirmPassword.Visibility = Visibility.Visible;
            TxtConfirmPassword.Focus();
        }
    }
}