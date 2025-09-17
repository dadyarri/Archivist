using Archivist.Services;
using Archivist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Archivist.Views
{
    /// <summary>
    /// Page for creating new summaries.
    /// </summary>
    public sealed partial class CreateSummaryPage : Page
    {
        private CreateSummaryViewModel ViewModel { get; }

        public CreateSummaryPage()
        {
            InitializeComponent();
            var serviceProvider = App.Current.Services;
            var filePickerService = serviceProvider.GetRequiredService<IFilePickerService>();
            var dialogService = serviceProvider.GetRequiredService<IDialogService>();
            ViewModel = new CreateSummaryViewModel(DispatcherQueue.GetForCurrentThread(), filePickerService, dialogService);
            DataContext = ViewModel;
        }
    }
}