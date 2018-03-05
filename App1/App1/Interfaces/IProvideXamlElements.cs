namespace App1.Interfaces
{
    using System.ComponentModel;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public interface IXamlDispatcherProvider : INotifyPropertyChanged
    {
        CoreDispatcher Dispatcher { get; set; }
    }

    public interface IXamlMediaElementProvider : IXamlDispatcherProvider
    {
        MediaElement RemoteMediaElement { get; set;  }
        MediaElement LocalMediaElement { get; set; }
     }
}
