using Archivist.Services;
using Archivist.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Archivist.Views
{
    /// <summary>
    /// Main window with sidebar navigation for the Archivist application.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();

            var navigationService = new NavigationService(ContentFrame);
            ViewModel = new MainViewModel(navigationService);
            //DataContext = ViewModel;

            // Set the default page
            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
            ViewModel.NavigateCommand.Execute((MainNavigationView.SelectedItem as NavigationViewItem)?.Tag?.ToString());
        }

        private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string? tag = item.Tag?.ToString();
                ViewModel.NavigateCommand.Execute(tag);
            }
        }
    }
}