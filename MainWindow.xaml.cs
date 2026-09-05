using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LadderToArduino.Models;
using LadderToArduino.Services;
using Microsoft.Win32;

namespace LadderToArduino
{
    public partial class MainWindow : Window
    {
        private LadderProgram _program = new LadderProgram();
        private readonly ArduinoCliService _cli = new ArduinoCliService();
        private readonly string _buildDir = Path.Combine(Path.GetTempPath(), "LadderToArduinoBuild");

        // Undo / Redo — full-program JSON snapshots. Simple and reliable given how small these projects are.
        private readonly Stack<string> _undoStack = new Stack<string>();
        private readonly Stack<string> _redoStack = new Stack<string>();

        // Rung copy/paste clipboard (kept in memory only, not the OS clipboard).
        private Rung _clipboardRung;

        // Simulation
        private SimulationViewModel _simVm;
        private DispatcherTimer _simTimer;

        // Live monitor (reads the Serial state line the generated sketch prints each scan)
        private readonly MonitorService _monitor = new MonitorService();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _program;

            BoardCombo.ItemsSource = new[]
            {
                "arduino:avr:uno",
                "arduino:avr:nano",
                "arduino:avr:mega",
                "arduino:avr:leonardo",
                "esp32:esp32:esp32"
            };
            BoardCombo.Text = _program.BoardFqbn;

            _monitor.StateReceived += OnMonitorState;
            _monitor.Error += OnMonitorError;

            RefreshPorts_Click(null, null);
            Log("Ready. Build a ladder, map your pins, then Build & Upload.");
            Log("First time setup: install arduino-cli (https://arduino.github.io/arduino-cli/) " +
                "and run 'arduino-cli core install arduino:avr' once.");
        }

        // ================= undo / redo =================

        private void PushUndo()
        {
            _undoStack.Push(ProjectFileService.ToJson(_program));
            _redoStack.Clear();
        }

        private void RestoreProgram(LadderProgram program)
        {
            _program = program;
            DataContext = _program;
            BoardCombo.Text = _program.BoardFqbn;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) { Log("Nothing to undo."); return; }
            _redoStack.Push(ProjectFileService.ToJson(_program));
            RestoreProgram(ProjectFileService.FromJson(_undoStack.Pop()));
            Log("Undo.");
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) { Log("Nothing to redo."); return; }
            _undoStack.Push(ProjectFileService.ToJson(_program));
            RestoreProgram(ProjectFileService.FromJson(_redoStack.Pop()));
            Log("Redo.");
        }

        // ================= rung / branch / contact / output editing =================

        private void AddRung_Click(object sender, RoutedEventArgs e)
        {
            PushUndo();
            _program.Rungs.Add(new Rung());
        }

        private void DeleteRung_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                PushUndo();
                _program.Rungs.Remove(rung);
            }
        }

        private void CopyRung_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                _clipboardRung = ProjectFileService.CloneRung(rung);
                Log("Rung copied. Use \"Paste Rung\" to add it to the end.");
            }
        }

        private void PasteRung_Click(object sender, RoutedEventArgs e)
        {
            if (_clipboardRung == null) { Log("Clipboard is empty — copy a rung first."); return; }
            PushUndo();
            _program.Rungs.Add(ProjectFileService.CloneRung(_clipboardRung));
        }

        private void MoveRungUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                int idx = _program.Rungs.IndexOf(rung);
                if (idx > 0) { PushUndo(); _program.Rungs.Move(idx, idx - 1); }
            }
        }

        private void MoveRungDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                int idx = _program.Rungs.IndexOf(rung);
                if (idx >= 0 && idx < _program.Rungs.Count - 1) { PushUndo(); _program.Rungs.Move(idx, idx + 1); }
            }
        }

        private void AddBranch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                PushUndo();
                var branch = new Branch();
                branch.Contacts.Add(new Contact());
                rung.Branches.Add(branch);
            }
        }

        private void DeleteBranch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Branch branch)
            {
                foreach (var rung in _program.Rungs)
                {
                    if (rung.Branches.Contains(branch))
                    {
                        if (rung.Branches.Count > 1) { PushUndo(); rung.Branches.Remove(branch); }
                        else MessageBox.Show("A rung needs at least one branch.");
                        break;
                    }
                }
            }
        }

        private void AddContact_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Branch branch)
            {
                PushUndo();
                branch.Contacts.Add(new Contact());
            }
        }

        private void DeleteContact_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Contact contact)
            {
                foreach (var rung in _program.Rungs)
                    foreach (var branch in rung.Branches)
                        if (branch.Contacts.Contains(contact))
                        {
                            PushUndo();
                            branch.Contacts.Remove(contact);
                            return;
                        }
            }
        }

        private void AddOutput_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Rung rung)
            {
                PushUndo();
                rung.Outputs.Add(new CoilOutput());
            }
        }

        private void DeleteOutput_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CoilOutput output)
            {
                foreach (var rung in _program.Rungs)
                {
                    if (rung.Outputs.Contains(output))
                    {
                        if (rung.Outputs.Count > 1) { PushUndo(); rung.Outputs.Remove(output); }
                        else MessageBox.Show("A rung needs at least one output.");
                        break;
                    }
                }
            }
        }

        // ================= pin mapping =================

        private void AddPinMapping_Click(object sender, RoutedEventArgs e)
        {
            PushUndo();
            _program.PinMap.Add(new PinMapping());
        }

        private void DeletePinMapping_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is PinMapping pm)
            {
                PushUndo();
                _program.PinMap.Remove(pm);
            }
        }

        // ================= cross reference =================

        private void RefreshXRef_Click(object sender, RoutedEventArgs e)
        {
            var contactUse = new Dictionary<string, List<int>>();
            var coilUse = new Dictionary<string, List<int>>();
            int rungNo = 0;

            foreach (var rung in _program.Rungs)
            {
                rungNo++;
                foreach (var b in rung.Branches)
                    foreach (var c in b.Contacts)
                        AddUse(contactUse, AddressText.From(c.Kind, c.Index), rungNo);

                foreach (var o in rung.Outputs)
                {
                    AddUse(coilUse, AddressText.From(o.OutputKind, o.OutputIndex), rungNo);
                    if (o.ResetIndex >= 0)
                        AddUse(contactUse, AddressText.From(o.ResetKind, o.ResetIndex), rungNo);
                    if (o.CoilType == CoilType.AnalogOutput && o.AnalogSource == AnalogSource.CopyFromAnalogInput)
                        AddUse(contactUse, AddressText.From(AddressKind.AnalogInput, o.AnalogSourceIndex), rungNo);
                }
            }

            var allAddrs = new SortedSet<string>(contactUse.Keys.Concat(coilUse.Keys), StringComparer.Ordinal);
            var rows = allAddrs.Select(a => new XRefRow
            {
                Address = a,
                UsedAsContactIn = contactUse.TryGetValue(a, out var cl) ? string.Join(", ", cl.Distinct().OrderBy(x => x)) : "",
                UsedAsCoilIn = coilUse.TryGetValue(a, out var ol) ? string.Join(", ", ol.Distinct().OrderBy(x => x)) : ""
            }).ToList();

            XRefGrid.ItemsSource = rows;
            Log($"Cross-reference refreshed: {rows.Count} address(es).");
        }

        private static void AddUse(Dictionary<string, List<int>> dict, string addr, int rungNo)
        {
            if (!dict.TryGetValue(addr, out var list)) { list = new List<int>(); dict[addr] = list; }
            list.Add(rungNo);
        }

        // ================= simulation =================

        private void SimLoad_Click(object sender, RoutedEventArgs e)
        {
            _simVm = new SimulationViewModel(_program);
            SimPanel.DataContext = _simVm;
            Log("Simulation loaded from the current ladder. Toggle inputs, then Step or Run.");
        }

        private void SimStep_Click(object sender, RoutedEventArgs e)
        {
            if (_simVm == null) { Log("Click \"Load / Reset\" first."); return; }
            _simVm.Tick();
        }

        private void SimRunToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_simVm == null) SimLoad_Click(sender, e);

            if (SimRunToggle.IsChecked == true)
            {
                _simTimer ??= new DispatcherTimer();
                _simTimer.Interval = TimeSpan.FromMilliseconds(_simVm.Sim.TickIntervalMs);
                _simTimer.Tick -= SimTimerTick;
                _simTimer.Tick += SimTimerTick;
                _simTimer.Start();
                SimRunToggle.Content = "Stop";
            }
            else
            {
                _simTimer?.Stop();
                SimRunToggle.Content = "Run";
            }
        }

        private void SimTimerTick(object sender, EventArgs e) => _simVm?.Tick();

        // ================= live monitor (real hardware over serial) =================

        private void MonitorStart_Click(object sender, RoutedEventArgs e)
        {
            if (PortCombo.SelectedItem == null) { MessageBox.Show("Select a COM port first."); return; }
            _monitor.Start(PortCombo.SelectedItem.ToString());
            Log("Monitor started on " + PortCombo.SelectedItem + ". Make sure nothing else (e.g. the Arduino IDE) has the port open.");
        }

        private void MonitorStop_Click(object sender, RoutedEventArgs e)
        {
            _monitor.Stop();
            Log("Monitor stopped.");
        }

        private void OnMonitorState(MonitorState st)
        {
            Dispatcher.Invoke(() =>
            {
                MonitorBox.AppendText($"I:{BoolStr(st.I)}  Q:{BoolStr(st.Q)}  M:{BoolStr(st.M)}  T:{BoolStr(st.T)}  C:{BoolStr(st.C)}{Environment.NewLine}");
                if (MonitorBox.Text.Length > 20000) MonitorBox.Text = MonitorBox.Text.Substring(MonitorBox.Text.Length - 10000);
                MonitorBox.ScrollToEnd();
            });
        }

        private void OnMonitorError(string msg) => Dispatcher.Invoke(() => Log("Monitor error: " + msg));

        private static string BoolStr(bool[] arr) => string.Concat(arr.Select(b => b ? '1' : '0'));

        // ================= ports / board =================

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            var ports = ArduinoCliService.ListSerialPorts();
            PortCombo.ItemsSource = ports;
            if (ports.Length > 0) PortCombo.SelectedIndex = 0;
        }

        // ================= build / upload =================

        private async void BuildOnly_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(_buildDir);
            string fqbn = BoardCombo.Text;
            string code = CodeGenerator.Generate(_program, "LadderSketch");
            Log("Compiling with FQBN " + fqbn + " ...");
            var (success, log) = await _cli.BuildOnly(code, "LadderSketch", fqbn, _buildDir);
            Log(log);
            Log(success ? "Compile OK." : "Compile FAILED.");
        }

        private async void BuildUpload_Click(object sender, RoutedEventArgs e)
        {
            if (PortCombo.SelectedItem == null)
            {
                MessageBox.Show("Select a COM port first (plug in the Arduino, then Refresh Ports).");
                return;
            }
            if (_monitor.IsOpen)
            {
                Log("Stopping the live monitor so the port is free for upload...");
                _monitor.Stop();
            }

            Directory.CreateDirectory(_buildDir);
            string fqbn = BoardCombo.Text;
            string port = PortCombo.SelectedItem.ToString();
            string code = CodeGenerator.Generate(_program, "LadderSketch");

            Log($"Building and uploading to {port} with FQBN {fqbn} ...");
            var (success, log) = await _cli.BuildAndUpload(code, "LadderSketch", fqbn, port, _buildDir);
            Log(log);
            Log(success ? "Upload OK." : "Upload FAILED.");
        }

        private void ExportIno_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Arduino sketch (*.ino)|*.ino", FileName = "LadderSketch.ino" };
            if (dlg.ShowDialog() == true)
            {
                string code = CodeGenerator.Generate(_program, Path.GetFileNameWithoutExtension(dlg.FileName));
                File.WriteAllText(dlg.FileName, code);
                Log("Exported sketch to " + dlg.FileName);
            }
        }

        // ================= project save / load =================

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Ladder project (*.json)|*.json", FileName = "LadderProject.json" };
            if (dlg.ShowDialog() == true)
            {
                ProjectFileService.Save(_program, dlg.FileName);
                Log("Saved project to " + dlg.FileName);
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Ladder project (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                _undoStack.Clear();
                _redoStack.Clear();
                RestoreProgram(ProjectFileService.Load(dlg.FileName));
                Log("Loaded project from " + dlg.FileName);
            }
        }

        // ================= misc =================

        private void Log(string text)
        {
            LogBox.AppendText(text + Environment.NewLine);
            LogBox.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            _monitor.Stop();
            _simTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
