using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text.Json;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using NAudio.Wave;
using NAudio.CoreAudioApi;



namespace fs_copilot
{



    public partial class MainWindow : Window
    {
        private VoskSpeechRecognizer? speechRecognizer;
        private JsonDocument? loadedJsonDocument;
        private Dictionary<string, string> pendingChanges = new Dictionary<string, string>();
        private Dictionary<string, string> originalValues = new Dictionary<string, string>();


        public MainWindow()
        {

            InitializeComponent();
            InitializeVosk();
            Console.WriteLine("[DEBUG] Uygulama başlatıldı.");
            StartSimConnect(); // SimConnect'i başlat
            LoadSpeechFilesToComboBox();
            LoadMicrophones();
            LoadAudioOutputDevices();
            LoadModelFolders();



            profileSelect_cBox.SelectionChanged += ProfileSelect_cBox_SelectionChanged;
            category_cBox.SelectionChanged += Category_cBox_SelectionChanged;
            subCategory_cBox.SelectionChanged += Subcategory_cBox_SelectionChanged;


        }

        private void InitializeVosk()
        {
            try
            {
                string modelPath = "./models/vosk-model-en-us-0.22-lgraph";
                speechRecognizer = new VoskSpeechRecognizer(modelPath);

                speechRecognizer.OnRecognized += OnSpeechRecognized;
                
                speechRecognizer.OnDebug += AppendToTestingConsole;

                speechRecognizer.Start();
                AppendToTestingConsole("[INFO] Vosk tanıma başlatıldı.");
            }
            catch (Exception ex)
            {
                AppendToTestingConsole($"[ERROR] Vosk başlatılamadı: {ex.Message}");
            }
        }

        private void OnSpeechRecognized(string text)
        {
            /*AppendToTestingConsole($"[Tam Tanıma]: {text}")*/;
        }

        private void OnPartialSpeechRecognized(string text)
        {
            
        }

        private void AppendToTestingConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TestingConsole.Text += $"{message}\n";
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            speechRecognizer?.Dispose();
            AppendToTestingConsole("[INFO] Pencere kapanıyor ve kaynaklar serbest bırakılıyor.");
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (speechRecognizer != null)
            {
                speechRecognizer.Start();
                AppendToTestingConsole("[ACTION] Vosk tanıma başlatıldı.");
            }
            else
            {
                AppendToTestingConsole("[ERROR] Vosk tanıma başlatılamadı. speechRecognizer başlatılmamış.");
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (speechRecognizer != null)
            {
                speechRecognizer.Stop();
                AppendToTestingConsole("[ACTION] Vosk tanıma durduruldu.");
            }
            else
            {
                AppendToTestingConsole("[ERROR] Vosk tanıma durdurulamadı. speechRecognizer başlatılmamış.");
            }
        }

        private void LoadMicrophones()
        {
            try
            {
                MicSelect_cBox.Items.Clear(); // Mevcut öğeleri temizle

                // Mikrofon cihazlarını algıla ve listele
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var deviceInfo = WaveIn.GetCapabilities(i);
                    MicSelect_cBox.Items.Add($"{i}: {deviceInfo.ProductName}");
                }

                if (MicSelect_cBox.Items.Count > 0)
                {
                    MicSelect_cBox.SelectedIndex = 0; // Varsayılan olarak ilk mikrofon seçilir
                    AppendToTestingConsole("[INFO] Mikrofonlar başarıyla yüklendi.");
                }
                else
                {
                    AppendToTestingConsole("[WARNING] Hiçbir mikrofon algılanamadı.");
                }
            }
            catch (Exception ex)
            {
                AppendToTestingConsole($"[ERROR] Mikrofon yükleme hatası: {ex.Message}");
            }
        }

        private int GetSelectedMicrophoneIndex()
        {
            if (MicSelect_cBox.SelectedItem != null)
            {
                string selectedDevice = MicSelect_cBox.SelectedItem.ToString() ?? string.Empty;
                return int.Parse(selectedDevice.Split(':')[0]); // ID'yi ayır ve döndür
            }

            return -1; // Geçerli bir seçim yoksa -1 döndür
        }

        private void LoadAudioOutputDevices()
        {
            try
            {
                AudioSelect_cBox.Items.Clear(); // Mevcut öğeleri temizle

                // Ses çıkış cihazlarını algıla
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in devices)
                {
                    AudioSelect_cBox.Items.Add(device.FriendlyName);
                }

                if (AudioSelect_cBox.Items.Count > 0)
                {
                    AudioSelect_cBox.SelectedIndex = 0; // Varsayılan olarak ilk cihaz seçilir
                    AppendToTestingConsole("[INFO] Ses çıkış cihazları başarıyla yüklendi.");
                }
                else
                {
                    AppendToTestingConsole("[WARNING] Hiçbir ses çıkış cihazı algılanamadı.");
                }
            }
            catch (Exception ex)
            {
                AppendToTestingConsole($"[ERROR] Ses çıkış cihazlarını yüklerken hata oluştu: {ex.Message}");
            }
        }

        private void LoadModelFolders()
        {
            try
            {
                ModelSelect_cBox.Items.Clear(); // Mevcut öğeleri temizle

                // Çalıştırılabilir dosyanın bulunduğu dizin
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                string modelsPath = Path.Combine(exePath, "models");

                if (!Directory.Exists(modelsPath))
                {
                    AppendToTestingConsole($"[WARNING] 'models' klasörü bulunamadı: {modelsPath}");
                    return;
                }

                // Alt klasörleri al ve ComboBox'a ekle
                string[] subDirectories = Directory.GetDirectories(modelsPath);

                foreach (string directory in subDirectories)
                {
                    string folderName = Path.GetFileName(directory); // Sadece klasör adı
                    ModelSelect_cBox.Items.Add(folderName);
                }

                if (ModelSelect_cBox.Items.Count > 0)
                {
                    ModelSelect_cBox.SelectedIndex = 0; // Varsayılan olarak ilk klasör seçilir
                    AppendToTestingConsole("[INFO] Model klasörleri başarıyla yüklendi.");
                }
                else
                {
                    AppendToTestingConsole("[WARNING] 'models' klasörü boş.");
                }
            }
            catch (Exception ex)
            {
                AppendToTestingConsole($"[ERROR] Model klasörlerini yüklerken hata oluştu: {ex.Message}");
            }
        }









        #region Data_Root
        public class EventItem
        {
            public string EventID { get; set; }
            public string SpeechText { get; set; }
        }
        public class RootObject
        {
            public List<Category> categories { get; set; }
        }

        public class Category
        {
            public string categoryName { get; set; }
            public List<Subcategory> subcategories { get; set; }
        }

        public class Subcategory
        {
            public string subcategoryName { get; set; }
            public List<Event> events { get; set; }
        }

        public class Event
        {
            public string eventID { get; set; }
            public string speechText { get; set; }
        }
        #endregion

        #region Control List

        private void ProfileSelect_cBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Clear subcategory and control event lists
                category_cBox.Items.Clear();
                subCategory_cBox.Items.Clear();
                controlEvent_ListView.ItemsSource = null;

                if (profileSelect_cBox.SelectedItem != null)
                {
                    string selectedProfile = profileSelect_cBox.SelectedItem.ToString();
                    string executionDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string speechFilesDirectory = System.IO.Path.Combine(executionDirectory, "Speech Files");
                    string selectedFilePath = System.IO.Path.Combine(speechFilesDirectory, $"{selectedProfile}.json");

                    if (File.Exists(selectedFilePath))
                    {
                        string jsonContent = File.ReadAllText(selectedFilePath);
                        loadedJsonDocument = JsonDocument.Parse(jsonContent);
                        PopulateCategories();
                    }
                    else
                    {
                        appendToDebugConsole($"Seçilen dosya bulunamadı: {selectedProfile}");
                    }
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Hata: {ex.Message}");
            }
        }

        private void PopulateCategories()
        {
            try
            {
                if (loadedJsonDocument != null)
                {
                    var categories = loadedJsonDocument.RootElement.GetProperty("categories");

                    category_cBox.Items.Clear(); // Clear existing items

                    foreach (var category in categories.EnumerateArray())
                    {
                        if (category.TryGetProperty("categoryName", out var categoryName))
                        {
                            category_cBox.Items.Add(categoryName.GetString()); // Add category names
                        }
                    }

                    
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Kategoriler işlenirken hata oluştu: {ex.Message}");
            }
        }

        private void Category_cBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                subCategory_cBox.Items.Clear();
                controlEvent_ListView.ItemsSource = null;

                if (category_cBox.SelectedItem != null && loadedJsonDocument != null)
                {
                    string selectedCategory = category_cBox.SelectedItem.ToString();

                    // Find selected category and load subcategories
                    var categories = loadedJsonDocument.RootElement.GetProperty("categories");
                    foreach (var category in categories.EnumerateArray())
                    {
                        if (category.TryGetProperty("categoryName", out var categoryName) &&
                            categoryName.GetString() == selectedCategory)
                        {
                            if (category.TryGetProperty("subcategories", out var subcategories))
                            {
                                PopulateSubcategories(subcategories);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Alt kategoriler işlenirken hata oluştu: {ex.Message}");
            }
        }

        private void PopulateSubcategories(JsonElement subcategories)
        {
            try
            {
                subCategory_cBox.Items.Clear(); // Clear existing items

                foreach (var subcategory in subcategories.EnumerateArray())
                {
                    if (subcategory.TryGetProperty("subcategoryName", out var subcategoryName))
                    {
                        subCategory_cBox.Items.Add(subcategoryName.GetString()); // Add subcategory names
                    }
                }

                
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Alt kategoriler yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void Subcategory_cBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                controlEvent_ListView.ItemsSource = null;

                if (subCategory_cBox.SelectedItem != null && loadedJsonDocument != null)
                {
                    string selectedSubcategory = subCategory_cBox.SelectedItem.ToString();

                    // Find selected subcategory and load events
                    var categories = loadedJsonDocument.RootElement.GetProperty("categories");
                    foreach (var category in categories.EnumerateArray())
                    {
                        if (category.TryGetProperty("subcategories", out var subcategories))
                        {
                            foreach (var subcategory in subcategories.EnumerateArray())
                            {
                                if (subcategory.TryGetProperty("subcategoryName", out var subcategoryName) &&
                                    subcategoryName.GetString() == selectedSubcategory)
                                {
                                    if (subcategory.TryGetProperty("events", out var events))
                                    {
                                        PopulateControlEventListView(events);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Kontrol olayları işlenirken hata oluştu: {ex.Message}");
            }
        }

        private void PopulateControlEventListView(JsonElement events)
        {
            try
            {
                var eventItems = new ObservableCollection<EventItem>();

                foreach (var eventItem in events.EnumerateArray())
                {
                    if (eventItem.TryGetProperty("eventID", out var eventID) &&
                        eventItem.TryGetProperty("speechText", out var speechText))
                    {
                        // Remove {value} for display
                        string displayText = speechText.GetString().Replace("{value}", "").Trim();

                        eventItems.Add(new EventItem
                        {
                            EventID = eventID.GetString(),
                            SpeechText = displayText
                        });
                    }
                }

                controlEvent_ListView.ItemsSource = eventItems; // Bind the data to the ListView
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"Kontrol olayları yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void SpeechTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is string eventID)
            {
                // Store the original value when the TextBox gains focus
                if (!originalValues.ContainsKey(eventID))
                {
                    originalValues[eventID] = textBox.Text;
                }
            }
        }

        private void SpeechTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && textBox.Tag is string eventID)
                {
                    string newSpeechText = textBox.Text;

                    // Check if the value actually changed
                    if (originalValues.ContainsKey(eventID) && originalValues[eventID] == newSpeechText)
                    {
                        // No change, do nothing
                        return;
                    }

                    // Update the value in the temporary storage
                    if (pendingChanges.ContainsKey(eventID))
                    {
                        pendingChanges[eventID] = newSpeechText;
                    }
                    else
                    {
                        pendingChanges.Add(eventID, newSpeechText);
                    }

                    // Save to JSON
                    SaveChangeToJson(eventID, newSpeechText);

                    // Update the original value to the new one
                    originalValues[eventID] = newSpeechText;
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"SpeechText güncellenirken hata oluştu: {ex.Message}");
            }
        }

        private void SaveChangeToJson(string eventID, string newSpeechText)
        {
            try
            {
                string selectedProfile = profileSelect_cBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedProfile))
                {
                    appendToDebugConsole("Profil seçilmedi. JSON dosyası güncellenemedi.");
                    return;
                }

                string executionDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string speechFilesDirectory = System.IO.Path.Combine(executionDirectory, "Speech Files");
                string selectedFilePath = System.IO.Path.Combine(speechFilesDirectory, $"{selectedProfile}.json");

                if (!File.Exists(selectedFilePath))
                {
                    appendToDebugConsole($"Seçilen dosya bulunamadı: {selectedProfile}");
                    return;
                }

                // Load and update the JSON
                string jsonContent = File.ReadAllText(selectedFilePath);
                var rootObject = JsonSerializer.Deserialize<RootObject>(jsonContent);

                if (rootObject == null || rootObject.categories == null)
                {
                    appendToDebugConsole("Dosya yapısı beklenen formata uygun değil.");
                    return;
                }

                bool updated = false;

                // Find and update the matching event
                foreach (var category in rootObject.categories)
                {
                    foreach (var subcategory in category.subcategories)
                    {
                        foreach (var ev in subcategory.events)
                        {
                            if (ev.eventID == eventID)
                            {
                                // Ensure {value} exists in the speechText
                                if (!newSpeechText.Contains("{value}"))
                                {
                                    newSpeechText = $"{newSpeechText} {{value}}".Trim();
                                }

                                ev.speechText = newSpeechText;
                                updated = true;
                                break;
                            }
                        }
                        if (updated) break;
                    }
                    if (updated) break;
                }

                if (updated)
                {
                    try
                    {
                        // Serialize and write back to the file
                        string updatedJson = JsonSerializer.Serialize(rootObject, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(selectedFilePath, updatedJson);

                        // Update the loadedJsonDocument after saving
                        loadedJsonDocument = JsonDocument.Parse(updatedJson);

                        appendToDebugConsole($"Kontrol Güncellendi :{eventID}");
                    }
                    catch (Exception writeEx)
                    {
                        appendToDebugConsole($"Dosyası yazılırken hata oluştu: {writeEx.Message}");
                    }
                }
                else
                {
                    appendToDebugConsole($"Güncellenecek öğe bulunamadı: {eventID}");
                }
            }
            catch (Exception ex)
            {
                appendToDebugConsole($"JSON güncellenirken hata oluştu: {ex.Message}");
            }
        }

        private void LoadSpeechFilesToComboBox()
        {
            try
            {
                // Çalışma klasörünü al
                string executionDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Speech Files klasör yolunu oluştur
                string speechFilesDirectory = System.IO.Path.Combine(executionDirectory, "Speech Files");

                // Klasörün mevcut olup olmadığını kontrol et
                if (Directory.Exists(speechFilesDirectory))
                {
                    // JSON dosyalarını bul
                    string[] jsonFiles = Directory.GetFiles(speechFilesDirectory, "*.json");

                    if (jsonFiles.Length > 0)
                    {
                        // ComboBox'ı temizle ve dosya adlarını ekle
                        profileSelect_cBox.Items.Clear();

                        foreach (var filePath in jsonFiles)
                        {
                            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                            profileSelect_cBox.Items.Add(fileName); // Dosya adını ComboBox'a ekle
                        }

                        // Başarılı ekleme mesajı (isteğe bağlı)
                        debugConsole.Dispatcher.Invoke(() =>
                        {
                            debugConsole.Text += $"{jsonFiles.Length} Kontrol Profili Bulundu.\n";
                        });
                    }
                    else
                    {
                        // JSON dosyası yoksa bilgi mesajı
                        debugConsole.Dispatcher.Invoke(() =>
                        {
                            debugConsole.Text += "Speech Files klasöründe JSON dosyası bulunamadı.\n";
                        });
                    }
                }
                else
                {
                    // Klasör mevcut değilse bilgi mesajı
                    debugConsole.Dispatcher.Invoke(() =>
                    {
                        debugConsole.Text += "Speech Files klasörü bulunamadı.\n";
                    });
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda mesaj yazdır
                debugConsole.Dispatcher.Invoke(() =>
                {
                    debugConsole.Text += $"Hata: {ex.Message}\n";
                });
            }
        }

        #endregion

        #region SimConnect

        private SimConnectModule? simConnectModule;

        private void StartSimConnect()
        {
            simConnectModule = new SimConnectModule(debugConsole);
            this.Loaded += (s, e) => simConnectModule?.Connect(new WindowInteropHelper(this).Handle);
            this.Closing += (s, e) => simConnectModule?.Disconnect();
        }

        #endregion

        #region Controls


        private void appendToDebugConsole(string message)
        {
            debugConsole.Text += $"{message}\n"; // Mesajları alt alta ekle
        }

        private void controlEvent_Save_Copy_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        #endregion
    }
}