using System;
using System.Buffers.Binary;
using System.IO;

namespace PostilionProxy.Core.MessageHandling
{
    // todo: much optimisation still possible to reduce memory copying and object/string allocations
    public class PostilionMessage
    {
        // todo: remove this temporary implementation
        private byte[] _testData = new byte[0];
        public byte[] TestData { get => _testData; set => _testData = value; }

        // todo: indexer into fields
        public PostilionMessage()
        {

        }

        internal void ParseFromBuffer(ReadOnlySpan<byte> buffer)
        {
            _testData = buffer.ToArray(); // just for testing

            // todo: ISO parsing
            // todo: should rather use following for parsing
            // buffer.Slice
            // BinaryPrimitives.Read..(buffer) for parsing
            // System.Text.Encoding.ASCII.GetString(buffer)
            // MemoryOwner.Decode
        }

        internal int WriteToBuffer(Span<byte> memory)
        {
            // todo: ISO writing
            // BinaryPrimitives.Write...(span, )
            // MemoryOwner.Encode

            _testData.CopyTo(memory); // just for testing
            return _testData.Length; // length of buffer used
        }

        //public PostilionMessage(IMemoryOwner<byte> msg)
        //{
        //    // todo: deserialize
        //    // todo: msg.Dispose();
        //}

        //public byte[] Serialize()
        //{

        //}
    }

}
