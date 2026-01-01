using System.Windows;
using CzatuCzatu.Models;
using CzatuCzatu.Services;
using MySqlConnector;

namespace CzatuCzatu.Views;

using MessageBox = System.Windows.MessageBox;
public partial class AddFriendWindow : Window
{

    private DatabaseService _dbService = new DatabaseService();

    public AddFriendWindow()
    {
        InitializeComponent();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        string input = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        try
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();

                string findSql = "SELECT id FROM users WHERE (id = @input OR username = @input) AND id != @myId";
                int targetUserId = 0;

                using (var cmd = new MySqlCommand(findSql, conn))
                {
                    cmd.Parameters.AddWithValue("@input", input);
                    cmd.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                    var result = cmd.ExecuteScalar();

                    if (result == null)
                    {
                        MessageBox.Show("Nie znaleziono takiego użytkownika (lub to Ty).", "Informacja");
                        return;
                    }
                    targetUserId = Convert.ToInt32(result);
                }

                string checkSql = "SELECT COUNT(*) FROM friends WHERE (user_id = @myId AND friend_id = @targetId) OR (user_id = @targetId AND friend_id = @myId)";
                using (var cmdCheck = new MySqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                    cmdCheck.Parameters.AddWithValue("@targetId", targetUserId);
                    if (Convert.ToInt64(cmdCheck.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("Ten użytkownik jest już na Twojej liście!", "Informacja");
                        return;
                    }
                }

                string insertSql = "INSERT INTO friends (user_id, friend_id, status) VALUES (@myId, @targetId, 'accepted')";
                using (var cmdInsert = new MySqlCommand(insertSql, conn))
                {
                    cmdInsert.Parameters.AddWithValue("@myId", UserSession.CurrentUserId);
                    cmdInsert.Parameters.AddWithValue("@targetId", targetUserId);
                    cmdInsert.ExecuteNonQuery();
                }

                MessageBox.Show("Dodano do znajomych!", "Sukces");
                this.DialogResult = true; 
                this.Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd: " + ex.Message);
        }
    }
}