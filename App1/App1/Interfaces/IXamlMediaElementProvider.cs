namespace App1.Interfaces
{
    using ConversationLibrary.Interfaces;
    using Windows.UI.Xaml.Controls;

    public interface IXamlMediaElementProvider : IDispatcherProvider
    {
        MediaElement RemoteMediaElement { get; set; }
        MediaElement LocalMediaElement { get; set; }
    }
}
