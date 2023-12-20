using System;
using System.Collections.Generic;
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
using System.Net.Http;
using System.IO;
using System.Collections.Concurrent;
using System.Windows.Threading;
using System.IO.Compression;
using System.Security.Policy;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace SDStarter
{
    /// <summary>
    /// NewGitClone.xaml の相互作用ロジック
    /// </summary>
    public partial class NewGitClone : Window
    {
        public NewGitClone()
        {
            InitializeComponent();

            InitializePresets();

            InitializeComboBoxItems();

            InitializeTimer();
        }

        private DispatcherTimer timer = new DispatcherTimer();

        private string FinishKeyword = string.Empty;

        private void InitializeTimer()
        {
            timer.Interval = TimeSpan.FromSeconds(0.5);
            timer.Tick += Timer_Tick;
            timer.Start();

            FinishKeyword = $"_end_{new Random().Next()}_{new Random().Next()}";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (fetchingTask == null) { return; }

            if (logs.Count == 0) { return; }

            while (logs.Count > 0)
            {
                string? logline;
                logs.TryDequeue(out logline);
                if (string.IsNullOrWhiteSpace(logline)) { continue; }
                logTextBox.AppendText(logline.Trim() + "\n");

                if (logline != null && logline.Contains(FinishKeyword))
                {
                    logTextBox.AppendText("close!" + "\n");
                    Close();
                }
            }

            if (progressBar.Value < 100)
            {
                progressBar.Value++;
            }

            while (progressLog.Count > 0)
            {
                int value;
                progressLog.TryDequeue(out value);
                if (value >= 0)
                {
                    progressBar.Value = value;
                }
            }
        }

        private string baseKit = "https://github.com/AUTOMATIC1111/stable-diffusion-webui/releases/download/v1.0.0-pre/sd.webui.zip";

        private Dictionary<string, string> url_presets = new Dictionary<string, string>();
        private ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
        private ConcurrentQueue<int> progressLog = new ConcurrentQueue<int>();

        private void InitializePresets()
        {
            url_presets.Clear();
            url_presets["sd.webui"] = "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
            //url_presets["sd.next"] = "https://github.com/vladmandic/automatic";
            //url_presets["lama-cleaner"] = "https://github.com/Sanster/lama-cleaner";
        }

        private void InitializeComboBoxItems()
        {
            presetComboBox.BeginInit();
            presetComboBox.Items.Clear();
            foreach (var key in url_presets.Keys)
            {
                presetComboBox.Items.Add(key);
            }
            presetComboBox.EndInit();
        }

        private Task? fetchingTask = null;

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (fetchingTask != null)
            {
                return;
            }
            fetchButton.IsEnabled = false;
            logs.Clear();

            Uri uri = new Uri(urlTextBox.Text.Trim());

            string lastSegment = uri.Segments[uri.Segments.Length - 1];

            fetchingTask = FetchingBase(lastSegment);
        }

        private async Task FetchingBase(string lastSegment)
        {
            try
            {
                await Fetching(lastSegment);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private async Task Fetching(string dirName)
        {
            await LogAsync("*** start ***", 5);
            FileInfo myFile = new FileInfo("kit.zip");


            await LogAsync("*** check base kit ***", 10);
            if (!myFile.Exists)
            {
                await LogAsync($"download target : {myFile.FullName}");
                await DownloadFileAsync(baseKit, "kit.zip");
            }
            else
            {
                await LogAsync("download ok");
            }

            var destName = string.Empty;
            var destPath = string.Empty;

            for (int i = 0; i < 100; i++) {
                destName = $"{i}"; // DateTime.Now.ToString("yyyyMMdd_HHmmss") // {dirName}_
                destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine("environs", destName));
                if(!Directory.Exists(destPath))
                {
                    break;
                }
            }

            await LogAsync("*** check directory ***", 15);
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            var appconfPath = Path.GetFullPath(Path.Combine(".", "config.json"));
            var configPath = Path.Combine(destPath, "config.data");

            JsonMemory appconf = new JsonMemory(appconfPath, true);
            JsonMemory config = new JsonMemory(configPath, true);

            var title = titleTextBox.Text.Trim();
            var url = urlTextBox.Text.Trim();

            config.Set("config", "name", title);
            config.Set("config", "url", url);
            config.Set("config", "dest", destName);
            config.Set("config", "datetime", DateTime.Now);
            config.Set("config", "complete", false);


            await LogAsync("*** unzip base kit ***", 20);

            await Unzip("kit.zip", destPath);

            string gitconfigPath = System.IO.Path.Combine(destPath, "webui", ".git", "config");
            string gitPath = System.IO.Path.Combine(destPath, "system", "git", "bin");
            string webuiPath = System.IO.Path.Combine(destPath, "webui");


            await LogAsync("*** check repository ***", 25);

            if (!await CheckRepository(gitconfigPath))
            {
                Directory.Delete(webuiPath, true);
                await LogAsync("*** git clone ***", 30);
                await RunExternalProcessAsync(destPath, "git", $"clone {url} webui", gitPath);
            }
            if (!await CheckRepository(gitconfigPath))
            {
                await LogAsync("git clone error");
                return;
            }
            // await RunExternalProcessAsync(webuiPath, "git", "fetch", gitPath);
            await RunExternalProcessAsync(webuiPath, "git", "pull", gitPath);

            var gitLogs = await RunExternalProcessAsync(webuiPath, "git", "log", gitPath);

            string[] autoinstalls = new string[]
            {
                "https://github.com/DominikDoom/a1111-sd-webui-tagcomplete",
                "https://github.com/Bing-su/adetailer",
                "https://github.com/Mikubill/sd-webui-controlnet",
                "https://github.com/Physton/sd-webui-prompt-all-in-one",
                "https://github.com/zanllp/sd-webui-infinite-image-browsing.git"
            };

            string extensionDir = System.IO.Path.Combine(webuiPath, "extensions");
            if(!Directory.Exists(extensionDir))
            {
                Directory.CreateDirectory(extensionDir);
            }
            foreach (string autoinstall in autoinstalls)
            {
                await RunExternalProcessAsync(extensionDir, "git", $"clone {autoinstall}", gitPath);
            }

            await LogAsync("*** check models ***", 35);

            string modelPath = System.IO.Path.GetFullPath(appconf.Get<string>("env", "model_path") ?? "models");
            string sdmodelPath = System.IO.Path.Combine(modelPath, "Stable-diffusion");
            string checkpointPath = System.IO.Path.Combine(modelPath, "checkpoints");
            string system32 = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(@"%windir%\System32"));

            await LogAsync($"model dir: {modelPath}");
            if (!Directory.Exists(sdmodelPath))
            {
                await LogAsync($"create dir: {sdmodelPath}");
                Directory.CreateDirectory(sdmodelPath);
            }

            StringBuilder sb = new StringBuilder();

            if (!Directory.Exists(checkpointPath))
            {
                sb.AppendLine($"mklink /D \"{checkpointPath}\" \"{sdmodelPath}\"");
            }

            string loraPath = System.IO.Path.Combine(modelPath, "Lora");
            string lorasPath = System.IO.Path.Combine(modelPath, "Loras");

            if (!Directory.Exists(loraPath))
            {
                Directory.CreateDirectory(loraPath);
            }
            if (!Directory.Exists(lorasPath))
            {
                sb.AppendLine($"mklink /D \"{lorasPath}\" \"{loraPath}\"");
            }

            string[] models = Directory.GetFiles(sdmodelPath);
            await LogAsync($"model count: {models.Length}");
            if (models.Length <= 0)
            {
                await LogAsync($"download test model: illust base model");
                await DownloadFileAsync(
                    "https://huggingface.co/sugarknight/test_illust/resolve/main/ggbb30x.safetensors?download=true",
                    System.IO.Path.Combine(sdmodelPath, "gb_illust.safetensors"));
                await LogAsync($"download test model: bbgg30x.safetensors");
                await DownloadFileAsync(
                    "https://huggingface.co/sugarknight/test_real/resolve/main/bbgg30x.safetensors?download=true",
                    System.IO.Path.Combine(sdmodelPath, "gb_real.safetensors"));
            }
        
            string vaePath = System.IO.Path.Combine(modelPath, "VAE");
            if(!Directory.Exists(vaePath))
            {
                Directory.CreateDirectory (vaePath);
            }
            string[] vaes = Directory.GetFiles(vaePath);
            if (vaes.Length <= 0)
            {
                await LogAsync($"download test vae: vae-ft-mse-840000-ema-pruned.safetensors");
                await DownloadFileAsync(
                    "https://huggingface.co/stabilityai/sd-vae-ft-mse-original/resolve/main/vae-ft-mse-840000-ema-pruned.safetensors?download=true",
                    System.IO.Path.Combine(vaePath, "vae-ft-mse-840000-ema-pruned.safetensors"));
            }

            string webuiModelPath = System.IO.Path.Combine(webuiPath, "models");
            if (Directory.Exists(webuiModelPath))
            {
                await LogAsync("delete models dir");
                Directory.Delete(webuiModelPath, true);
            }

            sb.AppendLine($"mklink /D \"{webuiModelPath}\" \"{modelPath}\"");


            /*
            string torchPath = System.IO.Path.Combine(destPath, @"system\python\Lib\site-packages\torch\");
            string torchlibPath = System.IO.Path.Combine(torchPath, "lib");
            if (!Directory.Exists(torchPath))
            {
                Directory.CreateDirectory(torchPath);
            }

            string sharePath = System.IO.Path.GetFullPath("share");
            string sharetorchlibPath = System.IO.Path.Combine(sharePath, "torch_lib");
            if (!Directory.Exists(sharetorchlibPath))
            {
                Directory.CreateDirectory (sharetorchlibPath);
            }

            sb.AppendLine($"mklink /D \"{torchlibPath}\" \"{sharetorchlibPath}\"");
            */

            string controlnetPath = System.IO.Path.Combine(modelPath, "ControlNet");
            if (!Directory.Exists(controlnetPath))
            {
                Directory.CreateDirectory(controlnetPath);
            }
            string[] strings = await File.ReadAllLinesAsync("controlnet.txt");
            foreach (string s in strings)
            {
                Uri uri = new Uri(s.Trim());
                string lastSegment = uri.Segments[uri.Segments.Length - 1];
                string ctnModelPath = System.IO.Path.Combine(controlnetPath, lastSegment);
                if (!File.Exists(ctnModelPath))
                {
                    await LogAsync($"download: {lastSegment}");
                    await DownloadFileAsync(s, ctnModelPath);
                }
            }

            string webuiUserPath = System.IO.Path.Combine(webuiPath, "webui-user.bat");
            if (!File.Exists(webuiUserPath))
            {
                await File.WriteAllTextAsync(webuiUserPath, "@echo off\r\n\r\nset PYTHON=\r\nset GIT=\r\nset VENV_DIR=\r\nset COMMANDLINE_ARGS=\r\n\r\ncall webui.bat\r\n");
            }
            string webuiUserBasePath = System.IO.Path.Combine(webuiPath, "webui-user_base.bat");
            if (!File.Exists(webuiUserBasePath))
            {
                await File.WriteAllTextAsync(webuiUserBasePath, "@echo off\r\n\r\nset PYTHON=\r\nset GIT=\r\nset VENV_DIR=\r\nset COMMANDLINE_ARGS=\r\n\r\ncall webui.bat\r\n");
            }

            string webuiconfigPath = System.IO.Path.Combine(webuiPath, "config.json");
            if (!File.Exists(webuiconfigPath))
            {
                await File.WriteAllTextAsync(webuiconfigPath, "{\r\n    \"sd_model_checkpoint\": \"gb_illust.safetensors [c936cb33ed]\",\r\n    \"CLIP_stop_at_last_layers\": 2,\r\n    \"sd_vae\": \"vae-ft-mse-840000-ema-pruned.safetensors\"\r\n}");
            }

            string webuiconfig2Path = System.IO.Path.Combine(webuiPath, "ui-config.json");
            if (!File.Exists(webuiconfig2Path))
            {
                await File.WriteAllTextAsync(webuiconfig2Path, "{\r\n    \"txt2img/Negative prompt/value\": \"(worst quality, low quality:1.3),monochrome,\",\r\n    \"img2img/Negative prompt/value\": \"(worst quality, low quality:1.3),monochrome,\"\r\n}");
            }

            await File.WriteAllTextAsync(
                "sudomklink.bat",
                "@echo off\r\nwhoami /priv | find \"SeDebugPrivilege\" > nul\r\nif %errorlevel% neq 0 (\r\n @powershell start-process %~0 -verb runas\r\n exit\r\n)\r\n\r\n" + sb.ToString());

            await RunExternalProcessAsync(System.IO.Path.GetFullPath("."), "cmd.exe", "/C sudomklink.bat", useShell: true);

            config.Set("config", "complete", true);
            Log(FinishKeyword, 100);
        }

        public async Task<StdString> RunExternalProcessAsync(string workingDirectory, string fileName, string arguments, string envDirectory = "", bool useShell = false)
        {
            var pathBackup = Environment.GetEnvironmentVariable("PATH");
            //await LogAsync(pathBackup);
            try
            {
                if (envDirectory.Length > 0)
                {
                    var newPath = $"{System.IO.Path.GetFullPath(envDirectory)};{Environment.GetEnvironmentVariable("PATH")}";
                    Environment.SetEnvironmentVariable("PATH", newPath);
                }

                await LogAsync($"subprocess: {fileName} {arguments}");

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


                await LogAsync("subprocess start");
                process.Start();

                string result_out = string.Empty;
                string result_err = string.Empty;

                if (!useShell)
                {
                    result_out = await process.StandardOutput.ReadToEndAsync();
                    result_err = await process.StandardError.ReadToEndAsync();
                }

                await process.WaitForExitAsync();

                await LogAsync("subprocess end");

                return new StdString(result_out, result_err);

            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", pathBackup);
            }
        }

        private async Task<bool> CheckRepository(string gitconfigPath)
        {
            if (File.Exists(gitconfigPath))
            {
                string content = await File.ReadAllTextAsync(gitconfigPath);
                if (content.ToLower().Contains(urlTextBox.Text.ToLower().Trim()))
                {
                    Log("repository ok");
                    return true;
                }
            }
            Log("repository ng");
            return false;
        }

        private async Task LogAsync(string? message, int progress=-1)
        {
            if (message == null)
            {
                return;
            }
            await Task.Run(() =>
            {
                Log(message, progress);
            });
        }

        private void Log(string message, int progress = -1)
        {
            logs.Enqueue($"{DateTime.Now} : {message}");
            if(progress > 0)
            {
                if(progress <= 0)
                {
                    progress = 0;
                }
                if(progress > 100)
                {
                    progress = 100;
                }
                progressLog.Enqueue(progress);
            }
        }

        private async Task Unzip(string zipPath, string extractPath)
        {
            await Task.Run(() =>
            {
                Log($"unzip target: {zipPath}");
                Log($"unzip dest: {extractPath}");
                // Zip ファイルを開く
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    // Zip ファイル内の各エントリ（ファイル）に対してループ
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // 解凍先のファイルパスを作成
                        string destinationPath = System.IO.Path.Combine(extractPath, entry.FullName);

                        // ディレクトリの場合はスキップ（ファイルのみ解凍）
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            var dirName = System.IO.Path.GetDirectoryName(destinationPath);
                            if (dirName != null)
                            {
                                Directory.CreateDirectory(dirName);
                            }
                            else
                            {
                                Log($"unzip error: GetDirectoryName {destinationPath}");
                            }

                            // ファイルを解凍
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                Log("unzip done.");
            });
        }


        private static readonly HttpClient client = new HttpClient();

        // URLからファイルを非同期にダウンロードして保存するメソッド
        public async Task DownloadFileAsync(string fileUrl, string localPath)
        {
            try
            {
                // HTTPリクエストを送信し、レスポンスを取得
                using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // ストリームを使用してレスポンスの内容を読み込む
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        // ファイルにデータを書き込む
                        await stream.CopyToAsync(fileStream);
                    }
                }

                await LogAsync($"DownloadFileAsync Complete: {localPath}");
            }
            catch (Exception ex)
            {
                await LogAsync($"DownloadFileAsync Error: {ex.Message}");
            }
        }

        public async Task<string> GetFileAsync(string fileUrl)
        {
            return await GetFileAsync(fileUrl, Encoding.UTF8);
        }
        public async Task<string> GetFileAsync(string fileUrl, Encoding encoding)
        {
            try
            {
                using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        TextReader reader = new StreamReader(stream, encoding);
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                await LogAsync($"GetFileAsync Error: {ex.Message}");
            }
            finally
            {
                await LogAsync($"GetFileAsync Complete");
            }
            return string.Empty;
        }

        private void presetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = presetComboBox.SelectedItem as string;
            Console.WriteLine(selectedItem);
            string keyName = !string.IsNullOrEmpty(selectedItem) ? selectedItem.ToString() : string.Empty;
            Console.WriteLine(keyName);
            titleTextBox.Text = url_presets.ContainsKey(keyName) ? keyName : string.Empty;
            urlTextBox.Text = url_presets.ContainsKey(keyName) ? url_presets[keyName] : string.Empty;
            Console.WriteLine(urlTextBox.Text);
        }

        private void toggleAdvancedSettings_Checked(object sender, RoutedEventArgs e)
        {

        }
    }

    public class StdString
    {
        public string Out { get; set; }
        public string Err { get; set; }
        public StdString()
        {
            Out = string.Empty;
            Err = string.Empty;
        }

        public StdString(string stdout, string stderr)
        {
            Out = stdout;
            Err = stderr;
        }
    }
}