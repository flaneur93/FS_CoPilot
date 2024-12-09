using fs_copilot;
using System;
using System.IO;
using System.Speech.Recognition;
using System.Text.Json;

public class VoiceModule
{
    private SpeechRecognitionEngine recognizer;
    private List<string> recognitionBuffer = new List<string>();
    private System.Timers.Timer resultTimer;

    public VoiceModule()
    {


        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] VoiceModule oluşturuluyor...");
            recognizer = new SpeechRecognitionEngine();
            MainWindow.Instance.WriteToMainConsole("[DEBUG] SpeechRecognitionEngine başarıyla oluşturuldu.");
            InitializeRecognizer();
            resultTimer = new System.Timers.Timer(1000); // 1 saniye
            resultTimer.Elapsed += ProcessRecognitionBuffer;
            resultTimer.AutoReset = false;

        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] VoiceModule başlatılamadı: {ex.Message}");
        }
    }

    private void InitializeRecognizer()
    {
        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Recognizer başlatılıyor...");

            // Eski tanıma motorunu temizle
            if (recognizer != null)
            {
                recognizer.RecognizeAsyncStop();
                recognizer.Dispose();
            }

            // Yeni tanıma motorunu oluştur
            recognizer = new SpeechRecognitionEngine();
            recognizer.SetInputToDefaultAudioDevice();
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Yeni Recognizer oluşturuldu ve mikrofon ayarlandı.");

            // Sessizlik sürelerini ayarla
            recognizer.EndSilenceTimeout = TimeSpan.FromMilliseconds(30); // Normal tanımalar için sessizlik süresi
            recognizer.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(100); // Belirsiz tanımalar için sessizlik süresi

            // Grammar'ı yükle
            LoadGrammarFromJson();

            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            recognizer.SpeechRecognitionRejected += Recognizer_SpeechRecognitionRejected;

            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            MainWindow.Instance.WriteToMainConsole("[INFO] Sesli algılama başlatıldı.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Recognizer başlatılamadı: {ex.Message}");
        }
    }

    private readonly Dictionary<string, string> _customMappings = new Dictionary<string, string>
{
    { "Alpha", "A" },
    { "Bravo", "B" },
    { "Charlie", "C" },
    { "One", "1" },
    { "Two", "2" }
};

    private void ProcessRecognitionBuffer(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            if (recognitionBuffer.Count > 0)
            {
                // Tüm tanıma sonuçlarını birleştir
                string finalResult = string.Join(" ", recognitionBuffer);

                // Konsola yaz
                MainWindow.Instance.WriteToMainConsole($"[RECOGNIZED] {finalResult}");

                // Buffer'ı temizle
                recognitionBuffer.Clear();
            }
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Buffer işlenirken hata oluştu: {ex.Message}");
        }
    }







    private void LoadGrammarFromJson()
    {
        try
        {
            var staticChoices = new Choices(); // Sabit ifadeler için
            var dynamicChoices = new Choices(); // Dinamik ifadeler için (Lima, Tango, Alpha, Bravo gibi)

            // Sabit ifadeleri ekle
            staticChoices.Add("get weather data");

            // Haritalamadaki dinamik ifadeleri ekle
            foreach (var key in _customMappings.Keys)
            {
                dynamicChoices.Add(key);
            }

            // Grammar Builder oluştur
            var grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(staticChoices); // İlk kısmı ekle (örneğin, "get weather data")
            grammarBuilder.Append(dynamicChoices, 0, 10); // Ardışık kelimeleri destekle (0-10 kelime arası)

            // Grammar'ı yükle
            var grammar = new Grammar(grammarBuilder);
            recognizer.LoadGrammar(grammar);

            MainWindow.Instance.WriteToMainConsole("[INFO] Grammar ve CustomMappings başarıyla yüklendi.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Grammar yüklenirken hata oluştu: {ex.Message}");
        }
    }

    private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
    {
        try
        {
            string recognizedText = e.Result.Text; // Tanınan tüm metin
            string[] words = recognizedText.Split(' '); // Kelimeleri ayır
            string staticPart = ""; // Sabit komut kısmı
            string dynamicPart = ""; // Dinamik kısım (haritalamalar)

            bool isDynamicPart = false;

            foreach (var word in words)
            {
                if (_customMappings.ContainsKey(word))
                {
                    // Haritalanmış kelimeler dinamik kısma eklenir
                    dynamicPart += _customMappings[word];
                    isDynamicPart = true;
                }
                else
                {
                    // Haritalanmamış kelimeler sabit kısma eklenir
                    if (!isDynamicPart)
                    {
                        staticPart += word + " ";
                    }
                    else
                    {
                        // Dinamik kısımdan sonra gelen haritalanmamış kelime
                        dynamicPart += " " + word;
                    }
                }
            }

            // Nihai çıktıyı konsola yaz
            MainWindow.Instance.WriteToMainConsole($"[COMMAND] {staticPart.Trim()}");
            MainWindow.Instance.WriteToMainConsole($"[DYNAMIC] {dynamicPart.Trim()}");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Algılanan ses işlenemedi: {ex.Message}");
        }
    }



    private void Recognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
    {
        MainWindow.Instance.WriteToMainConsole("[INFO] Ses algılandı ama tanınamadı.");
    }

    public void StartRecognition()
    {
        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Sesli algılama başlatılıyor...");
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            MainWindow.Instance.WriteToMainConsole("[INFO] Sesli algılama başladı.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Sesli algılama başlatılamadı: {ex.Message}");
        }
    }

    public void StopRecognition()
    {
        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Sesli algılama durduruluyor...");
            recognizer.RecognizeAsyncStop();
            MainWindow.Instance.WriteToMainConsole("[INFO] Sesli algılama durduruldu.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Sesli algılama durdurulamadı: {ex.Message}");
        }
    }

    public void RestartRecognition()
    {
        MainWindow.Instance.WriteToMainConsole("[DEBUG] VoiceModule yeniden başlatılıyor...");
        InitializeRecognizer();
        StartRecognition();
    }

    private class GrammarFile
    {
        public List<string> Grammar { get; set; }
    }


}







