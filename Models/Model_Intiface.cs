using MudBlazor;

namespace ZeniControlSuite.Models;

public enum PatternType
{
    None,
    Pulse,
    Wave
}

public enum PatternState
{
    Up,
    Down
}

public class IntifaceDevice
{
    public bool Enabled { get; set; }
    public bool Connected { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
}

public class HapticInput
{
    public required Parameter Parameter { get; set; }
    public float Min { get; set; }
    public float Max { get; set; }
    public float Exponent { get; set; }
    public float Multiplier { get; set; }
    public float Influence { get; set; }
}