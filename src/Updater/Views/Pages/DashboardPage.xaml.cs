using Updater.ViewModels.Pages;

using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Updater.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        private readonly INavigationService _navigationService;

        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel, INavigationService navigationService)
        {
            ViewModel = viewModel;
            _navigationService = navigationService;
            DataContext = this;

            InitializeComponent();
        }
    }
}
