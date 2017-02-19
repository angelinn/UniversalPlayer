using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace UniversalPlayer.ViewModels
{
    public class LoadingViewModel
    {
        public List<StorageFile> Files { get; set; }

        public async Task LoadInitialMusic()
        {
            StorageLibrary music = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Music);
            QueryOptions query = new QueryOptions(CommonFileQuery.OrderByDate, new string[] { ".mp3" });
            query.FolderDepth = FolderDepth.Deep;

            var files = await KnownFolders.MusicLibrary.CreateFileQueryWithOptions(query).GetFilesAsync();
            Files = files.ToList();
        }
    }
}
