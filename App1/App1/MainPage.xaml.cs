namespace App1
{
    using Org.WebRtc;
    using PeerConnectionClient.Signalling;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Windows.Data.Json;
    using Windows.Networking.Connectivity;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.currentPeerId = null;
        }
        public bool HasConnected => this.signaller?.IsConnected() == true;

        public bool IsInitiator
        {
            get => this.isInitiator;
            set
            {
                if (this.isInitiator != value)
                {
                    this.isInitiator = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public string IPAddress
        {
            get => this.ipAddress;
            set
            {
                if (this.ipAddress != value)
                {
                    this.ipAddress = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public int Port
        {
            get => this.port;
            set
            {
                if (this.port != value)
                {
                    this.port = value;
                    this.FirePropertyChanged();
                }
            }
        }
        string HostName
        {
            get
            {
                var candidate =
                    NetworkInformation.GetHostNames()
                    .Where(n => !string.IsNullOrEmpty(n.DisplayName)).FirstOrDefault();

                // Note - only candidate below can be null, not the Displayname
                return (candidate?.DisplayName ?? "Anonymous");
            }
        }
        async Task InitialiseAsync()
        {
            if (!this.initialised)
            {
                this.initialised = true;

                // I find that if I don't do this before Initialize() then I crash.
                await WebRTC.RequestAccessForMediaCapture();

                WebRTC.Initialize(this.Dispatcher);

                this.media = Media.CreateMedia();

                RTCMediaStreamConstraints constraints = new RTCMediaStreamConstraints()
                {
                    audioEnabled = true,
                    videoEnabled = true
                };
                this.userMedia = await media.GetUserMedia(constraints);

                this.media.AddVideoTrackMediaElementPair(this.LocalVideoTrack, this.localMediaElement, "LOCAL");
            }
        }
        async void OnConnectToSignallingAsync()
        {
            await this.InitialiseAsync();

            if (this.signaller == null)
            {
                this.signaller = new Signaller();

                // Note - not trying to handle everything here, just trying to handle the
                // minimum that I can to see if I can get things working.
                this.signaller.OnSignedIn += OnSignallingSignedIn;
                this.signaller.OnPeerConnected += OnSignallingPeerConnected;
                this.signaller.OnMessageFromPeer += OnSignallingMessageFromPeer;

                this.signaller.OnServerConnectionFailure += OnSignallingServerConnectionFailure;

                this.signaller.OnDisconnected += OnSignallingDisconnected;
            }
            this.signaller.Connect(this.IPAddress, this.Port.ToString(), this.HostName);
        }
        void OnSignallingSignedIn()
        {
            this.FirePropertyChanged(nameof(this.HasConnected));
        }
        async void OnSignallingPeerConnected(int id, string name)
        {
            // We are simply going to jump at the first opportunity we get.
            if (this.isInitiator && (name != this.HostName) && !this.currentPeerId.HasValue)
            {
                // We have found a peer to connect to so we will connect to it.
                this.currentPeerId = id;

                await this.CreatePeerConnectionAsync();

                await this.SendOfferAsync();
            }
        }
        void OnSignallingDisconnected()
        {
            this.ShutDown();
        }
        void OnSignallingServerConnectionFailure()
        {
            this.ShutDown();
        }
        void ShutDown()
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
            if (this.peerConnection != null)
            {
                this.peerConnection.OnIceCandidate -= this.OnIceCandidate;
                this.peerConnection.OnAddStream -= this.OnRemoteStreamAdded;
                this.peerConnection.Close();
                this.peerConnection = null;
            }
        }
        async void OnSignallingMessageFromPeer(int peer_id, string message)
        {
            var jsonObject = JsonObject.Parse(message);
            var sdp = string.Empty;

            if (this.currentPeerId == null)
            {
                this.currentPeerId = peer_id;
            }

            if (this.currentPeerId == peer_id)
            {
                switch (SignallerMessagingExtensions.GetMessageType(jsonObject))
                {
                    case SignallerMessagingExtensions.MessageType.Offer:
                        sdp = SignallerMessagingExtensions.SdpFromJsonMessage(jsonObject);
                        await this.AcceptRemoteOfferAsync(sdp);
                        break;
                    case SignallerMessagingExtensions.MessageType.Answer:
                        sdp = SignallerMessagingExtensions.SdpFromJsonMessage(jsonObject);
                        await this.peerConnection.SetRemoteDescription(new RTCSessionDescription(RTCSdpType.Answer, sdp));
                        break;
                    case SignallerMessagingExtensions.MessageType.Ice:
                        var candidate = SignallerMessagingExtensions.IceCandidateFromJsonMessage(jsonObject);
                        await this.peerConnection.AddIceCandidate(candidate);
                        break;
                    default:
                        break;
                }
            }
        }
        Visibility Negate(bool value)
        {
            return (value ? Visibility.Collapsed : Visibility.Visible);
        }
        async Task CreatePeerConnectionAsync()
        {
            if (this.peerConnection == null)
            {             
                this.peerConnection = new RTCPeerConnection(
                    new RTCConfiguration()
                    {
                    // Hard-coding these for now...
                    BundlePolicy = RTCBundlePolicy.Balanced,

                    // I got this wrong for a long time. Because I am not using ICE servers
                    // I thought this should be 'NONE' but it shouldn't. Even though I am
                    // not going to add any ICE servers, I still need ICE in order to
                    // get candidates for how the 2 ends should talk to each other.
                    // Lesson learned, took a few hours to realise it :-)
                    IceTransportPolicy = RTCIceTransportPolicy.All
                    }
                );

                this.peerConnection.AddStream(this.userMedia);
                this.peerConnection.OnAddStream += OnRemoteStreamAdded;
                this.peerConnection.OnIceCandidate += OnIceCandidate;
            }
        }
        MediaVideoTrack LocalVideoTrack
        {
            get
            {
                return (this.userMedia?.GetVideoTracks()?.FirstOrDefault());
            }
        }
        async Task SendOfferAsync()
        {
            // Create the offer.
            var description = await this.peerConnection.CreateOffer();

            // We filter some pieces out of the SDP based on what I think
            // aren't supported Codecs. I largely took it from the original sample
            // when things didn't work for me without it.
            var filteredDescriptionSdp = FilterToSupportedCodecs(description.Sdp);

            description.Sdp = filteredDescriptionSdp;

            // Set that filtered offer description as our local description.
            await this.peerConnection.SetLocalDescription(description);

            var jsonMessage = description.ToJsonMessageString(
                SignallerMessagingExtensions.MessageType.Offer);

            await this.signaller.SendToPeer(this.currentPeerId.Value, jsonMessage);
        }
        async Task AcceptRemoteOfferAsync(string sdpDescription)
        {
            // Only if we're expecting a call.
            if (!this.isInitiator)
            {
                await this.CreatePeerConnectionAsync();

                // Take the description from the UI and set it as our Remote Description
                // of type 'offer'
                await this.peerConnection.SetRemoteDescription(
                    new RTCSessionDescription(RTCSdpType.Offer, sdpDescription));

                // And create our answer
                var answer = await this.peerConnection.CreateAnswer();

                // And set that as our local description
                await this.peerConnection.SetLocalDescription(answer);

                // And sent it back over the network to the peer as the answer.
                await this.signaller.SendToPeer(
                    this.currentPeerId.Value,
                    answer.ToJsonMessageString(SignallerMessagingExtensions.MessageType.Answer));
            }
        }
        async void OnIceCandidate(RTCPeerConnectionIceEvent args)
        {
            // We send this to our connected peer immediately.
            if (this.signaller.IsConnected())
            {
                var jsonMessage = args.Candidate.ToJsonMessageString();
                await this.signaller.SendToPeer(this.currentPeerId.Value, jsonMessage);
            }
        }
        async void OnRemoteStreamAdded(MediaStreamEvent args)
        {
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
        /// <summary>
        /// Heavily borrowed from the original sample with some mods - the original sample also did
        /// some work to pick a specific video codec and also to move VP8 to the head of the list
        /// but I've not done that yet.
        /// </summary>
        /// <param name="originalSdp"></param>
        /// <param name="audioCodecs"></param>
        /// <returns></returns>
        static string FilterToSupportedCodecs(string originalSdp)
        {
            var filteredSdp = originalSdp;

            string[] incompatibleAudioCodecs =
                new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            var compatibleCodecs = WebRTC.GetAudioCodecs().Where(
                codec => !incompatibleAudioCodecs.Contains(codec.Name + codec.ClockRate));

            Regex mfdRegex = new Regex("\r\nm=audio.*RTP.*?( .\\d*)+\r\n");
            Match mfdMatch = mfdRegex.Match(filteredSdp);

            List<string> mfdListToErase = new List<string>(); //mdf = media format descriptor

            bool audioMediaDescFound = mfdMatch.Groups.Count > 1; //Group 0 is whole match

            if (audioMediaDescFound)
            {
                for (int groupCtr = 1/*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                {
                    for (int captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    {
                        mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());
                    }
                }
                mfdListToErase.RemoveAll(entry => compatibleCodecs.Any(c => c.Id.ToString() == entry));
            }

            if (audioMediaDescFound)
            {
                // Alter audio entry
                Regex audioRegex = new Regex("\r\n(m=audio.*RTP.*?)( .\\d*)+");
                filteredSdp = audioRegex.Replace(filteredSdp, "\r\n$1 " + string.Join(' ', compatibleCodecs.Select(c => c.Id)));
            }

            // Remove associated rtp mapping, format parameters, feedback parameters
            Regex removeOtherMdfs = new Regex("a=(rtpmap|fmtp|rtcp-fb):(" + String.Join("|", mfdListToErase) + ") .*\r\n");

            filteredSdp = removeOtherMdfs.Replace(filteredSdp, "");

            return (filteredSdp);
        }
        void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        async Task DispatchAsync(Action a)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => a());
        }
        bool initialised;
        bool isInitiator;
        Signaller signaller;
        Media media;
        MediaStream userMedia;
        MediaVideoTrack remoteVideoTrack;
        RTCPeerConnection peerConnection;
        int port = 8888;
        string ipAddress = "52.174.16.92";
        int? currentPeerId;
    }
}
