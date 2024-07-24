using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Components.Avatars;
using ZeniControlSuite.Components.Pages;

namespace ZeniControlSuite.Services;
public class Service_AvatarControls : IHostedService
{
    private readonly Service_Logs LogService;
    public Service_AvatarControls(Service_Logs serviceLogs){LogService = serviceLogs;}
    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_AvatarControls", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
    public delegate void RequestAvatarsUpdate();
    public event RequestAvatarsUpdate? OnAvatarsUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeAvatarControls();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public void InvokeAvatarControlsUpdate()
    {
        if (OnAvatarsUpdate != null)
        {
            OnAvatarsUpdate.Invoke();
        }
    }
    #endregion


    //===========================================//
    #region Settings
    public List<Control> globalControls = new List<Control>();
    public List<Avatar> avatars = new List<Avatar>();
    #endregion


    //===========================================//
    #region Initialization & Avatar Controls

    private string validationLog = "";
    private void InitializeAvatarControls()
    {
        try
        {
            var jsonString = File.ReadAllText("Configs/AvatarControls.json");
            ReadAvatarControlsJson(jsonString);
            Log("Service Started", Severity.Normal);
            Console.WriteLine("");
        }
        catch (Exception e)
        {
            Log($"AvatarControls.json parsing failed during {validationLog}", Severity.Error);
            Console.WriteLine(e.Message);
        }
    }
    //Error help
    //The given key was not present in the dictionary = Parameter## was spelled incorrectly ~ EG HSV "PUramterSatAration"

    public void ReadAvatarControlsJson(string jsonString)
    {
        // Deserialize the JSON string
        var jsonDocument = JsonDocument.Parse(jsonString);

        // Deserialize GlobalControls
        var globalControlsElement = jsonDocument.RootElement.GetProperty("GlobalControls");
        foreach (var controlElement in globalControlsElement.EnumerateArray())
        {
            var control = DeserializeControl(controlElement);
            globalControls.Add(control);
        }

        // Deserialize Avatars
        var avatarsElement = jsonDocument.RootElement.GetProperty("Avatars");
        foreach (var avatarElement in avatarsElement.EnumerateArray())
        {
            Console.WriteLine($"AC | Loading Avatar {avatarElement.GetProperty("Name").GetString()}");
            validationLog = $"loading avatar {avatarElement.GetProperty("Name").GetString()}";
            var avatar = new Avatar {
                ID = avatarElement.GetProperty("ID").GetString(),
                Name = avatarElement.GetProperty("Name").GetString(),
                Controls = new List<Control>()
            };

            // Deserialize Avatar Controls
            var controlsElement = avatarElement.GetProperty("Controls");
            foreach (var controlElement in controlsElement.EnumerateArray())
            {
                var control = DeserializeControl(controlElement);
                avatar.Controls.Add(control);
            }

            // Deserialize Inherited Global Controls
            var inheritedControlsElement = avatarElement.GetProperty("InheritedGlobalControls");
            Console.WriteLine($"AC | Loading inherited controls");
            foreach (var controlName in inheritedControlsElement.EnumerateArray())
            {
                validationLog = $"loading inherited control {controlName.GetString()}";
                    
                var globalControl = globalControls.FirstOrDefault(c => c.GetType().Name == controlName.GetString());
                if (globalControl != null)
                {
                    avatar.Controls.Add(globalControl);
                }
            }

            avatars.Add(avatar);
        }
    }

    private Control DeserializeControl(JsonElement controlElement)
    {
        Console.WriteLine($"AC | Deserializing Control {controlElement.GetProperty("Name").GetString()}");
        validationLog = $"deserializing control {controlElement.GetProperty("Name").GetString()}";
        var type = controlElement.GetProperty("Type").GetString();
        Control control;

        switch (type)
        {
            case "Button":
                control = new ContTypeButton {
                    Parameter = DeserializeParameter(controlElement.GetProperty("Parameter"))
                };
                break;
            case "Toggle":
                control = new ContTypeToggle {
                    Parameter = DeserializeParameter(controlElement.GetProperty("Parameter")),
                    ValueOff = controlElement.GetProperty("ValueOff").GetSingle(),
                    ValueOn = controlElement.GetProperty("ValueOn").GetSingle()
                };
                break;
            case "Radial":
                control = new ContTypeRadial {
                    Parameter = DeserializeParameter(controlElement.GetProperty("Parameter")),
                    ValueMin = controlElement.GetProperty("ValueMin").GetSingle(),
                    ValueMax = controlElement.GetProperty("ValueMax").GetSingle()
                };
                break;
            case "HSV":
                control = new ContTypeHSV {
                    ParameterHue = DeserializeParameter(controlElement.GetProperty("ParameterHue")),
                    ParameterSaturation = DeserializeParameter(controlElement.GetProperty("ParameterSaturation")),
                    ParameterBrightness = DeserializeParameter(controlElement.GetProperty("ParameterBrightness")),
                    InvertedBrightness = controlElement.GetProperty("InvertedBrightness").GetBoolean()
                };
                break;
            default:
                throw new InvalidOperationException($"Unknown control type: {type}");
        }

        return control;
    }

    private Parameter DeserializeParameter(JsonElement parameterElement)
    {
        Console.WriteLine($"AC | Deserializing Param {parameterElement.GetProperty("Path").GetString()}");
        validationLog = $"deserializing param {parameterElement.GetProperty("Path").GetString()}";
        var type = parameterElement.GetProperty("Type").GetString();
        Parameter parameter;

        switch (type)
        {
            case "Bool":
                parameter = new ParamTypeBool {
                    Path = parameterElement.GetProperty("Path").GetString(),
                    Value = parameterElement.GetProperty("Value").GetBoolean()
                };
                break;
            case "Int":
                parameter = new ParamTypeInt {
                    Path = parameterElement.GetProperty("Path").GetString(),
                    Value = parameterElement.GetProperty("Value").GetInt32()
                };
                break;
            case "Float":
                parameter = new ParamTypeFloat {
                    Path = parameterElement.GetProperty("Path").GetString(),
                    Value = parameterElement.GetProperty("Value").GetSingle()
                };
                break;
            default:
                Log("Unknown parameter type", Severity.Error);
                throw new InvalidOperationException("Unknown parameter type");
        }

        return parameter;
    }
    #endregion


    //===========================================//
}

