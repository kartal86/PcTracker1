using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading; // Sayaç için eklendi
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PcTracker
{
    public partial class MainWindow : Window
    {
        private string selectedUser = "";
        private string userType = "";

        private readonly string botToken = "8971048145:AAGOf4JZI-o0dvYw74eG_5b7iMPInRX9s3c";
        private readonly string chatId = "5715963346";
        private TelegramBotClient botClient;
        private CancellationTokenSource cts = new CancellationTokenSource();

        private bool isTestMode = true;
        private System.Windows.Forms.NotifyIcon trayIcon;

        // VERİ TABANI VE SAYAÇ DEĞİŞKENLERİ
        private DateTime girisZamani;
        private long aktifOturumId = 0;
        private DispatcherTimer oyunSayaci;
        private int kalanDakika = 0;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();

            // 1. Veritabanını hazırla (Tablo yoksa oluşturur)
            DbHelper.InitializeDatabase();

            botClient = new TelegramBotClient(botToken);
            StartTelegramListener();

            // Uygulama kapanırken veya bilgisayar kapatılırken süreyi hesapla
            System.Windows.Application.Current.Exit += App_Exit;
            Microsoft.Win32.SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }

        private void SetupTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Icon = System.Drawing.SystemIcons.Shield;
            trayIcon.Text = "PC Tracker - Arka Planda Çalışıyor";
            trayIcon.Visible = false;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Kalan Süremi Göster", null, (s, e) => {
                // Tray icon arka plan thread'inde çalıştığı için WPF arayüzünü Dispatcher ile çağırıyoruz
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (userType == "User")
                    {
                        StatusWindow status = new StatusWindow($"Kullanıcı: {selectedUser} \n\nKalan Süreniz: {kalanDakika} Dakika");
                        status.ShowDialog();
                    }
                    else
                    {
                        StatusWindow status = new StatusWindow("Boss Mode Aktif.\nSüre kısıtlaması yok.", true);
                        status.ShowDialog();
                    }
                });
            });
            trayIcon.ContextMenuStrip = menu;
        }

        private void UserButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button clickedButton = sender as System.Windows.Controls.Button;
            selectedUser = clickedButton.Content.ToString().Replace("👑 ", "").Replace("🎮 ", "");
            userType = clickedButton.Tag.ToString();

            BtnHalil.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B2D42"));
            BtnIbrahim.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B2D42"));
            BtnAhmet.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B2D42"));
            BtnIsmail.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B2D42"));
            clickedButton.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8D99AE"));

            if (userType == "Boss")
            {
                PanelTime.Visibility = Visibility.Hidden;
                BtnStart.Content = "YÖNETİCİ GİRİŞİ YAP";
                BtnStart.Background = new SolidColorBrush(System.Windows.Media.Colors.Green);
                BtnStart.Visibility = Visibility.Visible;
            }
            else
            {
                PanelTime.Visibility = Visibility.Visible;
                BtnStart.Content = "OYUNU BAŞLAT";
                BtnStart.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D90429"));
                BtnStart.Visibility = Visibility.Visible;
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (userType == "Boss")
            {
                BossModeWindow bossWindow = new BossModeWindow(selectedUser);
                bossWindow.Show();
                this.Close();
            }
            else
            {
                double selectedHours = double.Parse(((ComboBoxItem)ComboTime.SelectedItem).Content.ToString());
                int izinDakika = (int)(selectedHours * 60);
                kalanDakika = izinDakika;

                // 2. DB'ye yeni oturumu kaydet ve ID'sini al
                aktifOturumId = DbHelper.YeniOturumBaslat(selectedUser, izinDakika);
                girisZamani = DateTime.Now;

                await botClient.SendMessage(chatId, $"🎮 {selectedUser} pc başında {selectedHours} saat ({izinDakika} dk) süre ile açtı.");

                this.Hide();
                trayIcon.Visible = true;

                // 3. Arka plan geri sayım sayacını başlat
                BaslatSayac();

                if (isTestMode)
                {
                    GosterOyunIciUyari($"TEST: {selectedUser}, {selectedHours} saatlik süren başladı!");
                }
            }
        }

        // --- SAYAC VE UYARI SİSTEMİ ---
        private void BaslatSayac()
        {
            oyunSayaci = new DispatcherTimer();
            oyunSayaci.Interval = TimeSpan.FromMinutes(1); // Her 1 dakikada bir çalışır
            oyunSayaci.Tick += (s, ev) =>
            {
                kalanDakika--;

                int yariSure = (int)(DateTime.Now - girisZamani).TotalMinutes; // İznin yarısı falan hesaplanabilir

                if (kalanDakika == 30)
                {
                    GosterOyunIciUyari("Yarım saatiniz kaldı.");
                }
                else if (kalanDakika == 5)
                {
                    GosterOyunIciUyari("🚨 DİKKAT: Son 5 dakikan! Lütfen oyununu kaydet veya maçı bitir.");
                    botClient.SendMessage(chatId, $"⚠️ {selectedUser} için oyun süresinin bitimine SON 5 DAKİKA kaldı!");
                }
                else if (kalanDakika <= 0)
                {
                    oyunSayaci.Stop();
                    GosterOyunIciUyari("SÜREN BİTTİ! Sistem kilitleniyor...");
                    botClient.SendMessage(chatId, $"🔒 {selectedUser} süresini doldurdu. Sistem kilitlendi.");

                    // Windows'u Kilitle
                    System.Diagnostics.Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                }
            };
            oyunSayaci.Start();
        }

        private void GosterOyunIciUyari(string mesaj)
        {
            OverlayWindow overlay = new OverlayWindow($"💬 SİSTEM UYARISI:\n{mesaj}");
            overlay.Show();
        }

        // --- ÇIKIŞ VE VERİ TABANI KAYIT İŞLEMLERİ ---
        private void App_Exit(object sender, ExitEventArgs e)
        {
            SendLogoutMessage();
        }

        private void SystemEvents_SessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            SendLogoutMessage();
        }

        private void SendLogoutMessage()
        {
            if (userType == "User" && !string.IsNullOrEmpty(selectedUser) && aktifOturumId > 0)
            {
                // DB'de oturumu kapat ve süreyi güncelle
                DbHelper.OturumKapat(aktifOturumId);

                TimeSpan oynananSure = DateTime.Now - girisZamani;
                string sureMetni = $"{(int)oynananSure.TotalHours} saat, {oynananSure.Minutes} dakika";

                try
                {
                    botClient.SendMessage(chatId, $"🛑 {selectedUser} bilgisayarı kapattı.\n⏱ Toplam oynama süresi: {sureMetni}").Wait();
                }
                catch { }

                aktifOturumId = 0; // Aynı kaydı iki kez yapmasını engelle
            }
        }

        // --- TELEGRAM DİNLEME (Değişiklik Yok) ---
        private void StartTelegramListener()
        {
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

            // Pozisyonel argüman kullanıldı (isimli parametreleri kaldırdık)
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) return;
            if (message.Text is not { } messageText) return;
            if (message.Chat.Id.ToString() != chatId) return;

            string[] metinParcalari = messageText.Split(' ');
            string komut = metinParcalari[0].ToLower();

            switch (komut)
            {
                case "/durum":
                    if (userType == "Boss")
                        await botClient.SendMessage(chatId, $"👑 Yönetici ({selectedUser}) sistemde.", cancellationToken: cancellationToken);
                    else if (string.IsNullOrEmpty(selectedUser))
                        await botClient.SendMessage(chatId, "⏳ Bilgisayar bekleme ekranında.", cancellationToken: cancellationToken);
                    else
                        await botClient.SendMessage(chatId, $"🎮 {selectedUser} sistemde.\n⏱ Kalan Süre: {kalanDakika} Dakika.", cancellationToken: cancellationToken);
                    break;

                case "/mesaj":
                    string gonderilecekMesaj = messageText.Replace(metinParcalari[0], "").Trim();
                    if (!string.IsNullOrEmpty(gonderilecekMesaj))
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => GosterOyunIciUyari(gonderilecekMesaj));
                        await botClient.SendMessage(chatId, "✅ Mesaj gösterildi.", cancellationToken: cancellationToken);
                    }
                    break;

                case "/kilitle":
                    await botClient.SendMessage(chatId, "🔒 Bilgisayar anında kilitleniyor...", cancellationToken: cancellationToken);
                    System.Diagnostics.Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                    break;

                case "/ekran":
                    await botClient.SendMessage(chatId, "📸 Ekran görüntüsü alınıyor, bekleyin...", cancellationToken: cancellationToken);
                    try
                    {
                        int width = (int)SystemParameters.PrimaryScreenWidth;
                        int height = (int)SystemParameters.PrimaryScreenHeight;
                        using (var bmp = new System.Drawing.Bitmap(width, height))
                        {
                            using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.CopyFromScreen(0, 0, 0, 0, bmp.Size); }
                            using (var ms = new System.IO.MemoryStream())
                            {
                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                await botClient.SendPhoto(chatId: chatId, photo: Telegram.Bot.Types.InputFile.FromStream(ms, "ekran.png"), caption: $"Anlık Ekran", cancellationToken: cancellationToken);
                            }
                        }
                    }
                    catch (Exception ex) { await botClient.SendMessage(chatId, $"❌ Ekran hatası: {ex.Message}", cancellationToken: cancellationToken); }
                    break;
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}