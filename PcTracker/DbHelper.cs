using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace PcTracker
{
    public static class DbHelper
    {
        // Veritabanı dosyası uygulamanın çalıştığı klasörde oluşacak
        private static readonly string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PcTracker.db");
        private static readonly string connectionString = $"Data Source={dbPath}";

        public static void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Oturumlar (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        KullaniciAdi TEXT NOT NULL,
                        GirisZamani DATETIME NOT NULL,
                        CikisZamani DATETIME,
                        IzinVerilenDakika INTEGER NOT NULL,
                        HarcananDakika INTEGER DEFAULT 0
                    );";

                using (var command = new SqliteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        // Yeni oturum başladığında kaydeder ve bize oluşan ID'yi döndürür
        public static long YeniOturumBaslat(string kullaniciAdi, int izinVerilenDakika)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string insertQuery = @"
                    INSERT INTO Oturumlar (KullaniciAdi, GirisZamani, IzinVerilenDakika) 
                    VALUES (@KullaniciAdi, @GirisZamani, @IzinVerilenDakika);
                    SELECT last_insert_rowid();";

                using (var command = new SqliteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@KullaniciAdi", kullaniciAdi);
                    command.Parameters.AddWithValue("@GirisZamani", DateTime.Now);
                    command.Parameters.AddWithValue("@IzinVerilenDakika", izinVerilenDakika);

                    return (long)command.ExecuteScalar();
                }
            }
        }

        // Oturum kapandığında veya süre bittiğinde çıkış zamanını ve harcanan süreyi günceller
        public static void OturumKapat(long oturumId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Önce giriş zamanını çekip harcanan dakikayı hesaplıyoruz
                string selectQuery = "SELECT GirisZamani FROM Oturumlar WHERE Id = @Id";
                DateTime girisZamani;

                using (var selectCmd = new SqliteCommand(selectQuery, connection))
                {
                    selectCmd.Parameters.AddWithValue("@Id", oturumId);
                    girisZamani = Convert.ToDateTime(selectCmd.ExecuteScalar());
                }

                int harcananDakika = (int)(DateTime.Now - girisZamani).TotalMinutes;

                // Şimdi kaydı güncelliyoruz
                string updateQuery = @"
                    UPDATE Oturumlar 
                    SET CikisZamani = @CikisZamani, HarcananDakika = @HarcananDakika 
                    WHERE Id = @Id";

                using (var updateCmd = new SqliteCommand(updateQuery, connection))
                {
                    updateCmd.Parameters.AddWithValue("@CikisZamani", DateTime.Now);
                    updateCmd.Parameters.AddWithValue("@HarcananDakika", harcananDakika);
                    updateCmd.Parameters.AddWithValue("@Id", oturumId);
                    updateCmd.ExecuteNonQuery();
                }
            }
        }
    }
}