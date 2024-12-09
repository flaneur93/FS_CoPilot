using fs_copilot;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class FsConnection
{
    private readonly MainWindow _mainWindow;
    private bool isConnected = false;
    private bool isFsRunningLastCheck = false; // Son kontrol durumunu saklar
    private bool isFirstCheck = true;         // İlk kontrol durumu

    public FsConnection(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow), "MainWindow null olamaz!");
    }

    [DllImport("SimConnectWrapper.dll")]
    private static extern int Connect();


    [DllImport("SimConnectWrapper.dll")]
    private static extern int Disconnect();





    private bool IsFlightSimulatorRunning()
    {
        Process[] fs2020 = Process.GetProcessesByName("FlightSimulator");
        Process[] fs2024 = Process.GetProcessesByName("FlightSimulator2024"); // Simülatör adı doğruysa ekleyin
        return fs2020.Length > 0 || fs2024.Length > 0;
    }

    public async Task ConnectToFSAsync()
    {
        _mainWindow.WriteToMainConsole("[INFO] Flight Simulator kontrol ediliyor...");

        while (!isConnected)
        {
            bool isFsRunning = IsFlightSimulatorRunning();

            // İlk kontrol veya durum değişikliği varsa mesaj göster
            if (isFirstCheck || isFsRunning != isFsRunningLastCheck)
            {
                if (isFsRunning)
                {
                    _mainWindow.WriteToMainConsole("[INFO] Flight Simulator çalışıyor. SimConnect'e bağlanılıyor...");
                    int result = Connect();

                    if (result == 0)
                    {
                        isConnected = true;
                        _mainWindow.WriteToMainConsole("[INFO] SimConnect bağlantısı başarıyla kuruldu.");
                    }
                    else
                    {
                        _mainWindow.WriteToMainConsole($"[ERROR] SimConnect bağlantısı başarısız! Hata kodu: {result}");
                    }
                }
                else
                {
                    _mainWindow.WriteToMainConsole("[INFO] Flight Simulator açık değil. Lütfen başlatın.");
                }

                // İlk kontrolü tamamlandı olarak işaretle
                isFirstCheck = false;
            }

            isFsRunningLastCheck = isFsRunning;
            await Task.Delay(5000); // 5 saniye bekle
        }
    }

    public void DisconnectFromFS()
    {
        if (!isConnected)
        {
            _mainWindow.WriteToMainConsole("[INFO] SimConnect zaten bağlı değil.");
            return;
        }

        _mainWindow.WriteToMainConsole("[INFO] SimConnect bağlantısı kapatılıyor...");
        int result = Disconnect();

        if (result == 0)
        {
            isConnected = false;
            _mainWindow.WriteToMainConsole("[INFO] SimConnect bağlantısı başarıyla kapatıldı.");
        }
        else
        {
            _mainWindow.WriteToMainConsole($"[ERROR] SimConnect bağlantısı kapatılamadı! Hata kodu: {result}");
        }
    }
}
