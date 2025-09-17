using CommunityToolkit.Mvvm.ComponentModel;

namespace Archivist.Models
{
    public partial class AudioFileItem : ObservableObject
    {
        [ObservableProperty]
        public partial string? FileName { get; set; }

        [ObservableProperty]
        public partial string? FilePath { get; set; }

        [ObservableProperty]
        public partial string? CharacterName { get; set; }
    }
}