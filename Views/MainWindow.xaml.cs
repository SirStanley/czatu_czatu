using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32; // Potrzebne do OpenFileDialog
using CzatuCzatu.Models;
using CzatuCzatu.Services;
using MySqlConnector;

namespace CzatuCzatu.Views
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private int _activeChatId = 0;
        private int _contactUpdateCounter = 0;
        private DatabaseService _dbService = new DatabaseService();

        public MainWindow()
        {
            InitializeComponent();

            if (UserSession.CurrentUsername != null)
            {
                LblCurrentUsername.Text = UserSession.CurrentUsername;
            }

            // Test połączenia przy starcie
            if (_dbService.TestConnection())
                this.Title = "Czatu-Czatu - Połączono";
            else
                this.Title = "Czatu-Czatu - Brak połączenia z bazą";

            LoadContacts();

            // Konfiguracja Timera (odświeżanie co 1 sekundę)
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_activeChatId != 0)
            {
                LoadMessages(_activeChatId);
            }

            _contactUpdateCounter++;
            if (_contactUpdateCounter >= 3)
            {
                LoadContacts();
                _contactUpdateCounter = 0;
            }
        }

        // --- OBSŁUGA PLIKÓW I ZDJĘĆ ---
        private void BtnAttachFile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeChatId == 0)
            {
                MessageBox.Show("Wybierz rozmówcę przed wysłaniem pliku.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Obrazy (*.jpg;*.png;*.gif)|*.jpg;*.png;*.gif|Wszystkie pliki (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(openFileDialog.FileName);

                    // Limit 10 MB
                    if (fileInfo.Length > 10 * 1024 * 1024)
                    {
                        MessageBox.Show("Plik jest za duży! Maksymalny rozmiar to 10 MB.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    string fileName = fileInfo.Name;
                    string ext = fileInfo.Extension.ToLower();

                    // Rozpoznawanie typu
                    string type = (ext == ".jpg" || ext == ".png" || ext == ".gif" || ext == ".jpeg") ? "image" : "file";

                    using (var conn = _dbService.GetConnection())
                    {
                        conn.Open();
                        string sql = @"INSERT INTO messages (sender_id, receiver_id, message_type, content, file_data, file_name, is_read) 
                                       VALUES (@myId, @friendId, @type, @content, @data, @name, 0)";

                        using (var cmd = new MySqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                            cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                            cmd.Parameters.AddWithValue("@type", type);
                            cmd.Parameters.AddWithValue("@content", fileName);
                            cmd.Parameters.AddWithValue("@data", fileBytes);
                            cmd.Parameters.AddWithValue("@name", fileName);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadMessages(_activeChatId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd wysyłania pliku: " + ex.Message);
                }
            }
        }

        private void LoadMessages(int friendId)
        {
            bool isAtBottom = ChatScrollViewer.VerticalOffset >= ChatScrollViewer.ScrollableHeight;

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    string sql = @"SELECT sender_id, content, message_type, file_data, file_name 
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
                                string content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString("content");
                                string type = reader.IsDBNull(reader.GetOrdinal("message_type")) ? "text" : reader.GetString("message_type");

                                // Bezpieczne pobieranie nazwy pliku
                                string? fileName = reader.IsDBNull(reader.GetOrdinal("file_name")) ? null : reader.GetString("file_name");

                                // Pobieranie danych binarnych
                                byte[]? data = reader.IsDBNull(reader.GetOrdinal("file_data")) ? null : (byte[])reader["file_data"];

                                bool isMe = (senderId == UserSession.CurrentUserId);

                                // Przekazujemy 'fileName' jako piąty argument
                                AddMessageToUI(content, isMe, type, data, fileName);
                            }
                        }
                    }
                }
                if (isAtBottom)
                {
                    ChatScrollViewer.Dispatcher.BeginInvoke(new Action(() => ChatScrollViewer.ScrollToEnd()));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Błąd LoadMessages: " + ex.Message);
            }
        }

        private void AddMessageToUI(string text, bool isMe, string? type = "text", byte[]? fileData = null, string? fileName = null)
        {
            // Ustalamy kolor dymka na podstawie tego, kto wysłał wiadomość
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

            // Obsługa różnych typów wiadomości
            if (type == "image" && fileData != null)
            {
                var img = new Image
                {
                    Source = LoadImage(fileData),
                    MaxWidth = 350,
                    Stretch = Stretch.Uniform,
                    Cursor = Cursors.Hand,
                    ToolTip = "Kliknij, aby zapisać zdjęcie na dysku" // Informacja dla użytkownika
                };

                // Podpinamy zdarzenie zapisu
                img.MouseDown += (s, e) => SaveFileToDisk(fileData, fileName);
                bubble.Child = img;
            }
            else if (type == "file" && fileData != null)
            {
                StackPanel fp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Cursor = Cursors.Hand,
                    ToolTip = "Kliknij, aby pobrać plik" // Informacja dla użytkownika
                };

                fp.Children.Add(new TextBlock { Text = "📄 ", FontSize = 16 });
                fp.Children.Add(new TextBlock
                {
                    Text = text,
                    TextDecorations = TextDecorations.Underline,
                    Foreground = isMe ? Brushes.White : Brushes.Blue
                });

                // Podpinamy zdarzenie zapisu do całego panelu pliku
                fp.MouseDown += (s, e) => SaveFileToDisk(fileData, fileName);
                bubble.Child = fp;
            }
            else // Standardowy tekst
            {
                bubble.Child = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Foreground = isMe ? Brushes.White : Brushes.Black
                };
            }

            ChatItemsControl.Items.Add(bubble);
        }
        private BitmapImage? LoadImage(byte[]? imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
        private void LoadContacts()
        {
            int selectedId = (_activeChatId != 0) ? _activeChatId : -1;
            LstContacts.Items.Clear();
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    string sql = @"SELECT u.id, u.username, u.is_online,
                                  (SELECT COUNT(*) FROM messages m WHERE m.sender_id = u.id AND m.receiver_id = @myId AND m.is_read = 0) as new_count
                                  FROM friends f JOIN users u ON (f.user_id = u.id OR f.friend_id = u.id)
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
                                if (contact.Id == selectedId) LstContacts.SelectedItem = contact;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void LstContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstContacts.SelectedItem is ContactItem selectedFriend)
            {
                _activeChatId = selectedFriend.Id;
                LblChatPartner.Text = "Rozmowa z: " + selectedFriend.Name;
                try
                {
                    using (var conn = _dbService.GetConnection())
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
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                LoadMessages(_activeChatId);
            }
        }

        private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtMessage.Text.Trim();
            if (_activeChatId == 0 || string.IsNullOrEmpty(text)) return;
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    string sql = "INSERT INTO messages (sender_id, receiver_id, message_type, content, is_read) VALUES (@myId, @friendId, 'text', @txt, 0)";
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
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("UPDATE users SET is_online = 0 WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", UserSession.CurrentUserId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
            _timer.Stop();
            UserSession.CurrentUserId = 0;
            UserSession.CurrentUsername = null;
            new WelcomeWindow().Show();
            this.Close();
        }

        private void BtnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            AddFriendWindow addWin = new AddFriendWindow { Owner = this };
            if (addWin.ShowDialog() == true) LoadContacts();
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
        private void SaveFileToDisk(byte[]? data, string? fileName)
        {
            // Jeśli nie ma danych lub nazwy, przerywamy
            if (data == null || string.IsNullOrEmpty(fileName)) return;

            // Standardowe okno zapisu Windows
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = fileName, // Domyślna nazwa pliku z bazy
                Title = "Wybierz miejsce zapisu pliku"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(saveFileDialog.FileName, data);
                    MessageBox.Show("Plik został zapisany pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}