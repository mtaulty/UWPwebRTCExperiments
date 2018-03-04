namespace App1
{
    using App1.Interfaces;
    using Org.WebRtc;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public class XamlMediaElementMediaManager : IMediaManager
    {
        public XamlMediaElementMediaManager(
            IXamlMediaElementProvider xamlElementProvider)
        {
            this.xamlElementProvider = xamlElementProvider;
        }
        public async Task CreateAsync(bool audioEnabled = true, bool videoEnabled = true)
        {
            this.media = Media.CreateMedia();

            RTCMediaStreamConstraints constraints = new RTCMediaStreamConstraints()
            {
                audioEnabled = audioEnabled,
                videoEnabled = videoEnabled
            };
            this.userMedia = await media.GetUserMedia(constraints);
        }
        public MediaStream UserMedia => this.userMedia;

        public Media Media => this.media;

        public async Task AddRemoteStreamAsync(MediaStream mediaStream)
        {
            await this.AddStreamToMediaElementAsync(ref this.remoteVideoTrack, mediaStream, this.xamlElementProvider.RemoteMediaElement, "REMOTE");
        }
        public async Task AddLocalStreamAsync(MediaStream mediaStream)
        {
            await this.AddStreamToMediaElementAsync(ref this.localVideoTrack, mediaStream, this.xamlElementProvider.LocalMediaElement, "LOCAL");
        }
        Task AddStreamToMediaElementAsync(ref MediaVideoTrack videoTrack, MediaStream mediaStream, MediaElement mediaElement, string label)
        {
            Task task = Task.CompletedTask;

            if (videoTrack == null)
            {
                videoTrack = mediaStream?.GetVideoTracks().FirstOrDefault();
            }

            if (videoTrack != null)
            {
                var track = videoTrack;

                task = this.DispatchAsync(
                    () =>
                    {
                        // Link it up with the MediaElement that we have in the UI.
                        this.media.AddVideoTrackMediaElementPair(track, mediaElement, label);
                    }
                );
            }
            return (task);
        }
        public void RemoveRemoteStream()
        {
            this.RemoveStream(ref this.remoteVideoTrack, this.xamlElementProvider.RemoteMediaElement);
        }
        public void RemoveLocalStream()
        {
            this.RemoveStream(ref this.localVideoTrack, this.xamlElementProvider.LocalMediaElement);
        }
        void RemoveStream(ref MediaVideoTrack videoTrack, MediaElement mediaElement)
        {
            if (videoTrack != null)
            {
                this.media.RemoveVideoTrackMediaElementPair(videoTrack);
                mediaElement.Source = null;
                videoTrack = null;
            }
        }
        public void Shutdown()
        {
            if (this.media != null)
            {
                if (this.localVideoTrack != null)
                {
                    this.media.RemoveVideoTrackMediaElementPair(this.localVideoTrack);
                    this.localVideoTrack.Dispose();
                    this.localVideoTrack = null;
                }
                if (this.remoteVideoTrack != null)
                {
                    this.media.RemoveVideoTrackMediaElementPair(this.remoteVideoTrack);
                    this.remoteVideoTrack.Dispose();
                    this.remoteVideoTrack = null;
                }
                this.userMedia = null;
                this.media.Dispose();
                this.media = null;
            }
        }
        async Task DispatchAsync(Action a)
        {
            await this.xamlElementProvider.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => a());
        }
        Media media;
        MediaStream userMedia;
        MediaVideoTrack remoteVideoTrack;
        MediaVideoTrack localVideoTrack;
        IXamlMediaElementProvider xamlElementProvider;
    }
}
