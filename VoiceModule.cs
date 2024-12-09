using fs_copilot;
using System;
using System.IO;
using System.Speech.Recognition;
using System.Text.Json;

public class VoiceModule
{
    private SpeechRecognitionEngine recognizer;

    public VoiceModule()
    {
        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] VoiceModule oluşturuluyor...");
            recognizer = new SpeechRecognitionEngine();
            MainWindow.Instance.WriteToMainConsole("[DEBUG] SpeechRecognitionEngine başarıyla oluşturuldu.");
            InitializeRecognizer();
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] VoiceModule başlatılamadı: {ex.Message}");
        }
    }

    private readonly Dictionary<string, string> _customMappings = new Dictionary<string, string>
{
    { "Lima", "L" },
    { "Alpha", "A" },
    { "Bravo", "B" },
    { "Charlie", "C" }
};


    private void InitializeRecognizer()
    {
        try
        {
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Recognizer başlatılıyor...");

            // Eski tanıma motorunu durdur ve serbest bırak
            if (recognizer != null)
            {
                recognizer.RecognizeAsyncStop();
                recognizer.UnloadAllGrammars();
                recognizer.Dispose();
                MainWindow.Instance.WriteToMainConsole("[DEBUG] Eski Recognizer temizlendi.");
            }

            // Yeni tanıma motoru oluştur
            recognizer = new SpeechRecognitionEngine();
            recognizer.SetInputToDefaultAudioDevice(); // Mikrofonu varsayılan giriş olarak ayarla
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Yeni Recognizer oluşturuldu ve mikrofon ayarlandı.");

            // Yeni grammar'ı yükle
            LoadGrammarFromJson();

            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            recognizer.SpeechRecognitionRejected += Recognizer_SpeechRecognitionRejected;
            MainWindow.Instance.WriteToMainConsole("[DEBUG] Yeni Recognizer event'leri bağlandı.");
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Recognizer başlatılamadı: {ex.Message}");
        }
    }

    private readonly Dictionary<string, string> customMappings = new Dictionary<string, string>
{
    { "Alpha", "A" },
    { "Bravo", "B" },
    { "Charlie", "C" },
    { "One", "1" },
    { "Two", "2" }
};






    private void LoadGrammarFromJson()
    {
        try
        {
            string grammarPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Grammar.json");
            if (System.IO.File.Exists(grammarPath))
            {
                string grammarContent = System.IO.File.ReadAllText(grammarPath);
                var grammarData = JsonSerializer.Deserialize<GrammarFile>(grammarContent);

                if (grammarData?.Grammar != null && grammarData.Grammar.Count > 0)
                {
                    var choices = new Choices(grammarData.Grammar.ToArray());
                    var grammarBuilder = new GrammarBuilder(choices);
                    var grammar = new Grammar(grammarBuilder);

                    recognizer.LoadGrammar(grammar);
                    MainWindow.Instance.WriteToMainConsole("[INFO] Yeni Grammar başarıyla yüklendi.");
                }
                else
                {
                    MainWindow.Instance.WriteToMainConsole("[ERROR] Yeni Grammar.json içinde geçerli gramer bulunamadı.");
                }
            }
            else
            {
                MainWindow.Instance.WriteToMainConsole("[ERROR] Yeni Grammar.json dosyası bulunamadı.");
            }
        }
        catch (Exception ex)
        {
            MainWindow.Instance.WriteToMainConsole($"[ERROR] Grammar yüklenirken hata oluştu: {ex.Message}");
        }
    }

    private class GrammarFile
    {
        public List<string> Grammar { get; set; }
    }

    private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
    {
        try
        {
            string recognizedText = e.Result.Text; // Tanınan tüm metin
            string[] words = recognizedText.Split(' '); // Kelimeleri boşlukla ayır
            string finalResult = ""; // Nihai sonuç

            foreach (var word in words)
            {
                if (_customMappings.ContainsKey(word))
                {
                    // Kod tarafındaki tanımlamaları birleştir
                    finalResult += _customMappings[word];
                }
                else
                {
                    // Grammar'den gelen kelimeler olduğu gibi yazılır (boşluk eklenir)
                    if (!string.IsNullOrEmpty(finalResult))
                    {
                        finalResult += " "; // Kod tarafındaki ve grammar arasına boşluk ekle
                    }

                    finalResult += word; // Grammar'deki kelimeyi olduğu gibi ekle
                }
            }

            // Nihai çıktıyı konsola yaz
            MainWindow.Instance.WriteToMainConsole(finalResult);
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
}




