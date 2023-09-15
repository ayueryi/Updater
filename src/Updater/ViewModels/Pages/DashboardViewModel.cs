using Updater.Views.Pages;

using Wpf.Ui;

namespace Updater.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        public DashboardViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        [RelayCommand]
        private void OnCarkClick()
        {
            _ = _navigationService.Navigate(typeof(ReleasePage));
        }
    }
}
