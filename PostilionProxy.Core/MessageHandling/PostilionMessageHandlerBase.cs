using System;

namespace PostilionProxy.Core.MessageHandling
{
    public class PostilionMessageHandlerBase : IMessageHandler, IDisposable
    {
        protected IMessageSink _messageSink;

        public virtual void OnConnected(IMessageSink messageSink)
        {
            _messageSink = messageSink;
        }

        public virtual void OnDisconnected()
        {
        }

        public void ProcessMessage(PostilionMessage message)
        {
            // todo: add all logic here e.g. handshake, state machine, etc.
            // todo: does exception here kill the connection?
            // todo: reply message using SendMessage(new PostilionMessage())

            // todo: pass on to derived classes for processing

            // echo message back for testing
            _messageSink.SendMessage(message);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

    }
}
