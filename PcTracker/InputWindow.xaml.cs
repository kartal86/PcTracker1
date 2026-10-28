using System;
using System.Collections.Generic;
using System.Linq;
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

namespace PcTracker
{
    public partial class InputWindow : Window
    {
        // Girilen metni MainWindow'un alabilmesi için özellik (Property) oluşturuyoruz
        public string Answer { get; private set; }

        public InputWindow(string promptMessage)
        {
            InitializeComponent();
            TxtPrompt.Text = promptMessage;
            TxtInput.Focus(); // Ekran açılır açılmaz klavye imlecini mesaj kutusuna getirir
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            Answer = TxtInput.Text;
            this.DialogResult = true; // Pencereyi onay vererek kapatır
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Pencereyi iptal ederek kapatır
            this.Close();
        }
    }
}
