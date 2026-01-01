using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using CzatuCzatu.Models;
using CzatuCzatu.Services;
using MySqlConnector;
using System.Media;
using System.Runtime.Versioning;

namespace CzatuCzatu.Views
{
    // ALIASY WEWNĄTRZ NAMESPACE - Rozwiązują błędy niejednoznaczności
    using Forms = System.Windows.Forms;
    using Application = System.Windows.Application;
    using MouseEventArgs = System.Windows.Input.MouseEventArgs;
    using MessageBox = System.Windows.MessageBox;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using Cursors = System.Windows.Input.Cursors;
    using Color = System.Windows.Media.Color;
    using Brushes = System.Windows.Media.Brushes;
    using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
    using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
    using ColorConverter = System.Windows.Media.ColorConverter;
    using Image = System.Windows.Controls.Image;
    using Orientation = System.Windows.Controls.Orientation;
    using Brush = System.Windows.Media.Brush;

    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        private Forms.NotifyIcon? _notifyIcon;
        private int _lastMessageCount = 0;
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

            // Test połączenia
            if (_dbService.TestConnection())
                this.Title = "Czatu-Czatu - Połączono";
            else
                this.Title = "Czatu-Czatu - Brak połączenia z bazą";

            LoadContacts();

            // Konfiguracja Timera
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Inicjalizacja ikony w pasku zadań
            _notifyIcon = new Forms.NotifyIcon();
            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                _notifyIcon.Visible = true;
                _notifyIcon.Text = "Czatu-Czatu";

                _notifyIcon.MouseDoubleClick += (s, e) => {
                    if (e.Button == Forms.MouseButtons.Left)
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                    }
                };

                // Przywracanie po kliknięciu w dymek
                _notifyIcon.BalloonTipClicked += (s, e) => {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                };
            }
            catch { }
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

        // --- POWIADOMIENIA ---
        private void PlayNotificationSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ringtone.wav");
                if (File.Exists(soundPath))
                {
                    using (var player = new SoundPlayer(soundPath)) { player.Play(); }
                }
                else
                {
                    SystemSounds.Asterisk.Play();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Błąd dźwięku: " + ex.Message); }
        }

        private void ShowNotification(string title, string text)
        {
            string cleanText = text.Length > 50 ? text.Substring(0, 47) + "..." : text;
            _notifyIcon?.ShowBalloonTip(3000, title, cleanText, Forms.ToolTipIcon.Info);
        }

        private void LoadMessages(int friendId)
        {
            // Sprawdzamy, czy użytkownik jest na samym dole przed odświeżeniem
            bool isAtBottom = ChatScrollViewer.VerticalOffset >= ChatScrollViewer.ScrollableHeight;

            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    conn.Open();
                    // JOIN pozwala nam wyciągnąć imię nadawcy bezpośrednio z bazy
                    string sql = @"SELECT m.sender_id, u.username, m.content, m.message_type, m.file_data, m.file_name 
                           FROM messages m
                           JOIN users u ON m.sender_id = u.id
                           WHERE (m.sender_id = @myId AND m.receiver_id = @friendId) 
                              OR (m.sender_id = @friendId AND m.receiver_id = @myId) 
                           ORDER BY m.sent_at ASC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                        cmd.Parameters.AddWithValue("@friendId", friendId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            int currentCount = 0;
                            string lastSenderName = "";
                            string lastMessageSnippet = "";
                            bool hasNewIncomingMessage = false;

                            // CZYŚCIMY LISTĘ PRZED DODANIEM 
                            ChatItemsControl.Items.Clear();

                            while (reader.Read())
                            {
                                currentCount++;
                                int senderId = reader.GetInt32("sender_id");
                                string senderName = reader.GetString("username");
                                string content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString("content");
                                string type = reader.IsDBNull(reader.GetOrdinal("message_type")) ? "text" : reader.GetString("message_type");

                                // TUTAJ BYŁ BŁĄD - brakowało tych definicji:
                                string? fName = reader.IsDBNull(reader.GetOrdinal("file_name")) ? null : reader.GetString("file_name");
                                byte[]? data = reader.IsDBNull(reader.GetOrdinal("file_data")) ? null : (byte[])reader["file_data"];

                                bool isMe = (senderId == UserSession.CurrentUserId);

                                // Dodajemy wiadomość do interfejsu
                                AddMessageToUI(content, isMe, type, data, fName);

                                // Logika wykrywania nowej wiadomości
                                if (currentCount > _lastMessageCount && !isMe)
                                {
                                    hasNewIncomingMessage = true;
                                    lastSenderName = senderName;
                                    lastMessageSnippet = type == "text" ? content : (type == "image" ? "📸 Przesłał(a) zdjęcie" : "📄 Przesłał(a) plik");
                                }
                            }

                            // Wyświetlamy dymek tylko jeśli to faktycznie nowa wiadomość (nie przy starcie)
                            if (hasNewIncomingMessage && _lastMessageCount > 0)
                            {
                                PlayNotificationSound();
                                ShowNotification(lastSenderName, lastMessageSnippet);
                            }

                            _lastMessageCount = currentCount;
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
            var bubbleColor = isMe ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")) : Brushes.LightGray;

            Border bubble = new Border
            {
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(isMe ? 50 : 5, 5, isMe ? 5 : 50, 5),
                MaxWidth = 400,
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = bubbleColor
            };

            if (type == "image" && fileData != null)
            {
                var img = new Image { Source = LoadImage(fileData), MaxWidth = 350, Stretch = Stretch.Uniform, Cursor = Cursors.Hand, ToolTip = "Kliknij, aby zapisać" };
                img.MouseDown += (s, e) => SaveFileToDisk(fileData, fileName);
                bubble.Child = img;
            }
            else if (type == "file" && fileData != null)
            {
                StackPanel fp = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, ToolTip = "Pobierz plik" };
                fp.Children.Add(new TextBlock { Text = "📄 ", FontSize = 16 });
                fp.Children.Add(new TextBlock { Text = text, TextDecorations = TextDecorations.Underline, Foreground = isMe ? Brushes.White : Brushes.Blue });
                fp.MouseDown += (s, e) => SaveFileToDisk(fileData, fileName);
                bubble.Child = fp;
            }
            else
            {
                bubble.Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = isMe ? Brushes.White : Brushes.Black };
            }

            ChatItemsControl.Items.Add(bubble);
        }
        private void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            ShowNotification("Czatu-Czatu", "Aplikacja działa w tle.");
        }
        private void SaveFileToDisk(byte[]? data, string? fileName)
        {
            if (data == null || string.IsNullOrEmpty(fileName)) return;
            SaveFileDialog sfd = new SaveFileDialog { FileName = fileName };
            if (sfd.ShowDialog() == true)
            {
                try { File.WriteAllBytes(sfd.FileName, data); MessageBox.Show("Zapisano!"); }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            }
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

        // --- LOGIKA KONTAKTÓW I SYSTEMU ---
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
                                var contact = new ContactItem { Id = reader.GetInt32("id"), Name = reader.GetString("username"), IsOnline = reader.GetInt32("is_online") == 1, HasNewMessages = reader.GetInt32("new_count") > 0 };
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
                _lastMessageCount = 0;
                LblChatPartner.Text = "Rozmowa z: " + selectedFriend.Name;
                try
                {
                    using (var conn = _dbService.GetConnection())
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand("UPDATE messages SET is_read = 1 WHERE sender_id = @friendId AND receiver_id = @myId", conn))
                        {
                            cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                            cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { }
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
                    using (var cmd = new MySqlCommand("INSERT INTO messages (sender_id, receiver_id, message_type, content, is_read) VALUES (@myId, @friendId, 'text', @txt, 0)", conn))
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
            catch { }
        }

        private void BtnAttachFile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeChatId == 0) return;
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Obrazy (*.jpg;*.png)|*.jpg;*.png|Wszystkie pliki (*.*)|*.*" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    FileInfo fi = new FileInfo(ofd.FileName);
                    if (fi.Length > 10 * 1024 * 1024) return;
                    byte[] bytes = File.ReadAllBytes(ofd.FileName);
                    string ext = fi.Extension.ToLower();
                    string type = (ext == ".jpg" || ext == ".png" || ext == ".jpeg") ? "image" : "file";
                    using (var conn = _dbService.GetConnection())
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand("INSERT INTO messages (sender_id, receiver_id, message_type, content, file_data, file_name, is_read) VALUES (@myId, @friendId, @type, @content, @data, @name, 0)", conn))
                        {
                            cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                            cmd.Parameters.AddWithValue("@friendId", _activeChatId);
                            cmd.Parameters.AddWithValue("@type", type);
                            cmd.Parameters.AddWithValue("@content", fi.Name);
                            cmd.Parameters.AddWithValue("@data", bytes);
                            cmd.Parameters.AddWithValue("@name", fi.Name);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadMessages(_activeChatId);
                }
                catch { }
            }
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
            _notifyIcon?.Dispose();
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
    }
}