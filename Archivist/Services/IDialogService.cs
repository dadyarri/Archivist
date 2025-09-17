using System.Threading.Tasks;

namespace Archivist.Services
{
    public interface IDialogService
    {
        Task ShowDialogAsync(string title, string content);
    }
}