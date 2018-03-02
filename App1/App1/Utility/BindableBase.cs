
namespace App1.Utility
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public class BindableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetProperty<T>(ref T storage, T value, 
            [CallerMemberName] string propertyName = null) 
        {
            if (!storage.Equals(value))
            {
                storage = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}


