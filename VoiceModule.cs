using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using Whisper.net;
using Whisper.net.Ggml;
using System.Media; // SoundPlayer için


public class VoiceModule
{
    private readonly string _modelPath;
    private readonly string _audioFilePath;
    private readonly string _grammarPath;
    private readonly TextBlock _consoleOutput;

    private WaveInEvent _waveIn;
    private WaveFileWriter _waveFileWriter;
    private bool _isRecording = false;

    private List<string> _grammarChoices;
    private Dictionary<string, string> _dynamicChoices;

    public VoiceModule(string modelPath, string audioFilePath, string grammarPath, TextBlock consoleOutput)
    {
        _modelPath = modelPath;
        _audioFilePath = audioFilePath;
        _grammarPath = grammarPath;
        _consoleOutput = consoleOutput;

        InitializeAudioRecorder();
        LoadGrammarChoices();
        InitializeDynamicChoices();
    }




    private void Log(string message)
    {
        _consoleOutput.Dispatcher.Invoke(() => _consoleOutput.Text += message + Environment.NewLine);
    }

    private void InitializeAudioRecorder()
    {
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1) // 16kHz, Mono
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    private void LoadGrammarChoices()
    {
        if (File.Exists(_grammarPath))
        {
            try
            {
                var grammarJson = File.ReadAllText(_grammarPath);
                var grammarData = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(grammarJson);

                if (grammarData != null && grammarData.ContainsKey("StaticChoices"))
                {
                    _grammarChoices = grammarData["StaticChoices"];
                    Log($"[INFO] Grammar.json yüklendi. {_grammarChoices.Count} ifade tanımlandı.");
                }
                else
                {
                    Log("[WARNING] Grammar.json 'StaticChoices' içermiyor.");
                    _grammarChoices = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Grammar.json yüklenirken hata oluştu: {ex.Message}");
                _grammarChoices = new List<string>();
            }
        }
        else
        {
            Log("[ERROR] Grammar.json dosyası bulunamadı.");
            _grammarChoices = new List<string>();
        }
    }

    private void InitializeDynamicChoices()
    {
        // Dinamik seçimler burada tanımlanır
        _dynamicChoices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "echo", "E" },
            { "lima", "L" }
        };
        Log($"[INFO] Dinamik seçimler yüklendi. {_dynamicChoices.Count} ifade tanımlandı.");
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        _waveFileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        _waveFileWriter?.Dispose();
        _waveFileWriter = null;

        Log("[INFO] Ses kaydı tamamlandı. İşleme başlıyor...");
        ProcessRecordedAudio();
    }

    public void StartRecording()
    {
        if (_isRecording)
        {
            Log("[WARNING] Zaten kayıt yapılıyor.");
            return;
        }

        Log("[INFO] Ses kaydı başlatılıyor...");
        _waveFileWriter = new WaveFileWriter(_audioFilePath, _waveIn.WaveFormat);
        _waveIn.StartRecording();
        _isRecording = true;

        // 500ms sonra bip sesi çal
        PlayBeepAfterDelay(500);
    }

    private async void PlayBeepAfterDelay(int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds);

        try
        {
            // Bip sesi çalma
            Console.Beep(1000, 200); // 1000 Hz frekans, 200ms süre
            Log("[INFO] Bip sesi çalındı.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Bip sesi çalınırken hata oluştu: {ex.Message}");
        }
    }

    public void StopRecording()
    {
        if (!_isRecording)
        {
            Log("[WARNING] Kayıt yapılmıyor.");
            return;
        }

        Log("[INFO] Ses kaydı durduruluyor...");
        _waveIn.StopRecording();
        _isRecording = false;
    }

    private async void ProcessRecordedAudio()
    {
        try
        {
            if (!File.Exists(_modelPath))
            {
                Log("[INFO] Model indiriliyor...");
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                using var fileWriter = File.OpenWrite(_modelPath);
                await modelStream.CopyToAsync(fileWriter);
                Log("[INFO] Model indirildi.");
            }

            Log("[INFO] Ses dosyası işleniyor...");
            using var whisperFactory = WhisperFactory.FromPath(_modelPath);
            using var processor = whisperFactory.CreateBuilder()
                                                .WithLanguage("auto") // Dili otomatik algıla
                                                .Build();
            using var fileStream = File.OpenRead(_audioFilePath);

            var recognizedTexts = new List<string>();
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                recognizedTexts.Add(result.Text.Trim());
                Log($"[RECOGNIZED] {result.Text}");
            }

            Log("[INFO] İşlenmiş metin oluşturuluyor...");
            string finalResult = ProcessDynamicChoices(recognizedTexts);
            Log($"[RESULT] {finalResult}");

            Log("[INFO] Ses işleme tamamlandı.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Ses işlenirken bir hata oluştu: {ex.Message}");
        }
    }

    private string ProcessDynamicChoices(List<string> recognizedTexts)
    {
        var finalOutput = new List<string>();
        bool lastWasStatic = false; // Son işlenen öğenin StaticChoice olup olmadığını takip eder

        foreach (var text in recognizedTexts)
        {
            // 1. Metni normalize et
            string normalizedText = NormalizeText(text);

            // 2. Birleşik metinleri parçala
            var splitWords = SplitCombinedWords(normalizedText);

            // 3. Parçalanmış kelimeleri sırayla işle
            foreach (var word in splitWords)
            {
                // 4. DynamicChoices kontrolü
                if (_dynamicChoices.TryGetValue(word, out var dynamicValue))
                {
                    if (lastWasStatic)
                    {
                        finalOutput.Add(dynamicValue);
                    }
                    else
                    {
                        Log($"[ERROR] DynamicChoice '{word}' öncesinde StaticChoice olmadığından işlenemiyor.");
                    }
                }
                // 5. StaticChoices kontrolü
                else if (_grammarChoices.Contains(word, StringComparer.OrdinalIgnoreCase))
                {
                    if (lastWasStatic)
                    {
                        Log($"[ERROR] Arka arkaya iki StaticChoice işlenemez: '{word}' işlenemedi.");
                    }
                    else
                    {
                        finalOutput.Add(word);
                        lastWasStatic = true;
                    }
                }
                else
                {
                    Log($"[WARNING] '{word}' için eşleşme bulunamadı.");
                }

                // Eğer işlem yapılan bir DynamicChoice varsa, lastWasStatic'i false yap
                if (_dynamicChoices.ContainsKey(word))
                {
                    lastWasStatic = false;
                }
            }
        }

        // Sonuçları birleştir ve döndür
        return string.Join(" ", finalOutput);
    }

    private List<string> SplitCombinedWords(string text)
    {
        // Birleşik kelimeleri parçalamak için büyük harfleri ve boşlukları dikkate al
        var words = new List<string>();
        int wordStart = 0;

        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]) && !char.IsWhiteSpace(text[i - 1]))
            {
                // Yeni bir kelime başlangıcı
                words.Add(text.Substring(wordStart, i - wordStart).ToLower());
                wordStart = i;
            }
        }

        // Son kelimeyi ekle
        if (wordStart < text.Length)
        {
            words.Add(text.Substring(wordStart).ToLower());
        }

        return words;
    }


    private string GetClosestMatch(string input, List<string> choices)
    {
        int threshold = 70; // Benzerlik yüzdesi için eşik değeri
        string bestMatch = null;
        int highestScore = 0;

        // Normalize edilen giriş
        string normalizedInput = NormalizeText(input);

        foreach (var choice in choices)
        {
            string normalizedChoice = NormalizeText(choice);

            int score = CalculateLevenshteinSimilarity(normalizedInput, normalizedChoice);
            if (score > highestScore && score >= threshold)
            {
                highestScore = score;
                bestMatch = choice;
            }
        }

        return bestMatch;
    }

    private string NormalizeText(string text)
    {
        // Küçük harfe çevir, fazla boşlukları kaldır ve tüm noktalama işaretlerini sil
        return new string(
            text.ToLower()
                .Trim()
                .Where(c => !char.IsPunctuation(c))
                .ToArray()
        );
    }

    private int CalculateLevenshteinSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0;

        int[,] distance = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++)
            distance[i, 0] = i;

        for (int j = 0; j <= target.Length; j++)
            distance[0, j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost
                );
            }
        }

        int maxLength = Math.Max(source.Length, target.Length);
        int editDistance = distance[source.Length, target.Length];

        // Benzerlik yüzdesi
        return (int)((1.0 - (double)editDistance / maxLength) * 100);
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
    }
}
