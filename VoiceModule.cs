using NAudio.Wave;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Whisper.net;
using Whisper.net.Ggml;

public class VoiceModule
{
    private readonly string _modelPath;
    private readonly string _audioFilePath;
    private readonly TextBlock _consoleOutput;

    private WaveInEvent _waveIn;
    private WaveFileWriter _waveFileWriter;
    private bool _isRecording = false;
    private readonly DataManager _dataManager;


    public VoiceModule(string modelPath, string audioFilePath, TextBlock consoleOutput, DataManager dataManager)
    {
        _modelPath = modelPath;
        _audioFilePath = audioFilePath;
        _consoleOutput = consoleOutput;
        _dataManager = dataManager;


        InitializeAudioRecorder();
        _dataManager = dataManager;
    }

    private static readonly Dictionary<string, string> PhoneticAlphabet = new()
    {
        { "echo", "E" },
        { "lima", "L" },
        { "tango", "T" },
        { "zulu", "Z" },
        { "alpha", "A" },
        { "bravo", "B" },
        { "charlie", "C" },
        { "delta", "D" },
        { "foxtrot", "F" },
        { "golf", "G" },
        { "hotel", "H" },
        { "india", "I" },
        { "juliett", "J" },
        { "kilo", "K" },
        { "mike", "M" },
        { "november", "N" },
        { "oscar", "O" },
        { "papa", "P" },
        { "quebec", "Q" },
        { "romeo", "R" },
        { "sierra", "S" },
        { "uniform", "U" },
        { "victor", "V" },
        { "whiskey", "W" },
        { "xray", "X" },
        { "yankee", "Y" }
    };

    private string ConvertPhoneticToLetters(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var word in words)
        {
            // En yakın phonetic eşleşmeyi bul
            string closestMatch = FindClosestPhoneticMatch(word);

            if (!string.IsNullOrEmpty(closestMatch) && PhoneticAlphabet.TryGetValue(closestMatch, out string letter))
            {
                result.Append(letter); // Kodlama harfini ekle
            }
            else
            {
                result.Append("?"); // Eşleşmeyenler için hata işareti
            }
        }

        return result.ToString();
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
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        _waveFileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
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
                string cleanText = RemovePunctuation(result.Text.Trim()); // Noktalama işaretlerini kaldır
                recognizedTexts.Add(cleanText);
                
            }

            // Komutları profile göre işle
            foreach (var text in recognizedTexts)
            {
                ProcessCommand(text);
            }

            Log("[INFO] Ses işleme tamamlandı.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Ses işlenirken bir hata oluştu: {ex.Message}");
        }
    }

    private void ProcessCommand(string command)
    {
        var speechTexts = _dataManager.GetSpeechTexts(); // Profildeki speechText ifadeleri
        int nGramSize = 2;

        // N-Gram tabanlı en iyi eşleşmeyi bul
        string bestMatch = FindBestMatchWithNGram(command, speechTexts, nGramSize);

        if (!string.IsNullOrEmpty(bestMatch))
        {
            // Eşleşen komutun Event nesnesini al
            var matchedEvent = _dataManager.GetEventBySpeechText(bestMatch);

            if (matchedEvent != null)
            {
                Log($"[RECOGNIZED] {command}");
                Log($"[MATCHED EVENT] {matchedEvent.JsName}");
                Log($"[COMMAND] {matchedEvent.SpeechText}");
                Log($"[INFO] hasParam: {matchedEvent.HasParam}");

                if (matchedEvent.HasParam ?? false)
                {
                    // `ExtractParam` için mappedValues sağla
                    string paramValue = ExtractParam(
                        command,
                        matchedEvent.SpeechText,
                        matchedEvent.PType,
                        matchedEvent.PMap // Mapped değerler
                    );

                    Log($"[EVENT PARAM] {paramValue}");
                    Log($"[INFO] pType: {matchedEvent.PType}");
                }
            }
            else
            {
                Log("[ERROR] Eşleşen Event bulunamadı.");
            }
        }
        else
        {
            Log("[INFO] Uygun eşleşme bulunamadı.");
        }
    }

    private string FindBestMatchWithNGram(string command, List<string> speechTexts, int n)
    {
        string bestMatch = null;
        double highestSimilarity = 0;

        foreach (var speechText in speechTexts)
        {
            double similarity = CalculateNGramSimilarity(command, speechText, n);
            if (similarity > highestSimilarity)
            {
                highestSimilarity = similarity;
                bestMatch = speechText;
            }
        }

        return bestMatch;
    }

    private string NormalizeText(string text)
    {
        return string.Join(" ", text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        _waveFileWriter?.Dispose();
        _waveFileWriter = null;

        Log("[INFO] Ses kaydı tamamlandı. İşleme başlıyor...");
        ProcessRecordedAudio();
    }

    public async void PlayBeepAfterDelay(int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds);
        try
        {
            // Bip sesi çalma
            Console.Beep(2000, 300); // 1000 Hz frekans, 200ms süre
            Log("[INFO] Bip sesi çalındı.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Bip sesi çalınırken hata oluştu: {ex.Message}");
        }
    }

    private List<string> GenerateNGrams(string text, int n)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ngrams = new List<string>();

        for (int i = 0; i <= words.Length - n; i++)
        {
            ngrams.Add(string.Join(" ", words.Skip(i).Take(n)));
        }

        return ngrams;
    }

    private double CalculateNGramSimilarity(string text1, string text2, int n)
    {
        var ngrams1 = GenerateNGrams(NormalizeText(text1), n);
        var ngrams2 = GenerateNGrams(NormalizeText(text2), n);

        var intersection = ngrams1.Intersect(ngrams2).Count();
        var union = ngrams1.Union(ngrams2).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private string RemoveTrailingPunctuation(string text)
    {
        return text.TrimEnd('.', '!', '?', ',', ';', ':'); // Sonundaki belirli işaretleri kaldırır
    }

    private string RemovePunctuation(string text)
    {
        return new string(text.Where(c => !char.IsPunctuation(c)).ToArray());
    }

    private string ExtractParam(string command, string matchedText, string pType, Dictionary<string, string> mappedValues = null)
    {
        // Komuttan matchedText kısmını çıkar
        string remainingText = command.Replace(matchedText, "", StringComparison.OrdinalIgnoreCase).Trim();

        // pType'a göre parametreyi işle
        if (pType.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            // Sayıları ayıkla
            string number = new string(remainingText.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(number) ? "No number found" : number;
        }
        else if (pType.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            // PhoneticAlphabet'e göre metni dönüştür
            return ConvertPhoneticToLetters(remainingText);
        }
        else if (pType.Equals("mapped", StringComparison.OrdinalIgnoreCase))
        {
            if (mappedValues == null || mappedValues.Count == 0)
            {
                return "No mapped values found";
            }

            // Mapped değerlerini kullanarak dönüşüm yap
            var words = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (mappedValues.TryGetValue(word.ToLower(), out string mappedValue))
                {
                    result.Append(mappedValue); // Mapped değeri ekle
                }
                else
                {
                    result.Append("?"); // Eşleşmeyen kelimeler için hata işareti
                }
            }

            return result.ToString();
        }

        return "Unknown param type";
    }

    private string FindClosestPhoneticMatch(string input)
    {
        string bestMatch = null;
        int smallestDistance = int.MaxValue;

        foreach (var key in PhoneticAlphabet.Keys)
        {
            int distance = CalculateLevenshteinDistance(input.ToLower(), key.ToLower());
            if (distance < smallestDistance)
            {
                smallestDistance = distance;
                bestMatch = key;
            }
        }

        return bestMatch;
    }

    private int CalculateLevenshteinDistance(string source, string target)
    {
        int[,] dp = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= target.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[source.Length, target.Length];
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

    public void Dispose()
    {
        _waveIn?.Dispose();
    }
}
