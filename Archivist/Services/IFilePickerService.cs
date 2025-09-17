using System.Threading.Tasks;
using Windows.Storage;

namespace Archivist.Services
{
    public interface IFilePickerService
    {
        Task<StorageFolder?> PickFolderAsync();
    }
}
