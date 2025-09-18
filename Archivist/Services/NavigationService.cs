using Microsoft.UI.Xaml.Controls;
using System;

namespace Archivist.Services
{
    public class NavigationService : INavigationService
    {
        private readonly Frame _frame;

        public NavigationService(Frame frame)
        {
            _frame = frame;
        }

        public void NavigateTo(Type pageType)
        {
            _frame.Navigate(pageType);
        }
    }
}