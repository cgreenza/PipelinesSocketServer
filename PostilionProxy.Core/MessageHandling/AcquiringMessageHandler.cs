using System;
using System.Threading.Tasks;

namespace PostilionProxy.Core.MessageHandling
{
    public class AcquiringMessageHandler : PostilionMessageHandlerBase
    {
        private readonly IActiveAcquiringMessageHandlerTracker _tracker;

        public AcquiringMessageHandler(IActiveAcquiringMessageHandlerTracker tracker)
        {
            _tracker = tracker;
        }

        public override void OnConnected(IMessageSink messageSink)
        {
            base.OnConnected(messageSink);

            _tracker.Register(this);
        }

        public override void OnDisconnected()
        {
            base.OnDisconnected();

            _tracker.Unregister(this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public ValueTask SendMessageAsync(PostilionMessage message)
        {
            return _messageSink.SendMessageAsync(message);
        }
    }
}
