﻿using CoreOSC;

namespace ZeniControlSuite.Services;

public class OSCSubscriptionEvent : EventArgs
{
    public OSCSubscriptionEvent(OscMessage message)
    {
        Message = message;
    }
    public OscMessage Message { get; private set; }
}