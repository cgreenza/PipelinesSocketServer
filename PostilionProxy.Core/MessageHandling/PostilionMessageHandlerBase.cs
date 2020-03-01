using System;

namespace PostilionProxy.Core.MessageHandling
{
    public class PostilionMessageHandlerBase : IMessageHandler, IDisposable
    {
        protected IMessageSink _messageSink;

        public virtual void OnConnected(IMessageSink messageSink)
        {
            _messageSink = messageSink;

            // todo: remove (sends "welcome" message for testing)
            var m = new PostilionMessage();
            m.ParseFromBuffer(System.Text.Encoding.ASCII.GetBytes("Hello from server. Test 1234567890"));
            _messageSink.SendMessageAsync(m);
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
            _messageSink.SendMessageAsync(message); // todo: await
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
