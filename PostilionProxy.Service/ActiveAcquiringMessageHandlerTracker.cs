using PostilionProxy.Core.MessageHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PostilionProxy.Service
{
    public class ActiveAcquiringMessageHandlerTracker : IActiveAcquiringMessageHandlerTracker
    {
        private AcquiringMessageHandler _acquiringMessageHandler;

        public void Register(AcquiringMessageHandler acquiringMessageHandler)
        {
            lock(this)
            {
                _acquiringMessageHandler = acquiringMessageHandler;
            }
        }

        public void Unregister(AcquiringMessageHandler acquiringMessageHandler)
        {
            lock (this)
            {
                if (_acquiringMessageHandler == acquiringMessageHandler)
                    _acquiringMessageHandler = null;
            }
        }

        public AcquiringMessageHandler GetCurrent()
        {
            lock (this) // todo: optimise locking e.g. reader/writer lock
            {
                return _acquiringMessageHandler;
            }
        }
    }
}
