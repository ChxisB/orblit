using System.Threading.Tasks;

namespace OrblitTools.IdeMessaging
{
    public interface IMessageHandler
    {
        public Task<MessageContent> HandleRequest(Peer peer, string id, MessageContent content, ILogger logger);
    }
}
