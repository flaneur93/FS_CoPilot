using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;





namespace fs_copilot
{



    public partial class MainWindow : Window
    {


        public static MainWindow Instance { get; private set; }
        private DataManager _dataManager = new DataManager();
        private FsConnection _fsConnection;
        private VoiceModule _voiceModule;




        public MainWindow()
        {

            InitializeComponent();
            PopulateProfileSelectComboBox();

            MainWindow.Instance = this;
            _fsConnection = new FsConnection(this);
            _dataManager = new DataManager();
            _voiceModule = new VoiceModule();
            _voiceModule.StartRecognition();

            InitializeFsConnection();

        }









        #region Event Listing

        private void PopulateProfileSelectComboBox()
        {
            try
            {
                // Controls klasör yolunu belirle
                string controlsPath = Path.Combine(Directory.GetCurrentDirectory(), "Controls");

                if (Directory.Exists(controlsPath))
                {
                    // .json uzantılı dosyaları al
                    var jsonFiles = Directory.GetFiles(controlsPath, "*.json");

                    ProfileSelect_CB.Items.Clear(); // Mevcut içerik temizlenir

                    foreach (var file in jsonFiles)
                    {
                        // Dosya isimlerini ComboBox'a ekle
                        ProfileSelect_CB.Items.Add(Path.GetFileName(file));
                    }

                    WriteToMainConsole("[INFO] ProfileSelect_CB başarıyla dolduruldu.");
                }
                else
                {
                    WriteToMainConsole("[ERROR] 'Controls' klasörü bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                WriteToMainConsole($"[ERROR] ProfileSelect_CB doldurulurken hata oluştu: {ex.Message}");
            }
        }

        private void PopulateCategoryComboBox()
        {
            Category_CB.Items.Clear();
            foreach (var category in _dataManager.Categories)
            {
                Category_CB.Items.Add(category);
            }
        }

        private void PopulateSubcategoryComboBox(string selectedCategory)
        {
            Subcategory_CB.Items.Clear();
            if (_dataManager.Subcategories.ContainsKey(selectedCategory))
            {
                foreach (var subcategory in _dataManager.Subcategories[selectedCategory])
                {
                    Subcategory_CB.Items.Add(subcategory);
                }
            }
        }

        private void PopulateEventListView(string selectedSubcategory)
        {
            EventListView.Items.Clear();
            if (_dataManager.Events.ContainsKey(selectedSubcategory))
            {
                foreach (var ev in _dataManager.Events[selectedSubcategory])
                {
                    EventListView.Items.Add(ev); // Doğrudan Event nesnesi ekleniyor
                }
            }
        }



        private void ProfileSelect_CB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileSelect_CB.SelectedItem == null)
                return;

            string selectedFile = ProfileSelect_CB.SelectedItem.ToString();
            string controlsPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Controls");
            string selectedFilePath = System.IO.Path.Combine(controlsPath, selectedFile);

            try
            {
                _dataManager.LoadJsonProfile(selectedFilePath);
                PopulateCategoryComboBox(); // Kategorileri doldur
                EventListView.Items.Clear();
                Subcategory_CB.Items.Clear();

                // Yeni Grammar.json oluştur
                string grammarPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Grammar.json");
                _dataManager.CreateGrammarJson(grammarPath);
                WriteToMainConsole($"[INFO] Yeni Grammar.json başarıyla oluşturuldu: {grammarPath}");

                // VoiceModule yeniden başlat
                _voiceModule.RestartRecognition();
                WriteToMainConsole("[INFO] VoiceModule yeniden başlatıldı.");
            }
            catch (Exception ex)
            {
                WriteToMainConsole($"[ERROR] Profil değiştirilirken hata oluştu: {ex.Message}");
            }
        }

        private void Category_CB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Category_CB.SelectedItem == null)
                return;

            string selectedCategory = Category_CB.SelectedItem.ToString();
            PopulateSubcategoryComboBox(selectedCategory);
            EventListView.Items.Clear();
        }

        private void Subcategory_CB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Subcategory_CB.SelectedItem == null)
                return;

            string selectedSubcategory = Subcategory_CB.SelectedItem.ToString();
            PopulateEventListView(selectedSubcategory);
        }

        private void SpeechText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;

                var eventItem = textBox.DataContext as Event;
                if (eventItem == null) return;

                try
                {
                    _dataManager.SaveUpdatedEventToJson(eventItem); // JSON'a kaydet
                    WriteToMainConsole($"[INFO] SpeechText güncellendi ve kaydedildi: {eventItem.SpeechText}");

                    // Düzenleme modunu sonlandır
                    textBox.IsReadOnly = true;  // Sadece okuma moduna geç
                    Keyboard.ClearFocus(); // TextBox'un odaktan çıkmasını sağla
                }
                catch (Exception ex)
                {
                    WriteToMainConsole($"[ERROR] SpeechText güncellenirken hata oluştu: {ex.Message}");
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Düzenleme iptal edilirse değişiklikler geri alınabilir
                Keyboard.ClearFocus();
            }
        }


        #endregion

























        #region Console Functions

        public void WriteToMainConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MainConsole.Text += $"{message}\n";
            });
        }
        public void WriteToInfoConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                InfoConsole.Text += $"{message}\n";
            });
        }

        #endregion

        #region Simconnect   

        private void InitializeFsConnection()
        {
            if (_fsConnection == null)
            {
                WriteToMainConsole("[ERROR] FsConnection başlatılamadı!");
                return;
            }

            _fsConnection.ConnectToFSAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            _fsConnection.DisconnectFromFS();
            base.OnClosed(e);
        }

        #endregion





    }
}