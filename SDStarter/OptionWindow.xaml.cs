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
using Path = System.IO.Path;

namespace SDStarter
{
    /// <summary>
    /// OptionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class OptionWindow : Window
    {
        public OptionWindow(string environsDirName, string id)
        {
            InitializeComponent();

            this.EnvironsDirName = environsDirName;
            this.Id = id;

            gpulist = new List<string>()
            {
                "",
                "0",
                "1",
                "2",
                "3",
            };
        }

        public string Id { get; set; }
        public string EnvironsDirName { get; set; }

        private JsonMemory config = new JsonMemory();

        public List<string> gpulist;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            combo_gpu.ItemsSource = gpulist;

            var configPath = Path.Combine(EnvironsDirName, Id, "config.data");
            config = new JsonMemory(configPath, false);

            text_name.Text = config.Get<string>("config", "name") ?? "unknown";
            check_api.IsChecked = config.Get<bool>("param", "api", false);
            combo_gpu.Text = config.Get<string>("param", "gpu") ?? "";
            check_safe_unpickle.IsChecked = config.Get<bool>("param", "safe_unpickle", true);

            UpdateParam();
        }

        private void button_ok_Click(object sender, RoutedEventArgs e)
        {
            config.Set("config", "name", text_name.Text);
            config.Set("param", "api", check_api.IsChecked);
            config.Set("param", "gpu", combo_gpu.Text);
            config.Set("param", "safe_unpickle", check_safe_unpickle.IsChecked);

            config.Save();

            this.Close();
        }

        private void button_cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UpdateParam()
        {
            var param = "";
            var env = "";
            if (check_api.IsChecked == true)
            {
                param += "--api ";
            }
            if (check_safe_unpickle.IsChecked == false)
            {
                param += "--disable-safe-unpickle ";
            }

            if (!string.IsNullOrWhiteSpace(combo_gpu.Text))
            {
                env += $"CUDA_VISIBLE_DEVICES={combo_gpu.Text} ";
            }

            text_param.Text = param + " / " + env;
        }

        private void text_userparam_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateParam();
        }

        private void combo_gpu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParam();
        }

        private void check_api_Checked(object sender, RoutedEventArgs e)
        {
            UpdateParam();
        }

        private void text_userparam_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            UpdateParam();
        }

        private void check_safe_unpickle_Checked(object sender, RoutedEventArgs e)
        {
            UpdateParam();
        }

        private void combo_gpu_Unselected(object sender, RoutedEventArgs e)
        {

            UpdateParam();
        }

        private void combo_gpu_DropDownClosed(object sender, EventArgs e)
        {
            UpdateParam();
        }
    }
}
