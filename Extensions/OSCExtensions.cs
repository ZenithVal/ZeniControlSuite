using ZeniControlSuite.Models;

namespace ZeniControlSuite.Extensions;

public static class OSCExtensions
{
    public static object? FormatOutGoing(float value, ParameterType type)
    {
        return type switch
        {
            ParameterType.Bool => value >= 0.5f,
            ParameterType.Int => (int)MathF.Round(value),
            ParameterType.Float => value,
            _ => null
        };
    }

    public static float FormatIncoming(object? value, ParameterType type)
    {
        if (value == null)
        {
            return 0;
        }

        try
        {
            return type switch
            {
                ParameterType.Bool => value is bool boolValue ? (boolValue ? 1 : 0) : (ToFloat(value) >= 0.5f ? 1 : 0),
                ParameterType.Int => Convert.ToInt32(value),
                ParameterType.Float => (float)Math.Truncate(ToFloat(value) * 1000) / 1000,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }

    private static float ToFloat(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? 1 : 0,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            string stringValue when float.TryParse(stringValue, out var parsed) => parsed,
            _ => Convert.ToSingle(value)
        };
    }
}
