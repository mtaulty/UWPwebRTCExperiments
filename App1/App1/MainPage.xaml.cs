namespace App1
{
    using Org.WebRtc;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        enum OperatingMode
        {
            TBD,
            Offering,
            Answering
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.CanInitialise = true;
            this.CurrentOperatingMode = OperatingMode.TBD;
        }
        OperatingMode CurrentOperatingMode
        {
            get => this.currentOperatingMode;
            set
            {
                if (value != this.currentOperatingMode)
                {
                    this.currentOperatingMode = value;
                    this.FireModeDependentPropertyChanges();
                }
            }
        }
        void FireModeDependentPropertyChanges()
        {
            this.FirePropertyChanged(nameof(this.ShowOfferGrid));
            this.FirePropertyChanged(nameof(this.ShowRemoteGrid));
            this.FirePropertyChanged(nameof(this.ShowRemoteAnswerGrid));
            this.FirePropertyChanged(nameof(this.ShowLocalAnswerGrid));
        }
        void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public bool ShowOfferGrid =>
            this.HasInitialised && this.CurrentOperatingMode != OperatingMode.Answering;

        public bool ShowRemoteGrid =>
            this.HasInitialised && this.currentOperatingMode == OperatingMode.TBD;

        public bool ShowLocalAnswerGrid =>
            this.HasInitialised && this.currentOperatingMode == OperatingMode.Answering;

        public bool ShowRemoteAnswerGrid =>
            this.HasInitialised && this.currentOperatingMode == OperatingMode.Offering;

        public bool CanInitialise
        {
            get => this.canInitialise;
            set
            {
                if (this.canInitialise != value)
                {
                    this.canInitialise = value;
                    this.FirePropertyChanged();
                    this.FirePropertyChanged(nameof(this.HasInitialised));
                    this.FireModeDependentPropertyChanges();
                }
            }
        }
        public string IceCandidates
        {
            get => this.iceCandidates;
            set
            {
                if (this.iceCandidates != value)
                {
                    this.iceCandidates = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public string LocalOfferSdp
        {
            get => this.localOfferSdp;
            set
            {
                if (this.localOfferSdp != value)
                {
                    this.localOfferSdp = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public string LocalAnswerSdp
        {
            get => this.localAnswerSdp;
            set
            {
                if (this.localAnswerSdp != value)
                {
                    this.localAnswerSdp = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public string RemoteDescriptionSdp
        {
            get => this.remoteDescriptionSdp;
            set
            {
                if (this.remoteDescriptionSdp != value)
                {
                    this.remoteDescriptionSdp = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public string RemoteAnswerSdp
        {
            get => this.remoteAnswerSdp;
            set
            {
                if (this.remoteAnswerSdp != value)
                {
                    this.remoteAnswerSdp = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public bool HasInitialised => !this.CanInitialise;

        async void OnInitialise()
        {
            if (this.CanInitialise)
            {
                this.CanInitialise = false;

                // I find that if I don't do this before Initialize() then I crash.
                await WebRTC.RequestAccessForMediaCapture();

                WebRTC.Initialize(this.Dispatcher);

                RTCMediaStreamConstraints constraints = new RTCMediaStreamConstraints()
                {
                    audioEnabled = true,
                    videoEnabled = true
                };

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

                this.media = Media.CreateMedia();
                this.userMedia = await media.GetUserMedia(constraints);

                this.peerConnection.AddStream(this.userMedia);
                this.peerConnection.OnAddStream += OnRemoteStreamAdded;
                this.peerConnection.OnIceCandidate += OnIceCandidate;
            }
        }

        void OnIceCandidate(RTCPeerConnectionIceEvent args)
        {
            this.IceCandidates += $"{args.Candidate.Candidate}|{args.Candidate.SdpMid}|{args.Candidate.SdpMLineIndex}\n";
        }

        async void OnCreateOffer()
        {
            if (this.HasInitialised)
            {
                this.CurrentOperatingMode = OperatingMode.Offering;

                // Create the offer.
                var description = await this.peerConnection.CreateOffer();

                // We filter some pieces out of the SDP based on what I think
                // aren't supported Codecs. I largely took it from the original sample
                // when things didn't work for me without it.
                var filteredDescriptionSdp = FilterToSupportedCodecs(description.Sdp);

                description.Sdp = filteredDescriptionSdp;

                // Set that filtered offer description as our local description.
                await this.peerConnection.SetLocalDescription(description);

                // Put it on the UI so someone can copy it.
                this.LocalOfferSdp = description.Sdp;
            }
        }
        async void OnSetRemoteDescription()
        {
            this.CurrentOperatingMode = OperatingMode.Answering;

            // Take the description from the UI and set it as our Remote Description
            // of type 'offer'
            await this.SetSessionDescription(RTCSdpType.Offer, this.RemoteDescriptionSdp);

            // And create our answer
            var answer = await this.peerConnection.CreateAnswer();

            // And set that as our local description
            await this.peerConnection.SetLocalDescription(answer);

            // And put it back into the UI
            this.LocalAnswerSdp = answer.Sdp;
        }
        async void OnSetRemoteAnswer()
        {
            await this.SetSessionDescription(RTCSdpType.Answer, this.RemoteAnswerSdp);
        }
        async Task SetSessionDescription(RTCSdpType type, string description)
        {
            var modifiedNewLineDescription = description.Replace("\r", "\n");

            await this.peerConnection.SetRemoteDescription(
                new RTCSessionDescription(type, modifiedNewLineDescription));
        }
        void OnRemoteStreamAdded(MediaStreamEvent args)
        {
            if (this.mediaElement.Source == null)
            {
                // Get the first video track that's present if any
                var firstTrack = args?.Stream?.GetVideoTracks().FirstOrDefault();

                if (firstTrack != null)
                {
                    // Link it up with the MediaElement that we have in the UI.
                    this.media.AddVideoTrackMediaElementPair(firstTrack, this.mediaElement, "a label");
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
        async void OnWriteIceToFile()
        {
            var file = await FileDialogExtensions.PickFileForSaveAsync(
                "text file", ".txt", "ice.txt");

            await FileIO.WriteTextAsync(file, this.IceCandidates);
        }
        async void OnReadIceFromFile()
        {
            var file = await FileDialogExtensions.PickFileForReadAsync(".txt");

            var contents = await FileIO.ReadTextAsync(file);
            var lines = contents.Split("\n");

            foreach (var line in lines)
            {
                var pieces = line.Split('|');

                if (pieces.Length == 3)
                {
                    RTCIceCandidate candidate = new RTCIceCandidate(
                        pieces[0], pieces[1], ushort.Parse(pieces[2]));

                    await this.peerConnection.AddIceCandidate(candidate);
                }
            }
        }
        OperatingMode currentOperatingMode = OperatingMode.Offering;
        Media media;
        MediaStream userMedia;
        RTCPeerConnection peerConnection;
        bool canInitialise;
        string iceCandidates;
        string localOfferSdp;
        string remoteDescriptionSdp;
        string remoteAnswerSdp;
        string localAnswerSdp;
    }
}
