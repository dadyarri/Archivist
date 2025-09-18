using System;

namespace Archivist.Services
{
    public interface INavigationService
    {
        void NavigateTo(Type pageType);
    }
}