using System.Data.SqlTypes;
using System.IO;
using System.Windows.Controls;
using System.Xml;

using Updater.Utils;
using Updater.ViewModels.Pages;

using Wpf.Ui.Controls;

namespace Updater.Views.Pages
{
    /// <summary>
    /// ReleasePage.xaml 的交互逻辑
    /// </summary>
    public partial class ReleasePage : INavigableView<ReleaseViewModel>
    {
        public ReleaseViewModel ViewModel { get; }

        public ReleasePage(ReleaseViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            ViewModel.LoadConfig();

            var project_list = Generic.GetProjectList(ViewModel.ProjectUrl);
            this.CompildProjectComboBox.Items.Clear();
            foreach (var project in project_list)
            {
                this.CompildProjectComboBox.Items.Add(new ComboBoxItem() { Content = project });
            }

            this.CompildConfigComboBox.SelectedIndex = 0;
            this.CompildProjectComboBox.SelectedIndex = 0;
        }

        private void CleanOutPut(object sender, RoutedEventArgs e)
        => ViewModel.OutPut = string.Empty;

        private void ReleaseAddress(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.ProjectUrl)) return;

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            var result = dialog.ShowDialog();
            if (result is not DialogResult.OK) return;

            var suppix = ViewModel.CompildProject!.Content.ToString()!.Split("\\");
            var packpath = Path.Combine(dialog.SelectedPath, suppix.Last());
            packpath = packpath.Replace(".csproj", $"{ViewModel.CsprojVersion}.zip");
            ViewModel.PackPath = packpath;
        }

        private void ReleaseConfig(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.ProjectUrl)) return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "选择项目";
            ofd.InitialDirectory = @"C:\";
            ofd.Filter = "更新配置|*.xml";

            bool? result = ofd.ShowDialog() ?? false;
            if (result is false) return;

            if (File.Exists(ofd.FileName))
            {
                ViewModel.PackConfigPath = ofd.FileName;
                var content = File.ReadAllText(ofd.FileName);
                Generic.GetXmlVersion(content, out string version);
                ViewModel.PackConfigVersion = version;
            }
        }
    }
}
