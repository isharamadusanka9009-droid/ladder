using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace LadderToArduino.Services
{
    public class MonitorState
    {
        public bool[] I = Array.Empty<bool>();
        public bool[] Q = Array.Empty<bool>();
        public bool[] M = Array.Empty<bool>();
        public bool[] T = Array.Empty<bool>();
        public bool[] C = Array.Empty<bool>();
    }

    // Opens the board's serial port and listens for the "S,I=...,Q=...,M=...,T=...,C=..." lines
    // that CodeGenerator's printState() emits every scan, so the app can show live I/O without
    // any extra hardware or protocol beyond what's already in the generated sketch.
    public class MonitorService : IDisposable
    {
        private SerialPort _port;
        public event Action<MonitorState> StateReceived;
        public event Action<string> Error;
        public bool IsOpen => _port != null && _port.IsOpen;

        public void Start(string portName, int baud = 9600)
        {
            Stop();
            try
            {
                _port = new SerialPort(portName, baud) { NewLine = "\n", ReadTimeout = 2000 };
                _port.DataReceived += OnDataReceived;
                _port.Open();
            }
            catch (Exception ex)
            {
                Error?.Invoke("Could not open " + portName + ": " + ex.Message);
                _port = null;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _port.ReadLine();
                var state = Parse(line);
                if (state != null) StateReceived?.Invoke(state);
            }
            catch { /* incomplete line / timeout - ignore, next line will resync */ }
        }

        public static MonitorState Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("S,")) return null;
            var dict = new Dictionary<string, string>();
            foreach (var part in line.Substring(2).Split(','))
            {
                var kv = part.Split('=');
                if (kv.Length == 2) dict[kv[0].Trim()] = kv[1].Trim();
            }
            return new MonitorState
            {
                I = ToBoolArray(dict.GetValueOrDefault("I", "")),
                Q = ToBoolArray(dict.GetValueOrDefault("Q", "")),
                M = ToBoolArray(dict.GetValueOrDefault("M", "")),
                T = ToBoolArray(dict.GetValueOrDefault("T", "")),
                C = ToBoolArray(dict.GetValueOrDefault("C", ""))
            };
        }

        private static bool[] ToBoolArray(string s) => s.Select(ch => ch == '1').ToArray();

        public void Stop()
        {
            if (_port == null) return;
            try
            {
                _port.DataReceived -= OnDataReceived;
                if (_port.IsOpen) _port.Close();
                _port.Dispose();
            }
            catch { /* ignore close errors */ }
            _port = null;
        }

        public void Dispose() => Stop();
    }
}
