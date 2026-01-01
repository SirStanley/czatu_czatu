using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CzatuCzatu.Views
{
    /// Logika interakcji dla klasy WelcomeWindow.xaml
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


        /// Obsługa kliknięcia przycisku "Zarejestruj się"

        private void BtnGoToRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow regWin = new RegisterWindow();
            regWin.Show();
            this.Close();
        }
    }
}