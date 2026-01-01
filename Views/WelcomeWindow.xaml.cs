using System.Runtime.Versioning;
using System.Windows;

namespace CzatuCzatu.Views
{
    public partial class WelcomeWindow : Window
    {
        [SupportedOSPlatform("windows")]
        public WelcomeWindow()

        {
            InitializeComponent();
        }

        private void BtnGoToLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWin = new LoginWindow();
            loginWin.Show();
            this.Close();
        }


        private void BtnGoToRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow regWin = new RegisterWindow();
            regWin.Show();
            this.Close();
        }
    }
}