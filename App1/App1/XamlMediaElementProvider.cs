namespace App1
{
    using App1.Interfaces;
    using App1.Utility;
    using ConversationLibrary.Interfaces;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public class XamlMediaElementProvider : BindableBase, IXamlMediaElementProvider
    {
        public MediaElement RemoteMediaElement
        {
            get => this.remoteMediaElement;
            set => base.SetProperty(ref this.remoteMediaElement, value);
        }
        public MediaElement LocalMediaElement
        {
            get => this.localMediaElement;
            set => base.SetProperty(ref this.localMediaElement, value);
        }
        public CoreDispatcher Dispatcher
        {
            get => this.dispatcher;
            set => base.SetProperty(ref this.dispatcher, value);
        }
        CoreDispatcher dispatcher;
        MediaElement remoteMediaElement;
        MediaElement localMediaElement;
    }
}
