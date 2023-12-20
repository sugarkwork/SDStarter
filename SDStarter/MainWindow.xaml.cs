using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace SDStarter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            LoadItems();

            configs = new JsonMemory("config.json", true);

            var count = configs.Get<int>("test", "boot_count");
            configs.Set("test", "boot_count", count + 1);

            environsDirName = configs.Get<string>("config", "environs") ?? "environs";
        }

        private string environsDirName = "environs";
        private JsonMemory configs;

        private void LoadItems()
        {
            listBoxItems.BeginInit();
            try
            {
                var items = new List<Item>();

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

                    string name = config.Get<string>("config", "name") ?? "unknown";

                    items.Add(new Item(name: name, summary: Path.GetFileNameWithoutExtension(dir), icon: "", status: ""));
                }

                listBoxItems.ItemsSource = items;
            }
            finally
            {
                listBoxItems.EndInit();
            }
        }

        private void ListBoxItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }


        private void NewItemCreateItem_Click(object sender, RoutedEventArgs e)
        {
            var newitem = new NewGitClone();
            newitem.ShowDialog();

            LoadItems();
        }

        private void UpdateWebUIBat(string basedir)
        {
            var configPath = Path.Combine(basedir, "config.data");
            var config = new JsonMemory(configPath, false);

            var webuiPath = Path.Combine(basedir, "webui");
            var webuiUserPath = Path.Combine(webuiPath, "webui-user.bat");
            var webuiUserBasePath = Path.Combine(webuiPath, "webui-user_base.bat");

            string[] lines = new string[0];

            if (!File.Exists(webuiUserBasePath) && File.Exists(webuiUserPath))
            {
                lines = File.ReadAllLines(webuiUserPath);
            }
            else
            {
                lines = File.ReadAllLines(webuiUserBasePath);
            }

            List<string> newlines = new List<string>();


            var parstr = "";
            var cudstr = "";

            var param = new Dictionary<string, List<string>>();

            param["COMMANDLINE_ARGS"] = new List<string>();
            param["CUDA_VISIBLE_DEVICES"] = new List<string>();

            if (config.Get<bool>("param", "api", false) == true)
            {
                param["COMMANDLINE_ARGS"].Add("--api");
                parstr += "--api ";
            }
            if (config.Get<bool>("param", "safe_unpickle", true) == false)
            {
                param["COMMANDLINE_ARGS"].Add("--disable-safe-unpickle ");
                parstr += "--disable-safe-unpickle ";
            }

            var gpuid = config.Get<string>("param", "gpu") ?? "";
            if (!string.IsNullOrWhiteSpace(gpuid))
            {
                param["CUDA_VISIBLE_DEVICES"].Add($"{gpuid}");
                cudstr += gpuid;
            }

            foreach (var line in lines) {
                var tline = line.Trim();
                if (string.IsNullOrWhiteSpace(tline))
                {
                    newlines.Add("");
                    continue;
                }
            }

            // TODO: 
            newlines.Clear();
            newlines.Add("@echo off");
            newlines.Add("");
            newlines.Add($"set COMMANDLINE_ARGS={parstr}");
            newlines.Add($"set CUDA_VISIBLE_DEVICES={cudstr}");
            newlines.Add("");
            newlines.Add("call webui.bat");
            newlines.Add("");

            File.WriteAllLines(webuiUserPath, newlines);
        }

        private void StartItem_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxItems.SelectedItem == null)
            {
                return;
            }
            var item = listBoxItems.SelectedItem as Item;
            if (item != null)
            {
                var runBatch = "run.bat";
                var basePath = Path.GetFullPath(Path.Combine(environsDirName, item.Summary));
                var runPath = Path.Combine(basePath, runBatch);

                UpdateWebUIBat(basePath);

                if (!Directory.Exists(basePath))
                {
                    Console.WriteLine($"directory not found: {basePath}");
                    return;
                }
                if (!File.Exists(runPath))
                {
                    Console.WriteLine($"file not found: {runPath}");
                    return;
                }
                try
                {
                    ProcessStartInfo psi = new()
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C \"{runBatch}\"",
                        WorkingDirectory = basePath,
                        CreateNoWindow = false,
                        UseShellExecute = true,
                    };

                    Console.WriteLine(psi.WorkingDirectory);
                    Console.WriteLine(psi.FileName + " " + psi.Arguments);

                    using Process? process = Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxItems.SelectedItem == null)
            {
                return;
            }
            var item = listBoxItems.SelectedItem as Item;
            if (item != null)
            {
                var outputsPath = Path.GetFullPath(Path.Combine(environsDirName, item.Summary, "webui", "outputs"));
                if (!Directory.Exists(outputsPath))
                {
                    Directory.CreateDirectory(outputsPath);
                }
                Process.Start("explorer.exe", outputsPath);
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxItems.SelectedItem == null)
            {
                return;
            }
            var item = listBoxItems.SelectedItem as Item;
            if (item != null)
            {

                OptionWindow optionWindow = new OptionWindow(environsDirName, item.Summary);
                optionWindow.ShowDialog();
                LoadItems();
            }
        }

        private void GlobalSettingItem_Click(object sender, RoutedEventArgs e)
        {
            EnvSettings envSettings = new EnvSettings();
            envSettings.ShowDialog();
            LoadItems();
        }
    }

    public class Item
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Icon { get; set; } // 画像のパスかURI
        public string Status { get; set; }

        public Item()
        {
            Name = string.Empty;
            Summary = string.Empty;
            Icon = string.Empty;
            Status = string.Empty;
        }

        [JsonConstructor]
        public Item(string name, string summary, string icon, string status)
        {
            Name = name;
            Summary = summary;
            Icon = icon;
            Status = status;
        }
    }

}
