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
                    process?.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
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
