using System.Windows;
using System.Windows.Media;

namespace PcTracker
{
    public partial class StatusWindow : Window
    {
        public StatusWindow(string message, bool isBoss = false)
        {
            InitializeComponent();
            TxtMessage.Text = message;

            // Eğer Boss moduysa başlığı yeşil yap ve adını değiştir
            if (isBoss)
            {
                TxtTitle.Text = "YÖNETİCİ MODU";
                TxtTitle.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}