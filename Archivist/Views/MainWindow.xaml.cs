using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Archivist.Views
{
    /// <summary>
    /// Main window with sidebar navigation for the Archivist application.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Set the default page
            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
            NavigateToPage("CreateSummary");
        }

        private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string? tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    NavigateToPage(tag);
                }
            }
        }

        private void NavigateToPage(string pageTag)
        {
            switch (pageTag)
            {
                case "CreateSummary":
                    MainNavigationView.Header = "Создать саммари";
                    ContentFrame.Navigate(typeof(Archivist.Views.CreateSummaryPage));
                    break;
                case "ListSummaries":
                    MainNavigationView.Header = "Список саммари";
                    ContentFrame.Navigate(typeof(Archivist.Views.ListSummariesPage));
                    break;
                case "Settings":
                    MainNavigationView.Header = "Настройки";
                    ContentFrame.Navigate(typeof(Archivist.Views.SettingsPage));
                    break;
            }
        }
    }
}
