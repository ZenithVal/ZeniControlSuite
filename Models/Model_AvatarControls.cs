namespace ZeniControlSuite.Models;

public class Avatar
{
    public string ID { get; set; }
    public string Name { get; set; }
    public List<AvatarControl> Controls { get; set; }
    public Dictionary<string, Parameter> Parameters { get; set; }
}

public enum ControlType
{
    Button,
    Toggle,
    Radial,
    HSV,
}
public abstract class AvatarControl
{
    public string Name { get; set; }
    public abstract ControlType Type { get; }
    public List<string> RequiredRoles { get; set; } = new List<string>();
    public string IconPath { get; set; }
}
public class ContTypeButton : AvatarControl
{
    public override ControlType Type => ControlType.Button;
    public required Parameter Parameter { get; set; }
}
public class ContTypeToggle : AvatarControl
{
    public override ControlType Type => ControlType.Toggle;
    public required Parameter Parameter { get; set; }
    public float ValueOff { get; set; }
    public float ValueOn { get; set; }
}
public class ContTypeRadial : AvatarControl
{
    public override ControlType Type => ControlType.Radial;
    public required Parameter Parameter { get; set; }
    public double SliderValue { get => (double)Parameter.Value; set => Parameter.Value = (float)value; }
    public float ValueMin { get; set; }
    public float ValueMax { get; set; }
}

public enum HSVParamValue
{
    Hue,
    Saturation,
    Brightness
}

public class ContTypeHSV : AvatarControl
{
    public override ControlType Type => ControlType.HSV;
    public MudBlazor.Utilities.MudColor targetColor { get; set; } = new MudBlazor.Utilities.MudColor(0, 0, 0, 0);
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

public class Parameter
{
    public string Address { get; set; }
    public ParameterType Type { get; set; }
    public float Value { get; set; } 
}