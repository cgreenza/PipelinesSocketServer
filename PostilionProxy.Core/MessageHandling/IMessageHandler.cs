namespace PostilionProxy.Core.MessageHandling
{
    public interface IMessageHandler
    {
        void OnConnected(IMessageSink sink);
        void OnDisconnected();
        void ProcessMessage(PostilionMessage message); // todo: make async?
    }

}
