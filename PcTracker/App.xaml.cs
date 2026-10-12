using System.Windows;
using Application = System.Windows.Application;

using System;


namespace PcTracker
{
    public partial class App : Application
    {
        public App()
        {
            // 1. En Üst Düzey Hata Yakalayıcı (Uygulama daha tam açılmadan patlarsa burası yakalar)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                System.Windows.MessageBox.Show($"ÖLÜMCÜL BAŞLANGIÇ HATASI:\n\n{ex.Message}\n\n{ex.StackTrace}", "Sistem Çöktü");
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 2. Arayüz (UI) Hata Yakalayıcısı
            Current.DispatcherUnhandledException += (sender, args) =>
            {
                System.Windows.MessageBox.Show($"ARAYÜZ HATASI:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "Sistem Hatası");
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}