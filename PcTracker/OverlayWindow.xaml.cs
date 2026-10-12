using System;
using System.Windows;
using System.Windows.Threading;

namespace PcTracker
{
    public partial class OverlayWindow : Window
    {
        private DispatcherTimer timer;

        public OverlayWindow(string message)
        {
            InitializeComponent();
            TxtMessage.Text = message;

            // Ekran yüklendiğinde otomatik sağ alta hizala
            this.Loaded += (s, e) =>
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = desktopWorkingArea.Right - this.Width ;
                this.Top = desktopWorkingArea.Bottom - this.Height - 30;
            };

            // 10 Saniye sonra kendini kapat
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(10);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }
    }
}