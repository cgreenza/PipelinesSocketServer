using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PostilionProxy.Core.MessageHandling;

namespace PostilionProxy.Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcquiringController : ControllerBase
    {
        private readonly ILogger<AcquiringController> _logger;
        private readonly IActiveAcquiringMessageHandlerTracker _acquiringTracker;

        public AcquiringController(ILogger<AcquiringController> logger, IActiveAcquiringMessageHandlerTracker acquiringTracker)
        {
            _logger = logger;
            _acquiringTracker = acquiringTracker;
        }

        [HttpPost]
        [Route("SendMessage")]
        public async Task<ActionResult> SendMessage(PostilionMessage message)
        {
            var activeAcquiringConnection = _acquiringTracker.GetCurrent();
            if (activeAcquiringConnection == null) // no active acquiring connection
                return new BadRequestResult(); // todo: better error

            await activeAcquiringConnection.SendMessageAsync(message);

            return new OkResult();
        }
    }
}
