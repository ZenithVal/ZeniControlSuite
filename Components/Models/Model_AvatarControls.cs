using System.Data.Common;
using System.Drawing;
using System.Reflection.Metadata;

namespace ZeniControlSuite.Models.AvatarControls;

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
    public double SliderValue { get => (double)Parameter.Value; set => Parameter.Value = (float)value;}
    public float ValueMin { get; set; }
    public float ValueMax { get; set; }
}
public class ContTypeHSV : Control
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
    public string Path { get; set; }
    public ParameterType Type { get; set; }
    public float Value { get; set; }
}