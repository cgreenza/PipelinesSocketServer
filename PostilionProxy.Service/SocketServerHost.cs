using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PostilionProxy.Core.MessageHandling;
using PostilionProxy.Core.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PostilionProxy.Service
{
    public class SocketServerHost : IHostedService
    {
        private PostilionSocketServer _issuingServer;
        private PostilionSocketServer _acquiringServer;

        private readonly IPEndPoint _issuingEndPoint;
        private readonly IPEndPoint _acquiringEndPoint;

        public SocketServerHost(IConfiguration config)
        {
            var issuingListenPort = int.Parse(config["AppSettings:IssuingListenPort"]);
            _issuingEndPoint = new IPEndPoint(IPAddress.Any, issuingListenPort);

            var acquiringListenPort = int.Parse(config["AppSettings:AcquiringListenPort"]);
            _acquiringEndPoint = new IPEndPoint(IPAddress.Any, acquiringListenPort);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _issuingServer = new PostilionSocketServer(() => new IssuingMessageHandler());
            _issuingServer.Listen(_issuingEndPoint);

            _acquiringServer = new PostilionSocketServer(() => new AcquiringMessageHandler());
            _acquiringServer.Listen(_acquiringEndPoint);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _issuingServer.Stop();
            _acquiringServer.Stop();

            return Task.CompletedTask;
        }
    }
}
