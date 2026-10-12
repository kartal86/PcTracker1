using System;
using System.IO;

namespace PcTracker
{
    public static class Logger
    {
        private static string logDosyasi;

        public static void Baslat()
        {
            // Log dosyası exe'nin yanındaki Logs klasöründe tutulacak.....
            string logKlasoru = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logKlasoru))
            {
                Directory.CreateDirectory(logKlasoru);
            }

            logDosyasi = Path.Combine(logKlasoru, $"SistemLog_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        public static void Yaz(string mesaj)
        {
            try
            {
                string logSatiri = $"[{DateTime.Now:HH:mm:ss}] - {mesaj}\n";
                File.AppendAllText(logDosyasi, logSatiri);
            }
            catch { /* Log yazarken hata olursa sistemi çökertme */ }
        }

        public static void HataYaz(Exception ex)
        {
            Yaz($"KRİTİK HATA: {ex.Message}\nStack Trace: {ex.StackTrace}");
        }
    }
}