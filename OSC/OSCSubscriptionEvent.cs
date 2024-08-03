
using CoreOSC;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.OSC;
public class OSCSubscriptionEvent : EventArgs
{
    public OSCSubscriptionEvent(OscMessage message, Service_AvatarControls AvatarsService)
    {

    }


}
