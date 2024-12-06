using NAudio.Wave;
using System.IO;
using Vosk;

namespace fs_copilot
{
    public class VoskSpeechRecognizer : IDisposable
    {
        private readonly VoskRecognizer recognizer;
        private readonly WaveInEvent waveIn;
        private readonly Model model;

        private string? lastDebugMessage; // Son yazılan debug mesajı
        private string? lastRecognitionResult; // Son tanıma sonucu

        public event Action<string>? OnRecognized; // Tam tanıma sonucu
        public event Action<string>? OnDebug; // Debug bilgileri

        public VoskSpeechRecognizer(string modelPath)
        {
            if (!Directory.Exists(modelPath))
            {
                LogDebug($"[ERROR] Model directory not found: {modelPath}");
                throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");
            }

            LogDebug("[INFO] Vosk modeli yükleniyor...");
            model = new Model(modelPath);
            recognizer = new VoskRecognizer(model, 16000.0f);

            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1) // 16 kHz mono
            };

            waveIn.DataAvailable += OnDataAvailable;

            LogDebug("[INFO] Mikrofon ayarları tamamlandı.");
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    string result = recognizer.Result();
                    if (result != lastRecognitionResult) // Aynı sonuç tekrar edilmesin
                    {
                        lastRecognitionResult = result;
                        LogDebug($"{result}");
                        OnRecognized?.Invoke(result);
                    }
                }
                // Partial mesajları devre dışı bırakıldı
            }
            catch (Exception ex)
            {
                LogDebug($"[ERROR] Tanıma sırasında hata oluştu: {ex.Message}");
            }
        }

        public void Start()
        {
            try
            {
                waveIn.StartRecording();
                LogDebug("[INFO] Mikrofon başlatıldı.");
            }
            catch (Exception ex)
            {
                LogDebug($"[ERROR] Mikrofon başlatılamadı: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                waveIn.StopRecording();
                LogDebug("[INFO] Mikrofon durduruldu.");
            }
            catch (Exception ex)
            {
                LogDebug($"[ERROR] Mikrofon durdurulamadı: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                waveIn?.Dispose();
                recognizer?.Dispose();
                model?.Dispose();
                LogDebug("[INFO] Kaynaklar serbest bırakıldı.");
            }
            catch (Exception ex)
            {
                LogDebug($"[ERROR] Kaynaklar serbest bırakılırken hata: {ex.Message}");
            }
        }

        private void LogDebug(string message)
        {
            if (message != lastDebugMessage) // Aynı mesajın tekrarını engelle
            {
                lastDebugMessage = message;
                OnDebug?.Invoke(message);
            }
        }
    }
}
