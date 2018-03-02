namespace App1.Model
{
    using App1.Utility;
    using System.Linq;
    using Windows.Networking.Connectivity;

    public class AddressDetails : BindableBase
    {
        public string IPAddress
        {
            get => this.ipAddress;
            set => base.SetProperty(ref this.ipAddress, value);
        }
        public int Port
        {
            get => this.port;
            set => base.SetProperty(ref this.port, value);
        }
        public string HostName
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
        int port = 8888;
        string ipAddress = "52.174.16.92";
    }
}
