namespace ZeniControlSuite.Models;

public enum ParameterType
{
    Bool,
    Int,
    Float
}

public class Parameter
{
    public Parameter() { }

    public Parameter(string address, ParameterType type, float value)
    {
        Address = address;
        Type = type;
        Value = value;
    }

    public string Address { get; set; }
    public ParameterType Type { get; set; }
    public float Value { get; set; }
}
