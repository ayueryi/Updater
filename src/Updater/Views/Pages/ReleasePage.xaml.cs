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
        }
    }
}
