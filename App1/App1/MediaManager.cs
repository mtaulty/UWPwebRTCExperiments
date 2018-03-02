namespace App1
{
    using Org.WebRtc;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public class MediaManager
    {
        public MediaManager(
            MediaElement localElement,
            MediaElement remoteElement,
            CoreDispatcher dispatcher)
        {            
            this.localMediaElement = localElement;
            this.remoteMediaElement = remoteElement;
            this.dispatcher = dispatcher;
        }
        public async Task CreateAsync()
        {
            this.media = Media.CreateMedia();

            RTCMediaStreamConstraints constraints = new RTCMediaStreamConstraints()
            {
                audioEnabled = true,
                videoEnabled = true
            };
            this.userMedia = await media.GetUserMedia(constraints);

            if (this.localMediaElement != null)
            {
                this.media.AddVideoTrackMediaElementPair(this.LocalVideoTrack, this.localMediaElement, "LOCAL");
            }
        }
        public MediaStream UserMedia => this.userMedia;

        public void Shutdown()
        {
            if (this.media != null)
            {
                if (this.LocalVideoTrack != null)
                {
                    this.media.RemoveVideoTrackMediaElementPair(this.LocalVideoTrack);
                }
                if (this.remoteVideoTrack != null)
                {
                    this.media.RemoveVideoTrackMediaElementPair(this.remoteVideoTrack);
                    this.remoteVideoTrack.Dispose();
                    this.remoteVideoTrack = null;
                }
                this.media.Dispose();
                this.media = null;
            }
        }
        public async Task AddRemoteStreamAsync(MediaStreamEvent args)
        {
            // We ignore anything but the first one that we get.
            if (this.remoteVideoTrack == null)
            {
                // Get the first video track that's present if any
                this.remoteVideoTrack = args?.Stream?.GetVideoTracks().FirstOrDefault();

                if (this.remoteVideoTrack != null)
                {
                    await this.DispatchAsync(
                        () =>
                        {
                            // Link it up with the MediaElement that we have in the UI.
                            this.media.AddVideoTrackMediaElementPair(this.remoteVideoTrack, this.remoteMediaElement, "REMOTE");
                        }
                    );
                }
            }
        }
        public void RemoveRemoteStream()
        {
            if (this.remoteVideoTrack != null)
            {
                this.media.RemoveVideoTrackMediaElementPair(this.remoteVideoTrack);
                this.remoteMediaElement.Source = null;
                this.remoteVideoTrack = null;
            }
        }
        MediaVideoTrack LocalVideoTrack
        {
            get
            {
                return (this.userMedia?.GetVideoTracks()?.FirstOrDefault());
            }
        }
        async Task DispatchAsync(Action a)
        {
            await this.dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => a());
        }
        MediaElement localMediaElement;
        MediaElement remoteMediaElement;
        Media media;
        MediaStream userMedia;
        MediaVideoTrack remoteVideoTrack;
        CoreDispatcher dispatcher;
    }
}
