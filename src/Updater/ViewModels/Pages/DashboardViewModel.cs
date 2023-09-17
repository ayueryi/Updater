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

            Utils.Generic.GetVersion(@"C:\\Program Files\\dotnet\\dotnet.exe",
                                     out string name,
                                     out string version,
                                     out string file_version);

            SdkVersion = version;
        }

        [RelayCommand]
        private void OnCarkClick()
        {
            _ = _navigationService.Navigate(typeof(ReleasePage));
        }

        [ObservableProperty] private string _sdkVersion;
    }
}
