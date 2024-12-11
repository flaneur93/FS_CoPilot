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
                Log($"[MATCHED EVENT] {matchedEvent.SpeechText}");
                Log($"[INFO] hasParam: {matchedEvent.HasParam}");

                if (matchedEvent.HasParam ?? false)
                {
                    string paramValue = ExtractParam(command, matchedEvent.SpeechText, matchedEvent.PType);
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


    private string ExtractParam(string command, string matchedText, string pType)
    {
        // Komuttan matchedText kısmını çıkar
        string remainingText = command.Replace(matchedText, "", StringComparison.OrdinalIgnoreCase).Trim();

        // pType'a göre parametreyi işle
        if (pType.Equals("Number", StringComparison.OrdinalIgnoreCase))
        {
            // Sayıları ayıkla
            string number = new string(remainingText.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(number) ? "No number found" : number;
        }
        else if (pType.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            // Metni olduğu gibi döndür
            return remainingText;
        }

        return "Unknown param type";
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
