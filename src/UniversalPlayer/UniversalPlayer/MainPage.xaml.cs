using BackgroundAudioShared;
using BackgroundAudioShared.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using UniversalPlayer.Communication.Models;
using UniversalPlayer.Managers;
using UniversalPlayer.Pages;
using UniversalPlayer.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UniversalPlayer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private AutoResetEvent backgroundAudioTaskStarted = new AutoResetEvent(false);
        public MainViewModel MainViewModel { get; set; } = new MainViewModel();

        public MainPage()
        {
            this.InitializeComponent();

            DataContext = MainViewModel;
            Loaded += MainPage_Loaded;

            BackgroundMediaPlayer.MessageReceivedFromBackground += OnBackgroundMessage;
        }

        private void OnBackgroundMessage(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            BackgroundAudioTaskStartedMessage backgroundAudioTaskStartedMessage;
            if (MessageService.TryParseMessage(e.Data, out backgroundAudioTaskStartedMessage))
            {
                // StartBackgroundAudioTask is waiting for this signal to know when the task is up and running
                // and ready to receive messages
                Debug.WriteLine("BackgroundAudioTask started");
                backgroundAudioTaskStarted.Set();
                return;
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!loaded)
            {
                Frame.Navigate(typeof(LoadingPage));
                loaded = true;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is List<SongViewModel>)
            {
                foreach (SongViewModel song in e.Parameter as List<SongViewModel>)
                {
                    MainViewModel.Songs.Add(song);
                    MainViewModel.MediaPlaybackList.Items.Add(await MediaManager.CreateMediaPlaybackItemAsync(song.FileHandle));
                }

                //player.Source =  MainViewModel.MediaPlaybackList;
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var song = e.AddedItems[0] as SongViewModel;
            if (!IsMyBackgroundTaskRunning || MediaPlayerState.Closed == player.CurrentState)
            {
                // First update the persisted start track
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.TrackId, song.Properties.Artist);
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.Position, new TimeSpan().ToString());

                // Start task
                StartBackgroundAudioTask();
            }
            else
            {
                // Switch to the selected track
                //MessageService.SendMessageToBackground(new TrackChangedMessage(song.FileHandle.Properties.)));
            }

            if (MediaPlayerState.Paused == player.CurrentState)
            {
                player.Play();
            }
           
            //MainViewModel.MediaPlaybackList.MoveTo((uint)(sender as ListView).SelectedIndex);
            //player.Play();
        }

        private void StartBackgroundAudioTask()
        {
            var startResult = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                bool result = backgroundAudioTaskStarted.WaitOne(10000);
                //Send message to initiate playback
                if (result == true)
                {
                    MessageService.SendMessageToBackground(new UpdatePlaylistMessage((MainViewModel.Songs.Select(s => new SongModel { Title = s.Properties.Title, MediaUri = s.FileHandle.Path }).ToList())));
                    MessageService.SendMessageToBackground(new StartPlaybackMessage());
                }
                else
                {
                    throw new Exception("Background Audio Task didn't start in expected time");
                }
            });
        }

        private bool _isMyBackgroundTaskRunning;
        private bool IsMyBackgroundTaskRunning
        {
            get
            {
                if (_isMyBackgroundTaskRunning)
                    return true;

                string value = ApplicationSettingsHelper.ReadResetSettingsValue(ApplicationSettingsConstants.BackgroundTaskState) as string;
                if (value == null)
                {
                    return false;
                }
                else
                {
                    try
                    {
                        _isMyBackgroundTaskRunning = EnumHelper.Parse<BackgroundTaskState>(value) == BackgroundTaskState.Running;
                    }
                    catch (ArgumentException)
                    {
                        _isMyBackgroundTaskRunning = false;
                    }
                    return _isMyBackgroundTaskRunning;
                }
            }
        }

        private static bool loaded;
        private MediaPlayer player = BackgroundMediaPlayer.Current;
    }
}
