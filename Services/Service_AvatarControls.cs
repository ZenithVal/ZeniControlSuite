using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using ZeniControlSuite.Components.Pages;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;
public class Service_AvatarControls : IHostedService
{
    private readonly Service_Logs LogService;
    public Service_AvatarControls(Service_Logs serviceLogs) { LogService = serviceLogs; }
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
    #region Settings/Variables
    public bool avatarsLoaded = false;
    public List<Avatar> avatars = new List<Avatar>();

    public Avatar selectedAvatar = new Avatar();
    #endregion


    //===========================================//
    #region Initialization
    private string validationLog = "";
    private void InitializeAvatarControls()
    {
        try
        {
            var jsonString = File.ReadAllText("Configs/AvatarControls.json");
            ReadAvatarControlsJson(jsonString);
            Log("Service Started", Severity.Normal);
            Console.WriteLine("");
            InvokeAvatarControlsUpdate();
        }
        catch (Exception e)
        {
            Log($"AvatarControls.json parsing failed during {validationLog}", Severity.Error);
            Console.WriteLine(e.Message);
            avatarsLoaded = false;
            InvokeAvatarControlsUpdate();
        }
    }
    //Error help
    //The given key was not present in the dictionary = Parameter## was spelled incorrectly ~ EG HSV "PUramterSatAration"

    public void ReadAvatarControlsJson(string jsonString)
    {
        avatarsLoaded = false;
        List<AvatarControl> globalControls = new List<AvatarControl>();
        avatars.Clear();

        // Deserialize the JSON string
        var jsonDocument = JsonDocument.Parse(jsonString);

        // Deserialize GlobalControls
        var globalControlsElement = jsonDocument.RootElement.GetProperty("GlobalControls");
        foreach (var controlElement in globalControlsElement.EnumerateArray())
        {
            var control = DeserializeControl(controlElement);
            globalControls.Add(control);
        }

        //Sort congrolGroups
        //Any controls with - are grouped. Strip the text before " - " and add the item to the group
        //TODO


        // Deserialize Avatars
        var avatarsElement = jsonDocument.RootElement.GetProperty("Avatars");
        foreach (var avatarElement in avatarsElement.EnumerateArray())
        {
            Console.WriteLine($"AC | Loading Avatar {avatarElement.GetProperty("Name").GetString()}");
            validationLog = $"loading avatar {avatarElement.GetProperty("Name").GetString()}";
            var avatar = new Avatar {
                ID = avatarElement.GetProperty("ID").GetString(),
                Name = avatarElement.GetProperty("Name").GetString(),
                Controls = new List<AvatarControl>(),
                Parameters = new Dictionary<string, Parameter>()
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

                //find a global control with the same name
                var globalControl = globalControls.FirstOrDefault(c => c.Name == controlName.GetString());

                if (globalControl != null)
                {
                    avatar.Controls.Add(globalControl);
                }
            }


            avatars.Add(avatar);
        }

        foreach (var avatar in avatars)
        {
            CreateAvatarParamList(avatar);
        }



        //if no avatar Exists with ID Global, create one
        if (!avatars.Any(a => a.ID == "Global"))
        {
            var globalAvatar = new Avatar {
                ID = "Global",
                Name = "Global",
                Controls = globalControls,
                Parameters = new Dictionary<string, Parameter>()
            };
            avatars.Add(globalAvatar);
        }
        SelectAvatar("Global");

        avatarsLoaded = true;
        InvokeAvatarControlsUpdate();
    }

    private void CreateAvatarParamList(Avatar avatar)
    {
        foreach (var control in avatar.Controls)
        {
            if (control is ContTypeButton contButton)
            {
                AddToParamDictionary(avatar, contButton.Parameter);
            }
            else if (control is ContTypeToggle contToggle)
            {
                AddToParamDictionary(avatar, contToggle.Parameter);
            }
            else if (control is ContTypeRadial contRadial)
            {
                AddToParamDictionary(avatar, contRadial.Parameter);
            }
            else if (control is ContTypeHSV contHSV)
            {
                AddToParamDictionary(avatar, contHSV.ParameterHue);
                AddToParamDictionary(avatar, contHSV.ParameterSaturation);
                AddToParamDictionary(avatar, contHSV.ParameterBrightness);
            }
        }
    }

    private void AddToParamDictionary(Avatar avatar, Parameter parameter)
    {
        if (!avatar.Parameters.ContainsKey(parameter.Address))
        {
            avatar.Parameters.Add(parameter.Address, parameter);
        }
        else
        {
            Log($"Parameter {parameter.Address} already exists in {avatar.Name}", Severity.Warning);
        }

    }

    private AvatarControl DeserializeControl(JsonElement controlElement)
    {
        string controlName = controlElement.GetProperty("Name").GetString();
        Console.WriteLine($"AC | Deserializing Control {controlName}");
        validationLog = $"deserializing control type of {controlName}";

        var type = controlElement.GetProperty("Type").GetString();
        AvatarControl control;

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
                    InvertedBrightness = controlElement.GetProperty("InvertedBrightness").GetBoolean(),
                };
                if (control is ContTypeHSV contHSV)
                {
                    if (contHSV.InvertedBrightness)
                    {
                        contHSV.InvertedBrightnessValue = Math.Abs(1 - contHSV.ParameterBrightness.Value);
                    }
                    contHSV.targetColor = HSVControlToMudColor(contHSV);
                }
                break;
            default:
                throw new InvalidOperationException($"Unknown control type: {type}");
        }

        validationLog = $"deserializing control name of {controlName}";
        control.Name = controlElement.GetProperty("Name").GetString();

        validationLog = $"deserializing control roles of {controlName}";
        control.RequiredRoles = controlElement.GetProperty("RequiredRoles").EnumerateArray().Select(r => r.GetString()).ToList();

        if (!Directory.Exists("Images"))
        {
            Directory.CreateDirectory("Images");
        }

        //if /api/ImageController/{controlName} exists, set IconPath to that, otherwise leave blank
        string controlNameNoSpaces = controlName.Replace(" ", "");
        if (File.Exists($"Images/{controlNameNoSpaces}.png"))
        {
            Console.WriteLine($"AC | Found image for {controlName}");
            control.IconPath = $"/api/Images/{controlNameNoSpaces}.png";
        }
        else
        {
            Console.WriteLine($"AC | No image found for {controlNameNoSpaces}");
            control.IconPath = "images/PowerButton.png";
        }

        return control;
    }

    private Parameter DeserializeParameter(JsonElement parameterElement)
    {
        var parameter = new Parameter();

        Console.WriteLine($"AC | Deserializing Param {parameterElement.GetProperty("Path").GetString()}");
        validationLog = $"deserializing param {parameterElement.GetProperty("Path").GetString()}";
        parameter.Address = parameterElement.GetProperty("Path").GetString();
        var type = parameterElement.GetProperty("Type").GetString();

        switch (type)
        {
            case "Bool":
                parameter.Type = ParameterType.Bool;
                parameter.Value = parameterElement.GetProperty("Value").GetBoolean() ? 1 : 0;
                break;
            case "Int":
                parameter.Type = ParameterType.Int;
                parameter.Value = parameterElement.GetProperty("Value").GetInt32();
                break;
            case "Float":
                parameter.Type = ParameterType.Float;
                parameter.Value = parameterElement.GetProperty("Value").GetSingle();
                break;
            default:
                Log("Unknown parameter type", Severity.Error);
                throw new InvalidOperationException("Unknown parameter type");
        }

        return parameter;
    }
    #endregion


    //===========================================//
    #region Avatar Functions
    public void SelectAvatar(string avatarID)
    {
        if (selectedAvatar.ID == avatarID)
        {
            return;
        }

        if (avatars.Any(a => a.ID == avatarID))
        {
            selectedAvatar = avatars.FirstOrDefault(a => a.ID == avatarID);
            Log($"Selected avatar {selectedAvatar.Name}", Severity.Normal);

            InvokeAvatarControlsUpdate();
        }
        else
        {
            //tuncate avatarID to 13 characters for log
            string avatarIDTruncated = avatarID.Length > 13 ? avatarID.Substring(0, 13) : avatarID;

            Log($"No avatar for {avatarIDTruncated} found", Severity.Warning);
            selectedAvatar = avatars.FirstOrDefault(a => a.ID == "Global");
            Log($"Selected avatar Global", Severity.Normal);

            InvokeAvatarControlsUpdate();
        }
    }
    #endregion


    //===========================================//
    #region Helper function
    public void UpdateParameterValue(Parameter param, float value) //Used for incoming OSC messages. Updates the param in the app and invokes an update for visuals.
    {
        selectedAvatar.Parameters[param.Address].Value = value;
        InvokeAvatarControlsUpdate();
    }

    public MudColor HSVControlToMudColor(ContTypeHSV control)
    {
        float H = control.ParameterHue.Value * 360;
        float S = Math.Clamp(control.ParameterSaturation.Value, 0.001f, 0.999f);
        float V = Math.Clamp(control.ParameterBrightness.Value, 0.001f, 0.999f);

        if (control.InvertedBrightness)
        {
            V = Math.Clamp(control.InvertedBrightnessValue, 0.001f, 0.999f);
        }

        //HSV to HSL (HSL Sucks ffs)
        float L = (2 - S) * V / 2;
        float S_HSL = S * V / (L < 0.5 ? L * 2 : 2 - L * 2);

        MudColor mudColor = new MudColor(H, S_HSL, L, 0);

        return mudColor;
    }
    #endregion

}