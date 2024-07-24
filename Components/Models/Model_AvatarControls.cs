namespace ZeniControlSuite.Components.Avatars;

public class Avatar
{
    public string ID { get; set; }
    public string Name { get; set; }
    public List<Control> Controls { get; set; }
}

public enum ControlType
{
    Button,
    Toggle,
    Radial,
    HSV,
}
public abstract class Control
{
    public string Name { get; set; }
    public abstract ControlType Type { get; }
    public List<String> RequiredRoles { get; set; } = new List<String>();
}
public class ContTypeButton : Control
{
    public override ControlType Type => ControlType.Button;
    public required Parameter Parameter { get; set; }
}
public class ContTypeToggle : Control
{
    public override ControlType Type => ControlType.Toggle;
    public required Parameter Parameter { get; set; }
    public float ValueOff { get; set; }
    public float ValueOn { get; set; }
}
public class ContTypeRadial : Control
{
    public override ControlType Type => ControlType.Radial;
    public required Parameter Parameter { get; set; }
    public float ValueMin { get; set; }
    public float ValueMax { get; set; }
}
public class ContTypeHSV : Control
{
    public override ControlType Type => ControlType.HSV;
    public required Parameter ParameterHue { get; set; }
    public required Parameter ParameterSaturation { get; set; }
    public required Parameter ParameterBrightness { get; set; }
    public bool InvertedBrightness { get; set; }
}


public enum ParameterType
{
    Bool,
    Int,
    Float
}
public abstract class Parameter
{
    public string Path { get; set; }
    public abstract ParameterType Type { get; }
}
public class ParamTypeBool : Parameter
{
    public override ParameterType Type => ParameterType.Bool;
    public bool Value { get; set; }
}
public class ParamTypeInt : Parameter
{
    public override ParameterType Type => ParameterType.Int;
    public int Value { get; set; }
}
public class ParamTypeFloat : Parameter
{
    public override ParameterType Type => ParameterType.Float;
    public float Value { get; set; }
}