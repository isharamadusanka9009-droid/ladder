using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace LadderToArduino.Services
{
    // Thin wrapper around the "arduino-cli" command line tool.
    // Install it from https://arduino.github.io/arduino-cli/ and make sure it's on PATH,
    // or set ArduinoCliExePath to the full path of arduino-cli.exe.
    public class ArduinoCliService
    {
        public string ArduinoCliExePath { get; set; } = "arduino-cli";

        public static string[] ListSerialPorts()
        {
            try { return SerialPort.GetPortNames(); }
            catch { return Array.Empty<string>(); }
        }

        // Writes the sketch text to <sketchFolder>/<sketchName>.ino (arduino-cli requires
        // the folder name to match the .ino file name), then compiles and uploads it.
        public async Task<(bool success, string log)> BuildAndUpload(
            string sketchCode, string sketchName, string fqbn, string comPort, string workingDir)
        {
            var log = new StringBuilder();
            try
            {
                string sketchFolder = Path.Combine(workingDir, sketchName);
                Directory.CreateDirectory(sketchFolder);
                string inoPath = Path.Combine(sketchFolder, sketchName + ".ino");
                File.WriteAllText(inoPath, sketchCode);
                log.AppendLine($"Wrote sketch: {inoPath}");

                var compile = await RunCli($"compile --fqbn {fqbn} \"{sketchFolder}\"");
                log.AppendLine("--- compile ---");
                log.AppendLine(compile.output);
                if (!compile.success)
                {
                    log.AppendLine("Compile failed - see output above.");
                    return (false, log.ToString());
                }

                var upload = await RunCli($"upload -p {comPort} --fqbn {fqbn} \"{sketchFolder}\"");
                log.AppendLine("--- upload ---");
                log.AppendLine(upload.output);
                if (!upload.success)
                {
                    log.AppendLine("Upload failed - see output above.");
                    return (false, log.ToString());
                }

                log.AppendLine("Done. Program uploaded successfully.");
                return (true, log.ToString());
            }
            catch (Exception ex)
            {
                log.AppendLine("Exception: " + ex.Message);
                return (false, log.ToString());
            }
        }

        // Just compiles, without uploading - useful to sanity check code before wiring up a board.
        public async Task<(bool success, string log)> BuildOnly(string sketchCode, string sketchName, string fqbn, string workingDir)
        {
            var log = new StringBuilder();
            string sketchFolder = Path.Combine(workingDir, sketchName);
            Directory.CreateDirectory(sketchFolder);
            string inoPath = Path.Combine(sketchFolder, sketchName + ".ino");
            File.WriteAllText(inoPath, sketchCode);

            var compile = await RunCli($"compile --fqbn {fqbn} \"{sketchFolder}\"");
            log.AppendLine(compile.output);
            return (compile.success, log.ToString());
        }

        public async Task<bool> EnsureCoreInstalled(string coreId)
        {
            var result = await RunCli($"core install {coreId}");
            return result.success;
        }

        private Task<(bool success, string output)> RunCli(string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ArduinoCliExePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var proc = Process.Start(psi);
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    string combined = stdout + (string.IsNullOrWhiteSpace(stderr) ? "" : "\n" + stderr);
                    return (proc.ExitCode == 0, combined);
                }
                catch (Exception ex)
                {
                    return (false, "Could not launch arduino-cli: " + ex.Message +
                        "\nMake sure arduino-cli is installed and on PATH, or set the full path in Settings.");
                }
            });
        }
    }
}
