using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace UniversalPlayer.Managers
{
    public static class MediaManager
    {
        public static async Task<MediaPlaybackItem> CreateMediaPlaybackItemAsync(StorageFile song)
        {
            MediaPlaybackItem item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(song));
            MediaItemDisplayProperties properties = item.GetDisplayProperties();
            MusicProperties musicProperties = await song.Properties.GetMusicPropertiesAsync();

            properties.Type = Windows.Media.MediaPlaybackType.Music;
            properties.MusicProperties.Artist = musicProperties.Artist;
            properties.MusicProperties.Title = musicProperties.Title;

            item.ApplyDisplayProperties(properties);
            return item;
        }
    }

}