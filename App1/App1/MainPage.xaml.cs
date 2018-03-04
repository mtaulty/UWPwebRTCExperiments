namespace App1
{
    using App1.Interfaces;
    using App1.Model;
    using App1.Signalling;
    using Org.WebRtc;
    using PeerConnectionClient.Interfaces;
    using PeerConnectionClient.Signalling;
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Windows.Data.Json;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage(ISignallingService signaller, IXamlMediaElementProvider xamlElementProvider, 
            IPeerManager peerManager, IMediaManager mediaManager)
        {
            this.InitializeComponent();
            this.addressDetails = new AddressDetails();

            xamlElementProvider.LocalMediaElement = this.localMediaElement;
            xamlElementProvider.RemoteMediaElement = this.remoteMediaElement;
            xamlElementProvider.Dispatcher = this.Dispatcher;

            this.mediaManager = mediaManager;
            this.peerManager = peerManager;
            this.peerManager.OnIceCandidate += this.OnLocalIceCandidateDeterminedAsync;
            this.signaller = signaller;
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

                await this.mediaManager.AddLocalStreamAsync(this.mediaManager.UserMedia);
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
            await this.signaller.ConnectAsync(
                this.AddressDetails.IPAddress,
                this.AddressDetails.Port.ToString(),
                this.AddressDetails.HostName);
        }
        void OnSignallingSignedIn()
        {
            this.FirePropertyChanged(nameof(this.HasConnected));
        }
        async void OnSignallingPeerConnected(object id, string name)
        {
            // We are simply going to jump at the first opportunity we get.
            if (this.isInitiator && (name != this.AddressDetails.HostName))
            {
                // We have found a peer to connect to so we will connect to it.
                this.peerManager.CreateConnectionForPeerAsync((int)id);

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
        void OnSignallingPeerHangup(object peerId)
        {
            this.peerManager.Shutdown();
        }
        async void OnSignallingMessageFromPeer(object peerId, string message)
        {
            var numericalPeerId = (int)peerId;

            var jsonObject = JsonObject.Parse(message);

            switch (SignallerMessagingExtensions.GetMessageType(jsonObject))
            {
                case SignallerMessagingExtensions.MessageType.Offer:
                    await this.OnOfferMessageFromPeerAsync(numericalPeerId, jsonObject);
                    break;
                case SignallerMessagingExtensions.MessageType.Answer:
                    await this.OnAnswerMessageFromPeerAsync(numericalPeerId, jsonObject);
                    break;
                case SignallerMessagingExtensions.MessageType.Ice:
                    await this.OnIceMessageFromPeerAsync(numericalPeerId, jsonObject);
                    break;
                default:
                    break;
            }
        }
        async Task OnOfferMessageFromPeerAsync(int peerId, JsonObject message)
        {
            var sdp = SignallerMessagingExtensions.SdpFromJsonMessage(message);
            await this.AcceptRemotePeerOfferAsync(peerId, sdp);
        }
        async Task OnAnswerMessageFromPeerAsync(int peerId, JsonObject message)
        {
            var sdp = SignallerMessagingExtensions.SdpFromJsonMessage(message);
            await this.peerManager.AcceptRemoteAnswerAsync(sdp);
        }
        async Task OnIceMessageFromPeerAsync(int peerId, JsonObject message)
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

            await this.signaller.SendToPeerAsync(this.peerManager.PeerId, jsonMessage);
        }
        async Task AcceptRemotePeerOfferAsync(int peerId, string sdpDescription)
        {
            // Only if we're expecting a call.
            if (!this.isInitiator)
            {
                var answer = await this.peerManager.AcceptRemoteOfferAsync(peerId, sdpDescription);

                // And sent it back over the network to the peer as the answer.
                await this.signaller.SendToPeerAsync(
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
                await this.signaller.SendToPeerAsync(this.peerManager.PeerId, jsonMessage);
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
        IMediaManager mediaManager;
        IPeerManager peerManager;
        ISignallingService signaller;
        AddressDetails addressDetails;
        bool initialised;
        bool isInitiator;
    }
}
