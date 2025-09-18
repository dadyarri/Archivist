using Archivist.Services;
using Archivist.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Archivist.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string Header { get; set; } = string.Empty;

        private readonly INavigationService _navigationService;

        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        [RelayCommand]
        private void Navigate(string? tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            switch (tag)
            {
                case "CreateSummary":
                    _navigationService.NavigateTo(typeof(CreateSummaryPage));
                    Header = "Создать саммари";
                    break;
                case "ListSummaries":
                    _navigationService.NavigateTo(typeof(ListSummariesPage));
                    Header = "Список саммари";
                    break;
                case "Settings":
                    _navigationService.NavigateTo(typeof(SettingsPage));
                    Header = "Настройки";
                    break;
            }
        }
    }
}
