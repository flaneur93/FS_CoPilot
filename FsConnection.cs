using fs_copilot;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class FsConnection
{
    private readonly MainWindow _mainWindow;
    private bool isConnected = false;
    private DataManager _dataManager = new DataManager();
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

    [DllImport("SimConnectWrapper.dll")]
    private static extern int MapClientEventToSimEvent(uint eventID, string simEventName);

    [DllImport("SimConnectWrapper.dll")]
    private static extern int SendEvent(uint eventID, uint data, uint groupID);

    [DllImport("SimConnectWrapper.dll")]
    private static extern int AddDynamicDataDefinition(string variableName, string unit);

    [DllImport("SimConnectWrapper.dll")]
    private static extern int RequestDynamicData(string variableName);




    public void InitializeMappings()
    {
        var eventMappings = _dataManager.GetEventMappings(); // DataManager'dan Event eşleştirmelerini al

        MapClientEvents(eventMappings);
    }

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

    public void MapClientEvents(Dictionary<uint, string> eventMappings)
    {
        if (!isConnected)
        {
            _mainWindow.WriteToMainConsole("[ERROR] SimConnect bağlantısı kurulmadı.");
            return;
        }

        foreach (var mapping in eventMappings)
        {
            uint eventID = mapping.Key; // JSON'dan alınan Event ID
            string simEventName = mapping.Value; // JSON'daki SimConnect Event Name

            int result = MapClientEventToSimEvent(eventID, simEventName);
            if (result == 0)
            {
                _mainWindow.WriteToMainConsole($"[INFO] MapClientEventToSimEvent: {eventID} -> {simEventName}");
            }
            else
            {
                _mainWindow.WriteToMainConsole($"[ERROR] MapClientEventToSimEvent başarısız: {eventID} -> {simEventName}");
            }
        }
    }

    public void SendSimCommand(uint eventID, uint data = 0, uint groupID = 0)
    {
        if (!isConnected)
        {
            _mainWindow.WriteToMainConsole("[ERROR] SimConnect bağlantısı kurulmadı.");
            return;
        }

        int result = SendEvent(eventID, data, groupID);
        if (result == 0)
        {
            _mainWindow.WriteToMainConsole($"[INFO] SimConnect komutu başarıyla gönderildi: Event ID={eventID}, Data={data}");
        }
        else
        {
            _mainWindow.WriteToMainConsole($"[ERROR] SimConnect komutu gönderilemedi! Event ID={eventID}, Data={data}");
        }
    }


}
