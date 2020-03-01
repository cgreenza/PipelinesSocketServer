namespace PostilionProxy.Core.MessageHandling
{
    public interface IActiveAcquiringMessageHandlerTracker
    {
        AcquiringMessageHandler GetCurrent();
        void Register(AcquiringMessageHandler acquiringMessageHandler);
        void Unregister(AcquiringMessageHandler acquiringMessageHandler);
    }
}