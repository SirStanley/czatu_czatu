using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading; // Potrzebne do DispatcherTimer
using CzatuCzatu.Models;
using CzatuCzatu.Services;
using MySqlConnector;

namespace CzatuCzatu.Views
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // POLA KLASY
        private DispatcherTimer _timer;
        private int _activeChatId = 0; // Przechowuje ID osoby, z którą aktualnie piszemy

        public MainWindow()
        {
            InitializeComponent();

            // 1. Wyświetlanie nazwy zalogowanego użytkownika
            if (UserSession.CurrentUsername != null)
            {
                LblCurrentUsername.Text = UserSession.CurrentUsername;
            }

            // 2. Test połączenia
            var db = new DatabaseService();
            if (db.TestConnection())
                this.Title = "Czatu-Czatu - Połączono";
            else
                this.Title = "Czatu-Czatu - Brak połączenia z bazą";

            // 3. Ładowanie kontaktów na start
            LoadContacts();

            // 4. Konfiguracja Timera do odświeżania wiadomości "na żywo"
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1); // Odświeżaj co 1 sekundę
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // --- LOGIKA TIMERA ---
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Odświeżaj wiadomości tylko jeśli wybraliśmy kogoś z listy
            if (_activeChatId != 0)
            {
                LoadMessages(_activeChatId);
            }
        }

        private void LoadContacts()
        {
            LstContacts.Items.Clear();

            try
            {
                using (var conn = new DatabaseService().GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT u.id, u.username 
                        FROM friends f
                        JOIN users u ON (f.user_id = u.id OR f.friend_id = u.id)
                        WHERE (f.user_id = @myId OR f.friend_id = @myId) 
                        AND u.id != @myId";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                LstContacts.Items.Add(new ContactItem
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("username")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania kontaktów: " + ex.Message);
            }
        }

        private void BtnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            AddFriendWindow addWin = new AddFriendWindow();
            addWin.Owner = this;
            if (addWin.ShowDialog() == true)
            {
                LoadContacts();
            }
        }

        private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtMessage.Text.Trim();

            if (_activeChatId == 0) return;
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                using (var conn = new DatabaseService().GetConnection())
                {
                    conn.Open();
                    string sql = @"INSERT INTO messages (sender_id, receiver_id, message_type, content) 
                                   VALUES (@myId, @friendId, 'text', @txt)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                        cmd.Parameters.AddWithValue("@txt", text);

                        cmd.ExecuteNonQuery();
                    }
                }

                // Dodajemy od razu do UI i czyścimy pole
                AddMessageToUI(text, true);
                TxtMessage.Clear();
                ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd wysyłania: " + ex.Message);
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop(); // Zatrzymujemy timer przed wylogowaniem
            UserSession.CurrentUserId = 0;
            UserSession.CurrentUsername = null;

            WelcomeWindow welcome = new WelcomeWindow();
            welcome.Show();
            this.Close();
        }

        private void LstContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstContacts.SelectedItem is ContactItem selectedFriend)
            {
                LblChatPartner.Text = "Rozmowa z: " + selectedFriend.Name;
                _activeChatId = selectedFriend.Id; // Ustawiamy aktywnego rozmówcę dla Timera

                LoadMessages(_activeChatId);
            }
        }

        private void LoadMessages(int friendId)
        {
            // Zapamiętujemy pozycję scrolla, aby nie skakał co sekundę, jeśli użytkownik coś czyta
            bool isAtBottom = ChatScrollViewer.VerticalOffset >= ChatScrollViewer.ScrollableHeight;

            // Pobieramy wiadomości z bazy
            try
            {
                using (var conn = new DatabaseService().GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT sender_id, content 
                        FROM messages 
                        WHERE (sender_id = @myId AND receiver_id = @friendId) 
                           OR (sender_id = @friendId AND receiver_id = @myId) 
                        ORDER BY sent_at ASC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        cmd.Parameters.AddWithValue("@friendId", friendId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            // Sprawdzamy, czy liczba wiadomości się zmieniła, żeby uniknąć mrugania
                            // (Uproszczona wersja: czyścimy i dodajemy, jeśli są nowe lub przy zmianie kontaktu)
                            ChatItemsControl.Items.Clear();

                            while (reader.Read())
                            {
                                int senderId = reader.GetInt32("sender_id");
                                string text = reader.GetString("content");
                                bool isMe = (senderId == UserSession.CurrentUserId);
                                AddMessageToUI(text, isMe);
                            }
                        }
                    }
                }

                if (isAtBottom) ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception ex)
            {
                // Nie wyświetlamy MessageBoxa w Timerze, bo zasypie użytkownika błędami
                System.Diagnostics.Debug.WriteLine("Błąd Timera: " + ex.Message);
            }
        }

        private void AddMessageToUI(string text, bool isMe)
        {
            var bubbleColor = isMe
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"))
                : Brushes.LightGray;

            Border bubble = new Border
            {
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(isMe ? 50 : 5, 5, isMe ? 5 : 50, 5),
                MaxWidth = 400,
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = bubbleColor
            };

            TextBlock msgBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = isMe ? Brushes.White : Brushes.Black
            };

            bubble.Child = msgBlock;
            ChatItemsControl.Items.Add(bubble);
        }

        public class ContactItem
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}