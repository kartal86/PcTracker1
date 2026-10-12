using System.Threading.Tasks;
using System.Windows;

namespace PcTracker
{
    public partial class BossModeWindow : Window
    {
        public BossModeWindow(string bossName)
        {
            InitializeComponent();
            TxtWelcome.Text = $"HOŞ GELDİNİZ, {bossName.ToUpper()}";

            // Ekran açılır açılmaz zamanlayıcıyı başlat
            StartSequence();
        }

        private async void StartSequence()
        {
            // 1. Yazıyı ekranda 2 saniye (2000 milisaniye) göster
            await Task.Delay(1000);

            // 2. Süre dolunca yazıyı gizle
            TxtWelcome.Visibility = Visibility.Hidden;

            // 3. Videoyu görünür yap ve oynatmayı başlat
            BgVideo.Visibility = Visibility.Visible;
            BgVideo.Play();
        }

        // Video tamamen bittiği an ekranı kapat (Masaüstüne dön)
        private void BgVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}