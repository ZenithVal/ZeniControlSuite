namespace ZeniControlSuite.Models;

public sealed class OscMessage
{
    public OscMessage(string address, params object[] arguments)
    {
        Address = address;
        Arguments = arguments?.ToList() ?? new List<object>();
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public string Address { get; }
    public List<object> Arguments { get; }
    public DateTimeOffset ReceivedAt { get; }

    public object? FirstOrDefault() => Arguments.Count > 0 ? Arguments[0] : null;

    public override string ToString()
    {
        return Arguments.Count == 0
            ? Address
            : $"{Address} {string.Join(", ", Arguments)}";
    }
}
