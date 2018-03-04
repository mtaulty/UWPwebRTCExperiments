namespace App1.Interfaces
{
    using System.ComponentModel;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public interface IXamlMediaElementProvider : INotifyPropertyChanged
    {
        MediaElement RemoteMediaElement { get; set;  }
        MediaElement LocalMediaElement { get; set; }
        CoreDispatcher Dispatcher { get; set; }
     }
}
