using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FlightSimulator.SimConnect; // SimConnect için gerekli kütüphane
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace fs_copilot
{
    public class SimConnectModule
    {
        private const int WM_USER_SIMCONNECT = 0x0402;
        private SimConnect? simConnect;
        private readonly TextBlock debugConsole;

        public SimConnectModule(TextBlock debugConsole)
        {
            this.debugConsole = debugConsole;
        }

        public void Connect(IntPtr windowHandle)
        {
            try
            {
                simConnect = new Microsoft.FlightSimulator.SimConnect.SimConnect("Managed Data Request", windowHandle, WM_USER_SIMCONNECT, null, 0);
                WriteToDebugConsole("SimConnect bağlantısı başarılı.");
            }
            catch (COMException ex)
            {
                WriteToDebugConsole("Conenction with MSFS Failed.", true);
            }
        }

        public void Disconnect()
        {
            if (simConnect != null)
            {
                simConnect.Dispose();
                simConnect = null;
                WriteToDebugConsole("SimConnect bağlantısı kesildi.");
            }
        }

        private void WriteToDebugConsole(string message, bool isError = false)
        {
            debugConsole.Dispatcher.Invoke(() =>
            {
                var text = new Run(message)
                {
                    Foreground = isError ? Brushes.IndianRed : Brushes.Green // Hata ise kırmızı, değilse siyah
                };
                debugConsole.Inlines.Add(text);
                debugConsole.Inlines.Add(new LineBreak()); // Satır sonu ekle
            });
        }
    }
}
