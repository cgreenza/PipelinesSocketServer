using NUnit.Framework;
using PostilionProxy.Core.MessageHandling;
using PostilionProxy.Core.Network;
using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PostilionProxy.Test
{
    public class SocketServerTests
    {
        private static TimeSpan _DefaultTimeout = TimeSpan.FromSeconds(5);

        [SetUp]
        public void Setup()
        {
        }

        private class TestMessageHandlerFactory
        {
            private AutoResetEvent _onCreateNewEvent = new AutoResetEvent(false);
            public TestMessageHandler LatestHandler { get; private set; }

            public TestMessageHandler CreateNew()
            {
                LatestHandler = new TestMessageHandler();
                _onCreateNewEvent.Set();
                return LatestHandler;
            }

            public void WaitForMessageHandlerCreated()
            {
                if (!_onCreateNewEvent.WaitOne(_DefaultTimeout))
                    throw new Exception("Timeout waiting for message handler create");
            }

            public void AssertNoWaitingEvents()
            {
                Assert.IsFalse(_onCreateNewEvent.WaitOne(0));
            }
        }

        private class TestMessageHandler : IMessageHandler, IDisposable
        {
            private AutoResetEvent _onConnectedEvent = new AutoResetEvent(false);
            private AutoResetEvent _onDisconnectedEvent = new AutoResetEvent(false);
            private AutoResetEvent _onProcessMessageEvent = new AutoResetEvent(false);

            public IMessageSink Sink { get; private set; }
            public bool DisposeCalled { get; private set; }
            public PostilionMessage LastMessage { get; private set; }

            public void OnConnected(IMessageSink sink)
            {
                Sink = sink;
                _onConnectedEvent.Set();
            }

            public void OnDisconnected()
            {
                _onDisconnectedEvent.Set();
            }

            public void ProcessMessage(PostilionMessage message)
            {
                LastMessage = message;
                _onProcessMessageEvent.Set();
            }

            public void WaitForConnect()
            {
                if (!_onConnectedEvent.WaitOne(_DefaultTimeout))
                    throw new Exception("Timeout waiting for connect");
            }

            public void WaitForDisconnect()
            {
                if (!_onDisconnectedEvent.WaitOne(_DefaultTimeout))
                    throw new Exception("Timeout waiting for disconnect");
            }

            public void WaitForMessage()
            {
                if (!_onProcessMessageEvent.WaitOne(_DefaultTimeout))
                    throw new Exception("Timeout waiting for message");
            }

            public void AssertNoWaitingEvents()
            {
                Assert.IsFalse(_onConnectedEvent.WaitOne(0));
                Assert.IsFalse(_onDisconnectedEvent.WaitOne(0));
                Assert.IsFalse(_onProcessMessageEvent.WaitOne(0));
            }

            public void Dispose()
            {
                DisposeCalled = true;
            }
        }

        private PostilionSocketServer StartServer(TestMessageHandlerFactory testHandlerFactory)
        {
            var server = new PostilionSocketServer(testHandlerFactory.CreateNew);
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
            return server;
        }

        private Socket ConnectSocket()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
            return socket;
        }


        [Test]
        public void CloseSocketDisconnectsHandler()
        {
            var testHandlerFactory = new TestMessageHandlerFactory();

            using (var server = StartServer(testHandlerFactory))
            {
                var socket = ConnectSocket();

                testHandlerFactory.WaitForMessageHandlerCreated();
                var latestHandler = testHandlerFactory.LatestHandler;

                latestHandler.WaitForConnect();
                latestHandler.AssertNoWaitingEvents();

                socket.Close();

                latestHandler.WaitForDisconnect();
                latestHandler.AssertNoWaitingEvents();
                Thread.Sleep(100);
                Assert.IsTrue(latestHandler.DisposeCalled);

                testHandlerFactory.AssertNoWaitingEvents();
            }
        }

        [Test]
        public void NewSocketDisconnectsOldHandler()
        {
            var testHandlerFactory = new TestMessageHandlerFactory();

            using (var server = StartServer(testHandlerFactory))
            {
                var socket1 = ConnectSocket();
                testHandlerFactory.WaitForMessageHandlerCreated();
                var handler1 = testHandlerFactory.LatestHandler;
                handler1.WaitForConnect();
                handler1.AssertNoWaitingEvents();

                var socket2 = ConnectSocket();
                testHandlerFactory.WaitForMessageHandlerCreated();
                var handler2 = testHandlerFactory.LatestHandler;
                handler2.WaitForConnect();
                handler2.AssertNoWaitingEvents();

                handler1.WaitForDisconnect();
                handler1.AssertNoWaitingEvents();
                Assert.IsTrue(handler1.DisposeCalled);

                testHandlerFactory.AssertNoWaitingEvents();
            }
        }


        [Test]
        public void ReceiveEmptyMessage_SendEmptyResponse()
        {
            var testHandlerFactory = new TestMessageHandlerFactory();

            using (var server = StartServer(testHandlerFactory))
            {
                var socket = ConnectSocket();

                testHandlerFactory.WaitForMessageHandlerCreated();
                var latestHandler = testHandlerFactory.LatestHandler;

                latestHandler.WaitForConnect();
                latestHandler.AssertNoWaitingEvents();

                // socket => server:
                socket.Send(new byte[] { 0x00, 0x01, 0xFF }); // 1 length message
                latestHandler.WaitForMessage();
                var msg = latestHandler.LastMessage;
                latestHandler.AssertNoWaitingEvents();

                // server => socket:
                latestHandler.Sink.SendMessageAsync(msg).AsTask().Wait();
                var response = ReadPayloadFromSocket(socket);
                Assert.AreEqual(1, response.Length);
                Assert.AreEqual(0xFF, response[0]);

                latestHandler.AssertNoWaitingEvents();
                testHandlerFactory.AssertNoWaitingEvents();
            }
        }

        private byte[] ReadPayloadFromSocket(Socket socket)
        {
            var buffer = new byte[1000];
            var lengthRead = 0;

            socket.ReceiveTimeout = checked((int)_DefaultTimeout.TotalMilliseconds);
            while (true)
            {
                var l = socket.Receive(buffer, lengthRead, buffer.Length - lengthRead, SocketFlags.None);
                lengthRead += l;

                if (lengthRead >= 2)
                {
                    var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan().Slice(0, 2));
                    if (lengthRead == 2 + payloadLength)
                        return buffer.AsSpan().Slice(2, payloadLength).ToArray(); // only return payload
                    if (lengthRead > 2 + payloadLength)
                        throw new Exception("Read too much");
                }

            }
        }

        // todo: parsing error
        // todo: send message (without first receiving message)
    }
}