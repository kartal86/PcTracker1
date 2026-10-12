using System.Windows;
using Application = System.Windows.Application;

namespace PcTracker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Tüm yakalanamayan hataları logla
            Current.DispatcherUnhandledException += (sender, args) =>
            {
                Logger.HataYaz(args.Exception);
                System.Windows.MessageBox.Show("Beklenmeyen bir hata oluştu. Detaylar Logs klasörüne kaydedildi.", "Sistem Hatası");
                args.Handled = true;
            };
        }
    }
}