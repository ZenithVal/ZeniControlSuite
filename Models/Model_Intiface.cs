namespace ZeniControlSuite.Models;

public enum PatternType
{
    None,
    Pulse,
    Wave,
    RampUp,
    RampDown,
    Saw,
    Sine,
    Tremor,
    Burst,
    RandomPulse
}

public enum PatternState
{
    Up,
    Down
}

public class IntifaceDevice
{
    public bool Enabled { get; set; } = true;
    public bool Connected { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class HapticInput
{
    public bool Enabled { get; set; } = true;
    public required Parameter Parameter { get; set; }
    public float Min { get; set; }
    public float Max { get; set; } = 1f;
    public float Exponent { get; set; } = 1f;
    public float Multiplier { get; set; } = 1f;
    public float Influence { get; set; } = 1f;
}
