using System.Threading.Tasks;

namespace PostilionProxy.Core.MessageHandling
{
    public interface IMessageSink
    {
        ValueTask SendMessage(PostilionMessage message);
    }

}
