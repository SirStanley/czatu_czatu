using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CzatuCzatu.Models;
using CzatuCzatu.Services;
using MySqlConnector;

namespace CzatuCzatu.Views
{
    public partial class MainWindow : Window
    {
        // POLA KLASY
        private DispatcherTimer _timer;
        private int _activeChatId = 0;
        private int _contactUpdateCounter = 0; // Licznik do odświeżania listy kontaktów

        public MainWindow()
        {
            InitializeComponent();

            if (UserSession.CurrentUsername != null)
            {
                LblCurrentUsername.Text = UserSession.CurrentUsername;
            }

            var db = new DatabaseService();
            if (db.TestConnection())
                this.Title = "Czatu-Czatu - Połączono";
            else
                this.Title = "Czatu-Czatu - Brak połączenia z bazą";

            LoadContacts();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // --- LOGIKA TIMERA ---
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // 1. Odświeżaj wiadomości w otwartym czacie (co 1 sekundę)
            if (_activeChatId != 0)
            {
                LoadMessages(_activeChatId);
            }

            // 2. KROK 3: Odświeżanie listy kontaktów i statusów halo (co 3 sekundy)
            _contactUpdateCounter++;
            if (_contactUpdateCounter >= 3)
            {
                LoadContacts();
                _contactUpdateCounter = 0;
            }
        }

        private void LoadContacts()
        {
            // Zapamiętujemy ID wybranego znajomego, aby nie stracić zaznaczenia przy odświeżeniu
            int selectedId = (_activeChatId != 0) ? _activeChatId : -1;

            LstContacts.Items.Clear();

            try
            {
                using (var conn = new Services.DatabaseService().GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT u.id, u.username, u.is_online,
                        (SELECT COUNT(*) FROM messages m WHERE m.sender_id = u.id AND m.receiver_id = @myId AND m.is_read = 0) as new_count
                        FROM friends f
                        JOIN users u ON (f.user_id = u.id OR f.friend_id = u.id)
                        WHERE (f.user_id = @myId OR f.friend_id = @myId) AND u.id != @myId";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var contact = new ContactItem
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("username"),
                                    IsOnline = reader.GetInt32("is_online") == 1,
                                    HasNewMessages = reader.GetInt32("new_count") > 0
                                };

                                LstContacts.Items.Add(contact);

                                // Przywracamy zaznaczenie wizualne
                                if (contact.Id == selectedId)
                                {
                                    LstContacts.SelectedItem = contact;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Błąd LoadContacts: " + ex.Message); }
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
                    string sql = @"INSERT INTO messages (sender_id, receiver_id, message_type, content, is_read) 
                                   VALUES (@myId, @friendId, 'text', @txt, 0)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                        cmd.Parameters.AddWithValue("@txt", text);

                        cmd.ExecuteNonQuery();
                    }
                }

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
            try
            {
                using (var conn = new Services.DatabaseService().GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE users SET is_online = 0 WHERE id = @id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", UserSession.CurrentUserId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Błąd Logout: " + ex.Message); }

            _timer.Stop();
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
                _activeChatId = selectedFriend.Id;
                LblChatPartner.Text = "Rozmowa z: " + selectedFriend.Name;

                // Natychmiastowe oznaczanie wiadomości jako przeczytane po kliknięciu
                try
                {
                    using (var conn = new Services.DatabaseService().GetConnection())
                    {
                        conn.Open();
                        string sql = "UPDATE messages SET is_read = 1 WHERE sender_id = @friendId AND receiver_id = @myId";
                        using (var cmd = new MySqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                            cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Błąd Update Read: " + ex.Message); }

                LoadMessages(_activeChatId);
            }
        }

        private void LoadMessages(int friendId)
        {
            bool isAtBottom = ChatScrollViewer.VerticalOffset >= ChatScrollViewer.ScrollableHeight;

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Błąd LoadMessages: " + ex.Message); }
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
            public bool IsOnline { get; set; }
            public Brush StatusColor => IsOnline ? Brushes.LimeGreen : Brushes.Red;
            public bool HasNewMessages { get; set; }
            public Visibility NewBadgeVisibility => HasNewMessages ? Visibility.Visible : Visibility.Collapsed;
            public override string ToString() => Name;
        }
    }
}