using System.Threading.Tasks;

namespace App1.Interfaces
{
    public interface IConversationManager
    {
        bool IsInitiator { get; set; }

        Task<bool> ConnectToSignallingAsync(string ipAddress, int port);

        Task InitialiseAsync(string localHostName);
    }
}