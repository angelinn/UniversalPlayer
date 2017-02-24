using BackgroundAudioShared;
using BackgroundAudioShared.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalPlayer.Communication.Models;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniversalPlayer.BackgroundTask
{
    public sealed class BackgroundAudioTask : IBackgroundTask
    {
        private BackgroundTaskDeferral deferral;
        private AppState foregroundAppState = AppState.Unknown;
        private ManualResetEvent backgroundTaskStarted = new ManualResetEvent(false);

        private SystemMediaTransportControls smtc;
        private MediaPlaybackList playbackList = new MediaPlaybackList();

        private bool playbackStartedPreviously;

        string GetCurrentTrackId()
        {
            if (playbackList == null)
                return null;

            return GetTrackId(playbackList.CurrentItem);
        }

        string GetTrackId(MediaPlaybackItem item)
        {
            if (item == null)
                return null; // no track playing

            return item.Source.CustomProperties[ApplicationSettingsConstants.TrackId] as string;
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            smtc = BackgroundMediaPlayer.Current.SystemMediaTransportControls;
            smtc.ButtonPressed += OnSmtcButtonPressed;
            smtc.PropertyChanged += OnSmtcPropertyChanged;
            smtc.IsEnabled = true;
            smtc.IsPauseEnabled = true;
            smtc.IsPlayEnabled = true;
            smtc.IsNextEnabled = true;
            smtc.IsPreviousEnabled = true;

            var value = ApplicationSettingsHelper.ReadResetSettingsValue(ApplicationSettingsConstants.AppState);
            if (value == null)
                foregroundAppState = AppState.Unknown;
            else
                foregroundAppState = EnumHelper.Parse<AppState>(value.ToString());

            BackgroundMediaPlayer.Current.CurrentStateChanged += OnMediaPlayerCurrentStateChanged;
            BackgroundMediaPlayer.MessageReceivedFromForeground += OnMessageFromForegroundReceived;

            if (foregroundAppState != AppState.Suspended)
                MessageService.SendMessageToForeground(new BackgroundAudioTaskStartedMessage());

            ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.BackgroundTaskState, BackgroundTaskState.Running.ToString());

            deferral = taskInstance.GetDeferral();

            backgroundTaskStarted.Set();

            taskInstance.Task.Completed += (s, a) => deferral.Complete();
            taskInstance.Canceled += new BackgroundTaskCanceledEventHandler(OnCanceled);
        }

        /// <summary>
        /// Handles background task cancellation. Task cancellation happens due to:
        /// 1. Another Media app comes into foreground and starts playing music 
        /// 2. Resource pressure. Your task is consuming more CPU and memory than allowed.
        /// In either case, save state so that if foreground app resumes it can know where to start.
        /// </summary>
        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            try
            {
                backgroundTaskStarted.Reset();

                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.TrackId, GetCurrentTrackId() == null ? null : GetCurrentTrackId().ToString());
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.Position, BackgroundMediaPlayer.Current.PlaybackSession.Position.ToString());
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.BackgroundTaskState, BackgroundTaskState.Canceled.ToString());
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.AppState, Enum.GetName(typeof(AppState), foregroundAppState));

                if (playbackList != null)
                {
                    playbackList.CurrentItemChanged -= OnCurrentItemChanged;
                    playbackList = null;
                }

                BackgroundMediaPlayer.MessageReceivedFromForeground -= OnMessageFromForegroundReceived;
                smtc.ButtonPressed -= OnSmtcButtonPressed;
                smtc.PropertyChanged -= OnSmtcPropertyChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            deferral.Complete();
        }

        private void UpdateUVCOnNewTrack(MediaPlaybackItem item)
        {
            if (item == null)
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
                smtc.DisplayUpdater.MusicProperties.Title = String.Empty;
                smtc.DisplayUpdater.Update();
            }
            else
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
                smtc.DisplayUpdater.Type = MediaPlaybackType.Music;

                smtc.DisplayUpdater.MusicProperties.Title = item.Source.CustomProperties["title"] as string;
                smtc.DisplayUpdater.MusicProperties.Artist = item.Source.CustomProperties["artist"] as string;

                //var albumArtUri = item.Source.CustomProperties[AlbumArtKey] as Uri;
                Uri albumArtUri = null;
                if (albumArtUri != null)
                    smtc.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromUri(albumArtUri);
                else
                    smtc.DisplayUpdater.Thumbnail = null;

                smtc.DisplayUpdater.Update();
            }
        }

        /// <summary>
        /// Fires when any SystemMediaTransportControl property is changed by system or user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void OnSmtcPropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            // If soundlevel turns to muted, app can choose to pause the music
        }

        private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:

                    // When the background task has been suspended and the SMTC
                    // starts it again asynchronously, some time is needed to let
                    // the task startup process in Run() complete.

                    // Wait for task to start. 
                    // Once started, this stays signaled until shutdown so it won't wait
                    // again unless it needs to.
                    bool result = backgroundTaskStarted.WaitOne(5000);
                    if (!result)
                        throw new Exception("Background Task didnt initialize in time");

                    StartPlayback();
                    break;
                case SystemMediaTransportControlsButton.Pause:

                    BackgroundMediaPlayer.Current.Pause();

                    break;
                case SystemMediaTransportControlsButton.Next:
                    SkipToNext();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    SkipToPrevious();
                    break;
            }
        }

        private void StartPlayback()
        {
            // If playback was already started once we can just resume playing.
            if (!playbackStartedPreviously)
            {
                playbackStartedPreviously = true;

                // If the task was cancelled we would have saved the current track and its position. We will try playback from there.
                string currentTrackId = ApplicationSettingsHelper.ReadResetSettingsValue(ApplicationSettingsConstants.TrackId) as string;
                var currentTrackPosition = ApplicationSettingsHelper.ReadResetSettingsValue(ApplicationSettingsConstants.Position);

                if (currentTrackId != null)
                {
                    // Find the index of the item by name
                    int index = playbackList.Items.ToList().FindIndex(item => GetTrackId(item) == currentTrackId);

                    if (currentTrackPosition == null)
                    {
                        // Play from start if we dont have position
                        playbackList.MoveTo((uint)index);
                        BackgroundMediaPlayer.Current.Play();
                    }
                    else
                    {
                        // Play from exact position otherwise
                        TypedEventHandler<MediaPlaybackList, CurrentMediaPlaybackItemChangedEventArgs> handler = null;
                        handler = (MediaPlaybackList list, CurrentMediaPlaybackItemChangedEventArgs args) =>
                        {
                            if (args.NewItem == playbackList.Items[index])
                            {
                                // Unsubscribe because this only had to run once for this item
                                playbackList.CurrentItemChanged -= handler;

                                // Set position
                                var position = TimeSpan.Parse((string)currentTrackPosition);
                                BackgroundMediaPlayer.Current.PlaybackSession.Position = position;

                                BackgroundMediaPlayer.Current.Play();
                            }
                        };
                        playbackList.CurrentItemChanged += handler;

                        // Switch to the track which will trigger an item changed event
                        playbackList.MoveTo((uint)index);
                    }
                }
                else
                    BackgroundMediaPlayer.Current.Play();
            }
            else
                BackgroundMediaPlayer.Current.Play();
        }


        /// <summary>
        /// Raised when playlist changes to a new track
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            MediaPlaybackItem item = args.NewItem;
            UpdateUVCOnNewTrack(item);
            
            string currentTrackId = null;
            if (item != null)
                currentTrackId = GetTrackId(item);
            
            if (foregroundAppState == AppState.Active)
                MessageService.SendMessageToForeground(new TrackChangedMessage(currentTrackId));
            else
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.TrackId, currentTrackId == null ? null : currentTrackId);
        }
        
        private void SkipToPrevious()
        {
            smtc.PlaybackStatus = MediaPlaybackStatus.Changing;
            playbackList.MovePrevious();
        }
        
        private void SkipToNext()
        {
            smtc.PlaybackStatus = MediaPlaybackStatus.Changing;
            playbackList.MoveNext();
        }
        
        void OnMediaPlayerCurrentStateChanged(MediaPlayer sender, object args)
        {
            if (sender.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            else if (sender.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
            else if (sender.CurrentState == MediaPlayerState.Closed)
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            }
        }
        
        void OnMessageFromForegroundReceived(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            AppSuspendedMessage appSuspendedMessage;
            if (MessageService.TryParseMessage(e.Data, out appSuspendedMessage))
            {
                foregroundAppState = AppState.Suspended;
                var currentTrackId = GetCurrentTrackId();
                ApplicationSettingsHelper.SaveSettingsValue(ApplicationSettingsConstants.TrackId, currentTrackId == null ? null : currentTrackId.ToString());
                return;
            }

            AppResumedMessage appResumedMessage;
            if (MessageService.TryParseMessage(e.Data, out appResumedMessage))
            {
                foregroundAppState = AppState.Active;
                return;
            }

            StartPlaybackMessage startPlaybackMessage;
            if (MessageService.TryParseMessage(e.Data, out startPlaybackMessage))
            {
                StartPlayback();
                return;
            }

            SkipNextMessage skipNextMessage;
            if (MessageService.TryParseMessage(e.Data, out skipNextMessage))
            {
                SkipToNext();
                return;
            }

            SkipPreviousMessage skipPreviousMessage;
            if (MessageService.TryParseMessage(e.Data, out skipPreviousMessage))
            {
                SkipToPrevious();
                return;
            }

            TrackChangedMessage trackChangedMessage;
            if (MessageService.TryParseMessage(e.Data, out trackChangedMessage))
            {
                var index = playbackList.Items.ToList().FindIndex(i => GetTrackId(i) == trackChangedMessage.TrackId);
                smtc.PlaybackStatus = MediaPlaybackStatus.Changing;
                playbackList.MoveTo((uint)index);
                return;
            }

            UpdatePlaylistMessage updatePlaylistMessage;
            if (MessageService.TryParseMessage(e.Data, out updatePlaylistMessage))
            {
                CreatePlaybackList(updatePlaylistMessage.Songs);
                return;
            }
        }
        
        void CreatePlaybackList(List<SongModel> songs)
        {
            foreach (SongModel song in songs)
            {
                StorageFile file = StorageFile.GetFileFromPathAsync(song.MediaUri).AsTask().Result;

                MediaPlaybackItem item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(file));
                item.Source.CustomProperties[ApplicationSettingsConstants.TrackId] = song.TrackID;
                item.Source.CustomProperties["title"] = song.Title;
                item.Source.CustomProperties["artist"] = song.Artist;

                playbackList.Items.Add(item);
            }
            
            BackgroundMediaPlayer.Current.AutoPlay = false;
            BackgroundMediaPlayer.Current.Source = playbackList;
            playbackList.CurrentItemChanged += OnCurrentItemChanged;
        }
    }
}
