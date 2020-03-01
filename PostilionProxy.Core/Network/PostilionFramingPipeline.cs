using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace PostilionProxy.Core.Network
{
    // adapted from https://github.com/mgravell/simplsockets/blob/master/SimplPipelines/SimplPipeline.cs

    /// <summary>
    ///  Pipeline that applies Postilion header (2 bytes big-endian payload length excluding header) to incoming and outgoing messages
    /// </summary>
    public abstract class PostilionFramingPipeline : IDisposable
    {
        private readonly SemaphoreSlim _singleWriter = new SemaphoreSlim(1);

        private IDuplexPipe _pipe;

        protected PostilionFramingPipeline(IDuplexPipe pipe)
            => _pipe = pipe;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Close();
        }

        public void Close(Exception ex = null)
        {
            var pipe = _pipe;
            _pipe = null;

            if (pipe != null)
            {
                // burn the pipe to the ground
                try { pipe.Input.Complete(ex); } catch { }
                try { pipe.Input.CancelPendingRead(); } catch { }
                try { pipe.Output.Complete(ex); } catch { }
                try { pipe.Output.CancelPendingFlush(); } catch { }
                if (pipe is IDisposable d) try { d.Dispose(); } catch { }
            }
            try { _singleWriter.Dispose(); } catch { }
        }

        void WriteHeader(PipeWriter writer, int length)
        {
            var span = writer.GetSpan(2);
            BinaryPrimitives.WriteUInt16BigEndian(span, checked((ushort)length));
            writer.Advance(2);
        }

        protected ValueTask WriteAsync(IMemoryOwner<byte> memory)
        {
            async ValueTask Awaited(IMemoryOwner<byte> mmemory, ValueTask write)
            {
                using (mmemory)
                {
                    await write;
                }
            }
            try
            {
                var result = WriteAsync(memory.Memory);
                if (result.IsCompletedSuccessfully) return default;
                var final = Awaited(memory, result);
                memory = null; // prevent dispose
                return final;
            }
            finally
            {
                using (memory) { }
            }
        }
        /// <summary>
        /// Note: it is assumed that the calling subclass has dealt with synchronization
        /// </summary>
        protected ValueTask WriteAsync(ReadOnlyMemory<byte> payload)
        {
            async ValueTask AwaitFlushAndRelease(ValueTask<FlushResult> flush)
            {
                try { await flush; }
                finally { _singleWriter.Release(); }
            }
            // try to get the conch; if not, switch to async
            if (!_singleWriter.Wait(0)) return WriteAsyncSlowPath(payload);
            bool release = true;
            try
            {
                var writer = _pipe?.Output ?? throw new ObjectDisposedException(ToString());
                WriteHeader(writer, payload.Length);
                var write = writer.WriteAsync(payload); // includes a flush
                if (write.IsCompletedSuccessfully) return default; // sync fast path
                release = false;
                return AwaitFlushAndRelease(write);
            }
            finally
            {
                if (release) _singleWriter.Release();
            }
        }
        async ValueTask WriteAsyncSlowPath(ReadOnlyMemory<byte> payload)
        {
            await _singleWriter.WaitAsync();
            try
            {
                var writer = _pipe?.Output ?? throw new ObjectDisposedException(ToString());
                WriteHeader(writer, payload.Length);
                await writer.WriteAsync(payload); // includes a flush
            }
            finally
            {
                _singleWriter.Release();
            }
        }

        protected abstract ValueTask OnReceiveAsync(ReadOnlySequence<byte> payload);

        static int ParseFrameHeader(ReadOnlySpan<byte> input)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(input);
        }

        private bool TryParseFrame(ref ReadOnlySequence<byte> input,
            out ReadOnlySequence<byte> payload)
        {
            if (input.Length < 2)
            {   // not enough data for the header
                payload = default;
                return false;
            }

            int length;
            if (input.First.Length >= 2)
            {
                length = ParseFrameHeader(input.First.Span);
            }
            else
            {
                Span<byte> local = stackalloc byte[2];
                input.Slice(0, 2).CopyTo(local);
                length = ParseFrameHeader(local);
            }

            // do we have the "length" bytes?
            if (input.Length < length + 2)
            {
                payload = default;
                return false;
            }

            // success!
            payload = input.Slice(2, length);
            input = input.Slice(payload.End);
            return true;
        }


        protected async Task StartReceiveLoopAsync(CancellationToken cancellationToken = default)
        {
            var reader = _pipe?.Input ?? throw new ObjectDisposedException(ToString());
            try
            {
                await OnStartReceiveLoopAsync();
                bool makingProgress = false;
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!(makingProgress && reader.TryRead(out var readResult)))
                        readResult = await reader.ReadAsync(cancellationToken);
                    if (readResult.IsCanceled) break;

                    var buffer = readResult.Buffer;

                    // handle as many frames from the data as we can
                    // (note: an alternative strategy is handle one frame
                    // and release via AdvanceTo as soon as possible)
                    makingProgress = false;
                    while (TryParseFrame(ref buffer, out var payload))
                    {
                        makingProgress = true;
                        await OnReceiveAsync(payload);
                    }

                    // record that we comsumed up to the (now updated) buffer.Start,
                    // and tried to look at everything - hence buffer.End
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    // exit the loop electively, or because we've consumed everything
                    // that we can usefully consume
                    if (!makingProgress && readResult.IsCompleted) break;
                }
                try { reader.Complete(); } catch { }
            }
            catch (Exception ex)
            {
                try { reader.Complete(ex); } catch { }
            }
            finally
            {
                try { await OnEndReceiveLoopAsync(); } catch { }
            }
        }

        protected virtual ValueTask OnStartReceiveLoopAsync() => default;
        protected virtual ValueTask OnEndReceiveLoopAsync() => default;
    }
}
