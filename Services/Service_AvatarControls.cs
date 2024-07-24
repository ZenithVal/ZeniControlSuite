using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Components.Avatars;

namespace ZeniControlSuite.Services;
public class Service_AvatarControls : IHostedService
{
    [Inject] private Service_Logs LogService { get; set; } = default!;
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

    private void InitializeAvatarControls()
    {
        try
        {
            var jsonString = File.ReadAllText("AvatarControls.json");
            ReadAvatarControlsJson(jsonString);
        }
        catch (Exception e)
        {
            Log("AvatarControls.json parsing failed: " + e.Message, Severity.Error);
        }
    }

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
            foreach (var controlName in inheritedControlsElement.EnumerateArray())
            {
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
            case "Slider":
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
                    ParamterBrightness = DeserializeParameter(controlElement.GetProperty("ParamterBrightness")),
                    InvertedBrightness = controlElement.GetProperty("InvertedBrightness").GetBoolean()
                };
                break;
            default:
                throw new InvalidOperationException("Unknown control type.");
        }

        return control;
    }

    private Parameter DeserializeParameter(JsonElement parameterElement)
    {
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

