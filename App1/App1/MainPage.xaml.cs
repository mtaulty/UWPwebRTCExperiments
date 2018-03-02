namespace App1
{
    using App1.Model;
    using App1.Signalling;
    using Org.WebRtc;
    using PeerConnectionClient.Signalling;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Windows.Data.Json;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.addressDetails = new AddressDetails();
            this.mediaManager = new MediaManager(this.localMediaElement, this.remoteMediaElement, this.Dispatcher);
            this.peerManager = new PeerManager(this.mediaManager);
            this.peerManager.OnIceCandidate += this.OnLocalIceCandidateDeterminedAsync;
        }
        public AddressDetails AddressDetails
        {
            get => this.addressDetails;
            set => this.SetProperty(ref this.addressDetails, value);
        }
        public bool HasConnected => this.signaller?.IsConnected() == true;

        public bool IsInitiator
        {
            get => this.isInitiator;
            set => this.SetProperty(ref this.isInitiator, value);
        }

        async Task InitialiseAsync()
        {
            if (!this.initialised)
            {
                this.initialised = true;

                // I find that if I don't do this before Initialize() then I crash.
                await WebRTC.RequestAccessForMediaCapture();
                WebRTC.Initialize(this.Dispatcher);

                await this.mediaManager.CreateAsync();
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
                this.signaller.OnPeerHangup += OnSignallingPeerHangup;
            }
            this.signaller.Connect(this.AddressDetails.IPAddress,
                this.AddressDetails.Port.ToString(), this.AddressDetails.HostName);
        }
        void OnSignallingSignedIn()
        {
            this.FirePropertyChanged(nameof(this.HasConnected));
        }
        async void OnSignallingPeerConnected(int id, string name)
        {
            // We are simply going to jump at the first opportunity we get.
            if (this.isInitiator && (name != this.AddressDetails.HostName))
            {
                // We have found a peer to connect to so we will connect to it.
                this.peerManager.CreateConnectionForPeerAsync(id);
                await this.SendOfferToRemotePeerAsync();
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
        void OnSignallingPeerHangup(int peer_id)
        {
            this.peerManager.Shutdown();
        }
        async void OnSignallingMessageFromPeer(int peer_id, string message)
        {
            var jsonObject = JsonObject.Parse(message);

            switch (SignallerMessagingExtensions.GetMessageType(jsonObject))
            {
                case SignallerMessagingExtensions.MessageType.Offer:
                    await this.OnOfferMessageFromPeerAsync(peer_id, jsonObject);
                    break;
                case SignallerMessagingExtensions.MessageType.Answer:
                    await this.OnAnswerMessageFromPeerAsync(peer_id, jsonObject);
                    break;
                case SignallerMessagingExtensions.MessageType.Ice:
                    await this.OnIceMessageFromPeerAsync(peer_id, jsonObject);
                    break;
                default:
                    break;
            }
        }
        async Task OnOfferMessageFromPeerAsync(int peer_id, JsonObject message)
        {
            var sdp = SignallerMessagingExtensions.SdpFromJsonMessage(message);
            await this.AcceptRemotePeerOfferAsync(peer_id, sdp);
        }
        async Task OnAnswerMessageFromPeerAsync(int peer_id, JsonObject message)
        {
            var sdp = SignallerMessagingExtensions.SdpFromJsonMessage(message);
            await this.peerManager.AcceptRemoteAnswerAsync(sdp);
        }
        async Task OnIceMessageFromPeerAsync(int peer_id, JsonObject message)
        {
            var candidate = SignallerMessagingExtensions.IceCandidateFromJsonMessage(message);
            await this.peerManager.AddIceCandidateAsync(candidate);
        }
        async Task SendOfferToRemotePeerAsync()
        {
            // Create the offer.
            var description = await this.peerManager.CreateAndSetLocalOfferAsync();

            var jsonMessage = description.ToJsonMessageString(
                SignallerMessagingExtensions.MessageType.Offer);

            await this.signaller.SendToPeer(this.peerManager.PeerId, jsonMessage);
        }
        async Task AcceptRemotePeerOfferAsync(int peerId, string sdpDescription)
        {
            // Only if we're expecting a call.
            if (!this.isInitiator)
            {
                var answer = await this.peerManager.AcceptRemoteOfferAsync(peerId, sdpDescription);

                // And sent it back over the network to the peer as the answer.
                await this.signaller.SendToPeer(
                    this.peerManager.PeerId,
                    answer.ToJsonMessageString(SignallerMessagingExtensions.MessageType.Answer));
            }
        }
        async void OnLocalIceCandidateDeterminedAsync(RTCPeerConnectionIceEvent args)
        {
            // We send this to our connected peer immediately.
            if (this.signaller.IsConnected())
            {
                var jsonMessage = args.Candidate.ToJsonMessageString();
                await this.signaller.SendToPeer(this.peerManager.PeerId, jsonMessage);
            }
        }
        void ShutDown()
        {
            this.mediaManager.Shutdown();
            this.peerManager.Shutdown();
        }
        void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (!storage.Equals(value))
            {
                storage = value;
                this.FirePropertyChanged(propertyName);
            }
        }
        Visibility Negate(bool value)
        {
            return (value ? Visibility.Collapsed : Visibility.Visible);
        }
        MediaManager mediaManager;
        PeerManager peerManager;
        AddressDetails addressDetails;
        bool initialised;
        bool isInitiator;
        Signaller signaller;
    }
}
