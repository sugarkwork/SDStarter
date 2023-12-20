using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace SDStarter
{
    /// <summary>
    /// EnvSettings.xaml の相互作用ロジック
    /// </summary>
    public partial class EnvSettings : Window
    {
        public EnvSettings()
        {
            InitializeComponent();
        }

        private JsonMemory appconf = new JsonMemory();
        private string environsDirName = "environs";

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var appconfPath = Path.GetFullPath(Path.Combine(".", "config.json"));
            appconf = new JsonMemory(appconfPath, true);
            environsDirName = appconf.Get<string>("config", "environs") ?? "environs";

            text_modelpath.Text = Path.GetFullPath(appconf.Get("env", "model_path", "models") ?? "models");
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            var modelpath = Path.GetFullPath(text_modelpath.Text);
            appconf.Set("env", "model_path", modelpath);

            StringBuilder sb = new StringBuilder();

            string modelPath = System.IO.Path.GetFullPath(appconf.Get("env", "model_path", "models") ?? "models");
            string sdmodelPath = System.IO.Path.Combine(modelPath, "Stable-diffusion");
            string checkpointPath = System.IO.Path.Combine(modelPath, "checkpoints");

            sb.AppendLine($"rmdir \"{checkpointPath}\"");
            sb.AppendLine($"del \"{checkpointPath}\"");
            sb.AppendLine($"mklink /D \"{checkpointPath}\" \"{sdmodelPath}\"");

            string loraPath = System.IO.Path.Combine(modelPath, "Lora");
            string lorasPath = System.IO.Path.Combine(modelPath, "Loras");

            sb.AppendLine($"rmdir \"{lorasPath}\"");
            sb.AppendLine($"del \"{lorasPath}\"");
            sb.AppendLine($"mklink /D \"{lorasPath}\" \"{loraPath}\"");

            environsDirName = appconf.Get<string>("config", "environs") ?? "environs";

            var environPath = Path.GetFullPath(environsDirName);
            if (!Directory.Exists(environPath))
            {
                Directory.CreateDirectory(environPath);
            }
            foreach (var dir in Directory.GetDirectories(environPath))
            {
                var configPath = Path.Combine(dir, "config.data");
                var config = new JsonMemory(configPath, true);

                if (!config.Get<bool>("config", "complete"))
                {
                    continue;
                }

                string webuimodelPath = System.IO.Path.Combine(environsDirName, dir, "webui", "models");
                sb.AppendLine($"rmdir \"{webuimodelPath}\"");
                sb.AppendLine($"del \"{webuimodelPath}\"");
                sb.AppendLine($"mklink /D \"{webuimodelPath}\" \"{modelPath}\"");
            }

            File.WriteAllText(
                "sudomklink.bat",
                "@echo off\r\nwhoami /priv | find \"SeDebugPrivilege\" > nul\r\nif %errorlevel% neq 0 (\r\n @powershell start-process %~0 -verb runas\r\n exit\r\n)\r\n\r\n" + sb.ToString());

            RunExternalProcess(System.IO.Path.GetFullPath("."), "cmd.exe", "/C sudomklink.bat", useShell: true);

            this.Close();
        }

        public StdString RunExternalProcess(string workingDirectory, string fileName, string arguments, string envDirectory = "", bool useShell = false)
        {
            var pathBackup = Environment.GetEnvironmentVariable("PATH");
            try
            {
                if (envDirectory.Length > 0)
                {
                    var newPath = $"{System.IO.Path.GetFullPath(envDirectory)};{Environment.GetEnvironmentVariable("PATH")}";
                    Environment.SetEnvironmentVariable("PATH", newPath);
                }

                var startinfo = new ProcessStartInfo
                {
                    WorkingDirectory = workingDirectory,
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = !useShell,
                    RedirectStandardError = !useShell,
                    UseShellExecute = useShell,
                    CreateNoWindow = true,
                };

                var process = new Process
                {
                    StartInfo = startinfo
                };

                process.Start();

                string result_out = string.Empty;
                string result_err = string.Empty;

                if (!useShell)
                {
                    result_out = process.StandardOutput.ReadToEnd();
                    result_err = process.StandardError.ReadToEnd();
                }

                process.WaitForExit();

                return new StdString(result_out, result_err);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", pathBackup);
            }
        }

    }
}