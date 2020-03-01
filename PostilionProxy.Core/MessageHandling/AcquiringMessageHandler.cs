namespace PostilionProxy.Core.MessageHandling
{
    public class AcquiringMessageHandler : PostilionMessageHandlerBase
    {
        public override void OnConnected(IMessageSink messageSink)
        {
            base.OnConnected(messageSink);
            // todo: save current instance as active instance (singleton)
        }

        public override void OnDisconnected()
        {
            base.OnDisconnected();
            // todo: remove instance as active instance
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
