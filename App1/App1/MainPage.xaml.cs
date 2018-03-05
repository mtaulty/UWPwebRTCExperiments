namespace App1
{
    using App1.Interfaces;
    using App1.Model;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // TODO: bit naughty to have a non-default constructor on a XAML page, could move these out
        // to injected property values.
        public MainPage(IConversationManager conversationManager, IXamlMediaElementProvider xamlElementProvider)
        {
            this.InitializeComponent();

            this.addressDetails = new AddressDetails();

            this.conversationManager = conversationManager;

            // NB: setting these here is really a race condition unless the components that use
            // them handle their property change notification.
            xamlElementProvider.LocalMediaElement = this.localMediaElement;
            xamlElementProvider.RemoteMediaElement = this.remoteMediaElement;
            xamlElementProvider.Dispatcher = this.Dispatcher;
        }
        public AddressDetails AddressDetails
        {
            get => this.addressDetails;
            set => this.SetProperty(ref this.addressDetails, value);
        }
        public bool HasConnected
        {
            get => this.hasConnected;
            set => this.SetProperty(ref this.hasConnected, value);
        }

        public bool IsInitiator
        {
            get => this.isInitiator;
            set => this.SetProperty(ref this.isInitiator, value);
        }

        async void OnConnectToSignallingAsync()
        {
            await this.conversationManager.InitialiseAsync(this.addressDetails.HostName);

            this.conversationManager.IsInitiator = this.isInitiator;

            this.HasConnected = await this.conversationManager.ConnectToSignallingAsync(
                this.addressDetails.IPAddress, this.addressDetails.Port);
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
        IConversationManager conversationManager;
        AddressDetails addressDetails;
        bool hasConnected;
        bool isInitiator;
    }
}
