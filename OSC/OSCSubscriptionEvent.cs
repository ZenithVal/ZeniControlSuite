
using CoreOSC;

namespace ZeniControlSuite.OSC;
public class OSCSubscriptionEvent
{
    public OSCSubscriptionEvent(OscMessage message)
    {
        Message = message;
        Console.WriteLine($"OSC Message Received: {message.Address}");
    }
    public OscMessage Message { get; private set; }
}
