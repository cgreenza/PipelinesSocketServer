using Pipelines.Sockets.Unofficial;
using PostilionProxy.Core.MessageHandling;
using PostilionProxy.Core.Utility;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace PostilionProxy.Core.Network
{
    // Refer Marc Gravell's blog series "Pipelines - a guided tour of the new IO API in .NET" for detailed discussion on System.IO.Pipelines
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-1.html
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-2.html
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-3.html
    // https://blog.marcgravell.com/2018/07/pipe-dreams-part-31.html

    public class PostilionSocketServer : SocketServer
    {
        // todo: https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/

        private PostilionPipeline _activePipeline;
        private Func<IMessageHandler> _handlerFactory;

        public PostilionSocketServer(Func<IMessageHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory;
        }

        protected override Task OnClientConnectedAsync(in ClientConnection client)
        {
            var newPipeline = new PostilionPipeline(client.Transport, this);
            var handler = _handlerFactory();
            return newPipeline.RunAsync(default, handler);
        }

        private void AddPipeline(PostilionPipeline pipeline)
        {
            // only allow one active connection, kill old connection:

            PostilionPipeline oldPipeline;
            lock (this)
            {
                oldPipeline = _activePipeline;
                _activePipeline = pipeline;
            }

            oldPipeline?.Dispose();
        }

        private void RemovePipeline(PostilionPipeline pipeline)
        {
            lock (this)
            {
                if (pipeline == _activePipeline)
                    _activePipeline = null;
            }

            pipeline?.Dispose();
        }

        protected override void OnServerFaulted(Exception exception)
        {
            // todo: do what?
            base.OnServerFaulted(exception);
        }

        protected override void Dispose(bool disposing)
        {
            lock (this)
            {
                _activePipeline?.Dispose();
            }

            base.Dispose(disposing);
        }


        private class PostilionPipeline : PostilionFramingPipeline, IMessageSink
        {
            private readonly PostilionSocketServer _owner;
            private IMessageHandler _messageHandler;

            public Task RunAsync(CancellationToken cancellationToken, IMessageHandler messageHandler)
            {
                _messageHandler = messageHandler;
                return StartReceiveLoopAsync(cancellationToken);
            }

            public PostilionPipeline(IDuplexPipe pipe, PostilionSocketServer owner) : base(pipe)
            {
                _owner = owner;
            }

            protected sealed override ValueTask OnReceiveAsync(ReadOnlySequence<byte> payload)
            {
                //void DisposeOnCompletion(/*Value*/Task task, ref IMemoryOwner<byte> message)
                //{
                //    task/*.AsTask()*/.ContinueWith((t, s) => ((IMemoryOwner<byte>)s)?.Dispose(), message);
                //    message = null; // caller no longer owns it, logically; don't wipe on exit
                //}

                var msg = payload.Lease(); // copy to buffer pool, ProcessAsync must release it

                // https://devblogs.microsoft.com/pfxteam/task-run-vs-task-factory-startnew/
                Task.Run(() =>
                {
                    PostilionMessage pm;
                    try
                    {
                        pm = new PostilionMessage();
                        pm.ParseFromBuffer(msg.Memory.Span);
                    }
                    finally
                    {
                        msg.Dispose(); // release back to buffer pool
                    }

                    _messageHandler.ProcessMessage(pm); // todo: make async?
                }); // parse & process message on worker thread, let this thread read next message from pipeline

                //try
                //{
                //var pendingAction = _sink.ProcessAsync(msg);
                //if (!pendingAction.IsCompletedSuccessfully)
                //    DisposeOnCompletion(pendingAction, ref msg);
                //}
                //finally
                //{   // might have been wiped if we went async
                //    msg?.Dispose();
                //}
                return default;
            }

            public ValueTask SendMessageAsync(PostilionMessage message)
            {
                // todo: check MemoryOwner.LeakCount

                var buffer = ArrayPool<byte>.Shared.Rent(1024); // todo: decide on max size
                int length;
                try
                {
                    length = message.WriteToBuffer(new Span<byte>(buffer));
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    throw;
                }

                var owner = buffer.Lease(length); // wrap buffer in IMemoryOwner so that it can be released back to pool later
                return WriteAsync(owner); // will release memory back to pool when done
            }

            protected override ValueTask OnStartReceiveLoopAsync()
            {
                _owner.AddPipeline(this);
                _messageHandler.OnConnected(this);
                return default;
            }

            protected override ValueTask OnEndReceiveLoopAsync()
            {
                _messageHandler.OnDisconnected();
                _owner.RemovePipeline(this);
                return default;
            }

            protected override void Dispose(bool disposing)
            {
                (_messageHandler as IDisposable)?.Dispose();
                base.Dispose(disposing);
            }
        }
    }

}
