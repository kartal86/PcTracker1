using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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

        private readonly string chatId = "5715963346";
        private TelegramBotClient botClient;
        private CancellationTokenSource cts = new CancellationTokenSource();

        private System.Windows.Forms.NotifyIcon trayIcon;

        // VERİ TABANI VE SAYAÇ DEĞİŞKENLERİ
        private DateTime girisZamani;
        private DateTime bitisZamani;
        private long aktifOturumId = 0;
        private DispatcherTimer oyunSayaci;
        private string botToken = "";

        // SPAM ENGELLEYİCİ DURUM BAYRAKLARI
        private bool bildirim30Dk = false;
        private bool bildirim5Dk = false;
        private bool kilitlendi = false;

        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Application.Current.Dispatcher.Invoke(() => this.Visibility = Visibility.Visible);
            ResetUI();

            Logger.Baslat();
            Logger.Yaz("Uygulama başlatıldı.");

            string baseDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
            string configPath = System.IO.Path.Combine(baseDir, "config.txt");

            if (System.IO.File.Exists(configPath))
            {
                botToken = System.IO.File.ReadAllText(configPath).Trim();
            }
            else
            {
                System.Windows.MessageBox.Show("HATA: config.txt dosyası bulunamadı!");
                Logger.Yaz("HATA: config.txt bulunamadı.");
                System.Windows.Application.Current.Shutdown();
                return;
            }

            SetupTrayIcon();
            DbHelper.InitializeDatabase();

            botClient = new TelegramBotClient(botToken);
            StartTelegramListener();

            System.Windows.Application.Current.Exit += App_Exit;
            Microsoft.Win32.SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }

        private void ResetUI()
        {
            selectedUser = "";
            userType = "";
            BtnStart.Visibility = Visibility.Hidden;
            PanelTime.Visibility = Visibility.Hidden;

            var converter = new BrushConverter();

            BtnHalil.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnIbrahim.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnAhmet.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnIsmail.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
        }

        private void SetupTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Icon = System.Drawing.SystemIcons.Shield;
            trayIcon.Text = "PC Tracker";
            trayIcon.Visible = false;

            var menu = new System.Windows.Forms.ContextMenuStrip();

            // 1. MEVCUT MENÜ: KALAN SÜREYİ GÖSTER
            menu.Items.Add("Kalan Süremi Göster", null, (s, e) => {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (userType == "User")
                    {
                        int kalanDk = (int)(bitisZamani - DateTime.Now).TotalMinutes;
                        if (kalanDk < 0) kalanDk = 0;
                        StatusWindow status = new StatusWindow($"Kullanıcı: {selectedUser} \n\nKalan Süreniz: {kalanDk} Dakika");
                        status.ShowDialog();
                    }
                    else
                    {
                        StatusWindow status = new StatusWindow("Boss Mode Aktif.\nSüre kısıtlaması yok.", true);
                        status.ShowDialog();
                    }
                });
            });

            // 2. YENİ EKLENTİ: YÖNETİCİYE MESAJ GÖNDER
            menu.Items.Add("Yöneticiye Mesaj Gönder", null, (s, e) => {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Kullanıcıdan mesaj almak için küçük bir input penceresi (InputWindow) açıyoruz
                    InputWindow inputWindow = new InputWindow("Yöneticiye iletilecek mesajı girin:");
                    if (inputWindow.ShowDialog() == true)
                    {
                        string gonderilecekMesaj = inputWindow.Answer;
                        if (!string.IsNullOrWhiteSpace(gonderilecekMesaj))
                        {
                            try
                            {
                                botClient.SendMessage(chatId, $"📨 **{selectedUser}'den Mesaj:**\n\n{gonderilecekMesaj}", parseMode: ParseMode.Markdown).Wait();
                                System.Windows.MessageBox.Show("Mesajınız yöneticiye başarıyla iletildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            catch (Exception ex)
                            {
                                System.Windows.MessageBox.Show("Mesaj gönderilirken bir hata oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                                Logger.HataYaz(ex);
                            }
                        }
                    }
                });
            });

            // 3. YENİ EKLENTİ: KULLANICI DEĞİŞTİR (OTURUM DEVRİ)
            menu.Items.Add("Kullanıcı Değiştir", null, (s, e) => {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrEmpty(selectedUser)) return;

                    MessageBoxResult result = System.Windows.MessageBox.Show($"Şu an {selectedUser} oturumu aktif.\nOturumu kapatıp yeni bir kullanıcı seçmek istiyor musunuz?", "Kullanıcı Değişikliği", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 1. Telegram'a çıkış bilgisini ve süreyi gönder
                        SendLogoutMessage();

                        // 2. Arayüzü sıfırla ve Ana Ekranı geri getir
                        ResetUI();
                        trayIcon.Visible = false;
                        this.Show();
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

            var converter = new BrushConverter();

            BtnHalil.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnIbrahim.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnAhmet.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");
            BtnIsmail.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#2B2D42");

            clickedButton.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#8D99AE");

            if (userType == "Boss")
            {
                PanelTime.Visibility = Visibility.Hidden;
                BtnStart.Content = "YÖNETİCİ GİRİŞİ YAP";
                BtnStart.Background = new SolidColorBrush(Colors.Green);
                BtnStart.Visibility = Visibility.Visible;
            }
            else
            {
                PanelTime.Visibility = Visibility.Visible;
                BtnStart.Content = "OYUNU BAŞLAT";
                BtnStart.Background = (System.Windows.Media.Brush)converter.ConvertFromString("#D90429");
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
                string selectedContent = ((ComboBoxItem)ComboTime.SelectedItem).Content.ToString().Replace(",", ".");
                double selectedHours = double.Parse(selectedContent, CultureInfo.InvariantCulture);

                int izinDakika = (int)(selectedHours * 60);

                girisZamani = DateTime.Now;
                bitisZamani = girisZamani.AddMinutes(izinDakika);

                bildirim30Dk = false;
                bildirim5Dk = false;
                kilitlendi = false;

                aktifOturumId = DbHelper.YeniOturumBaslat(selectedUser, izinDakika);

                try
                {
                    await botClient.SendMessage(chatId, $"🎮 {selectedUser} pc başında {selectedHours} saat ({izinDakika} dk) süre ile açtı.");
                }
                catch { }

                this.Hide();
                trayIcon.Visible = true;

                BaslatSayac();
            }
        }

        private void BaslatSayac()
        {
            if (oyunSayaci != null) oyunSayaci.Stop();

            oyunSayaci = new DispatcherTimer();
            oyunSayaci.Interval = TimeSpan.FromSeconds(10);
            oyunSayaci.Tick += (s, ev) =>
            {
                TimeSpan kalanSure = bitisZamani - DateTime.Now;
                double kalanDakika = kalanSure.TotalMinutes;

                if (kalanDakika <= 30 && kalanDakika > 29 && !bildirim30Dk)
                {
                    bildirim30Dk = true;
                    GosterOyunIciUyari("Yarım saatiniz kaldı.");
                }
                else if (kalanDakika <= 5 && kalanDakika > 0 && !bildirim5Dk)
                {
                    bildirim5Dk = true;
                    GosterOyunIciUyari("🚨 DİKKAT: Son 5 dakikan! Lütfen oyununu kaydet veya maçı bitir.");
                    botClient.SendMessage(chatId, $"⚠️ {selectedUser} için oyun süresinin bitimine SON 5 DAKİKA kaldı!");
                }
                else if (kalanDakika <= 0 && !kilitlendi)
                {
                    kilitlendi = true;
                    oyunSayaci.Stop();

                    GosterOyunIciUyari("SÜREN BİTTİ! Sistem kilitleniyor...");
                    botClient.SendMessage(chatId, $"🔒 {selectedUser} süresini doldurdu. Sistem kilitlendi.");

                    System.Diagnostics.Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                }
            };
            oyunSayaci.Start();
        }

        private void GosterOyunIciUyari(string mesaj)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is OverlayWindow) window.Close();
            }

            OverlayWindow overlay = new OverlayWindow($"💬 SİSTEM UYARISI:\n{mesaj}");
            overlay.Show();
        }

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
                oyunSayaci?.Stop();
                DbHelper.OturumKapat(aktifOturumId);

                TimeSpan oynananSure = DateTime.Now - girisZamani;
                string sureMetni = $"{(int)oynananSure.TotalHours} saat, {oynananSure.Minutes} dakika";

                try
                {
                    botClient.SendMessage(chatId, $"🛑 {selectedUser} bilgisayarı kapattı / oturumu devretti.\n⏱ Toplam oynama süresi: {sureMetni}").Wait();
                }
                catch { }

                aktifOturumId = 0;
                selectedUser = "";
            }
        }

        private void StartTelegramListener()
        {
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
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
                    else if (string.IsNullOrEmpty(selectedUser) || aktifOturumId == 0)
                        await botClient.SendMessage(chatId, "⏳ Bilgisayar bekleme ekranında.", cancellationToken: cancellationToken);
                    else
                    {
                        int kalan = (int)(bitisZamani - DateTime.Now).TotalMinutes;
                        if (kalan < 0) kalan = 0;
                        await botClient.SendMessage(chatId, $"🎮 {selectedUser} sistemde.\n⏱ Kalan Süre: {kalan} Dakika.", cancellationToken: cancellationToken);
                    }
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

                case "/cikis":
                    if (!string.IsNullOrEmpty(selectedUser) && userType == "User")
                    {
                        await botClient.SendMessage(chatId, $"🔄 {selectedUser} için oturum sonlandırılıyor. Bilgisayar kilitlenecek ve ekran sıfırlanacak.", cancellationToken: cancellationToken);

                        SendLogoutMessage();

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ResetUI();
                            trayIcon.Visible = false;
                            this.Show();
                            System.Diagnostics.Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                        });
                    }
                    else
                    {
                        await botClient.SendMessage(chatId, "❌ Şu anda kapatılabilecek aktif bir kullanıcı oturumu yok.", cancellationToken: cancellationToken);
                    }
                    break;

                case "/sureayar":
                    if (metinParcalari.Length > 1 && int.TryParse(metinParcalari[1], out int yeniDakika))
                    {
                        if (!string.IsNullOrEmpty(selectedUser) && userType == "User")
                        {
                            bitisZamani = DateTime.Now.AddMinutes(yeniDakika);
                            bildirim30Dk = false;
                            bildirim5Dk = false;
                            kilitlendi = false;
                            await botClient.SendMessage(chatId, $"✅ {selectedUser} için kalan süre {yeniDakika} dakika olarak baştan ayarlandı.", cancellationToken: cancellationToken);
                            System.Windows.Application.Current.Dispatcher.Invoke(() => GosterOyunIciUyari($"Süreniz {yeniDakika} dakika olarak güncellendi."));
                        }
                        else { await botClient.SendMessage(chatId, "❌ Şu an aktif bir oyuncu yok.", cancellationToken: cancellationToken); }
                    }
                    else { await botClient.SendMessage(chatId, "Kullanım: /sureayar [dakika]", cancellationToken: cancellationToken); }
                    break;

                case "/sureekle":
                    if (metinParcalari.Length > 1 && int.TryParse(metinParcalari[1], out int ekleDakika))
                    {
                        if (!string.IsNullOrEmpty(selectedUser) && userType == "User")
                        {
                            bitisZamani = bitisZamani.AddMinutes(ekleDakika);
                            kilitlendi = false;

                            int totalKalan = (int)(bitisZamani - DateTime.Now).TotalMinutes;
                            if (totalKalan > 30) bildirim30Dk = false;
                            if (totalKalan > 5) bildirim5Dk = false;

                            await botClient.SendMessage(chatId, $"➕ {selectedUser} için süreye {ekleDakika} dk eklendi.\n⏱ Yeni kalan süre: {totalKalan} Dakika.", cancellationToken: cancellationToken);
                            System.Windows.Application.Current.Dispatcher.Invoke(() => GosterOyunIciUyari($"Sürenize {ekleDakika} dakika EKLENDİ."));
                        }
                        else { await botClient.SendMessage(chatId, "❌ Şu an aktif bir oyuncu yok.", cancellationToken: cancellationToken); }
                    }
                    else { await botClient.SendMessage(chatId, "Kullanım: /sureekle [dakika]", cancellationToken: cancellationToken); }
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