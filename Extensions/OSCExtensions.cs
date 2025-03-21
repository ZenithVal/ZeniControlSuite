using ZeniControlSuite.Models;

namespace ZeniControlSuite.Extensions;

public static class OSCExtensions
{
    public static object? FormatOutGoing(float value, ParameterType type)
    {
        switch (type)
        {
            case ParameterType.Bool:
                return value < 0.5 ? false : true;
            case ParameterType.Int:
                return (int)value;
            case ParameterType.Float:
                return value;
            default:
                return null;
        }
    }

    public static float FormatIncoming(object value, ParameterType type)
    {
        switch (type)
        {
            case ParameterType.Bool:
                if (value is bool)
                {
                    return (bool)value ? 1 : 0;
                }
                else //Sometimes paramters are mismatched. Dont want to explode.
                {
                    return (float)value < 0.5 ? 0 : 1;
                }
            case ParameterType.Int:
                return (int)value;
            case ParameterType.Float:
                return (float)Math.Truncate((float)value * 1000) / 1000;
            default:
                return 0;
        }
    }
}

