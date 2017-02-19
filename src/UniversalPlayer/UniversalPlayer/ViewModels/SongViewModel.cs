using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace UniversalPlayer.ViewModels
{
    public class SongViewModel : BaseViewModel
    {
        public StorageFile FileHandle { get; set; }
        public MusicProperties Properties { get; set; }
    }
}
