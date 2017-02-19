using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;

namespace UniversalPlayer.ViewModels
{
    public class LoadingViewModel
    {
        public List<SongViewModel> Songs { get; set; } = new List<SongViewModel>();

        public async Task LoadInitialMusic()
        {
            StorageLibrary music = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Music);
            QueryOptions query = new QueryOptions(CommonFileQuery.OrderByDate, new string[] { ".mp3" });
            query.FolderDepth = FolderDepth.Deep;

            var files = await KnownFolders.MusicLibrary.CreateFileQueryWithOptions(query).GetFilesAsync();
            foreach (StorageFile file in files)
            {
                Songs.Add(new SongViewModel
                {
                    FileHandle = file,
                    Properties = await file.Properties.GetMusicPropertiesAsync()
                });
            }
        }
    }
}
