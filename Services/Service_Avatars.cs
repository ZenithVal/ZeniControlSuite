﻿using System.Text.Json;
using CoreOSC;
using MudBlazor;
using MudBlazor.Utilities;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;

public class Service_Avatars : IHostedService
{
    private readonly Service_Logs LogService;
    private readonly Service_Points PointsService;
    private readonly Service_OSC OSCService;

    public Service_Avatars(Service_Logs serviceLogs, Service_Points servicePoints, Service_OSC serviceOSC)
    {
        LogService = serviceLogs;
        PointsService = servicePoints;
        OSCService = serviceOSC;
        OSCService.OnOscMessageReceived += HandleOSCMessage;
    }

    private void LogControls(string message, Severity severity)
    {
        LogService.AddLog("Service_Avatars/Controls", "System", message, severity, Variant.Outlined);
    }

    private void LogAvatars(string message, Severity severity)
    {
        LogService.AddLog("Service_Avatars/Switch", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
    public delegate void RequestAvatarsUpdate();
    public event RequestAvatarsUpdate? OnAvatarsUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeAvatarControls();
		StruggleGameSetup();
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

    public bool avatarSelectEnabled = true;
    public bool avatarSelectFree = false;

    public double avatarSelectCostMulti = 1.0;

    public Avatar lastSelectedAvatar = new Avatar();
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
            LogControls("Service Started", Severity.Normal);
            Console.WriteLine("");
            InvokeAvatarControlsUpdate();
        }
        catch (Exception e)
        {
            LogControls($"AvatarControls.json parsing failed during {validationLog}", Severity.Error);
            Console.WriteLine(e.Message);
            avatarsLoaded = false;
            InvokeAvatarControlsUpdate();
        }
    }

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
                Selectable = avatarElement.GetProperty("Selectable").GetBoolean(),
                Available = avatarElement.GetProperty("Available").GetBoolean(),
                Cost = avatarElement.GetProperty("Cost").GetDouble(),
                Thumbnail = GetAvatarImage(avatarElement.GetProperty("Thumbnail").GetString()),
                Controls = new List<AvatarControl>(),
                Parameters = new Dictionary<string, Parameter>()
            };

            var controlsElement = avatarElement.GetProperty("Controls");
            foreach (var controlElement in controlsElement.EnumerateArray())
            {
                if (controlElement.GetProperty("Type").GetString() == "ToggleCollection")
                {
                    var ToggleCollection = DeserializeToggleCollection(controlElement);
                    foreach (var control in ToggleCollection)
                    {
                        avatar.Controls.Add(control);
                    }
                }
                else if (controlElement.GetProperty("Type").GetString() == "ParameterCollection")
                {
                    var ParameterCollection = DeserializeParameterCollection(controlElement);
                    foreach (var parameter in ParameterCollection)
                    {
                        avatar.Parameters.Add(parameter.Address, parameter);
                    }
                }
                else
                {
                    var control = DeserializeControl(controlElement);
                    avatar.Controls.Add(control);
                }
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
        HandleAvatarChange("Global");

        if (!avatars.Any(a => a.ID == "Undefined"))
        {
            var undefiinedAvatar = new Avatar {
                ID = "Undefined",
                Name = "Undefined",
                Controls = globalControls,
                Parameters = new Dictionary<string, Parameter>()
            };
            avatars.Add(undefiinedAvatar);
        }
        var undefinedAvatar = avatars.FirstOrDefault(a => a.ID == "Undefined");

        avatarsLoaded = true;
        InvokeAvatarControlsUpdate();
    }

    private string GetAvatarImage(string ThumbnailName)
    {
        if (!Directory.Exists("Images"))
        {
            Directory.CreateDirectory("Images");
        }
        if (!Directory.Exists("Images/Avatars"))
        {
            Directory.CreateDirectory("Images/Avatars");
        }

        if (File.Exists($"Images/Avatars/{ThumbnailName}.png"))
        {
            Console.WriteLine($"AC | Found image for {ThumbnailName}");
            return $"/api/Images/Avatars/{ThumbnailName}.png";
        }
        else
        {
            Console.WriteLine($"AC | No image found for {ThumbnailName}");
            return "images/AvatarThumbDefault.png";
        }

    }

    private void CreateAvatarParamList(Avatar avatar)
    {
        foreach (var control in avatar.Controls)
        {
            if (control is ContTypeButton contButton)
            {
                AddToParamToDictionary(avatar, contButton.Parameter);
            }
            else if (control is ContTypeToggle contToggle)
            {
                AddToParamToDictionary(avatar, contToggle.Parameter);
            }
            else if (control is ContTypeRadial contRadial)
            {
                AddToParamToDictionary(avatar, contRadial.Parameter);
            }
            else if (control is ContTypeHSV contHSV)
            {
                AddToParamToDictionary(avatar, contHSV.ParameterHue);
                AddToParamToDictionary(avatar, contHSV.ParameterSaturation);
                AddToParamToDictionary(avatar, contHSV.ParameterBrightness);
            }
        }
    }

    public void AddToParamToDictionary(Avatar avatar, Parameter parameter)
    {
        if (!avatar.Parameters.ContainsKey(parameter.Address))
        {
            avatar.Parameters.Add(parameter.Address, parameter);
        }
        else
        {
            LogControls($"Parameter {parameter.Address} already exists in {avatar.Name}", Severity.Warning);
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

        validationLog = $"deserializing control icons of {controlName}";
        control.Icon = GetControlImage(controlName);

        validationLog = $"deserializing control roles of {controlName}";
        control.RequiredRoles = controlElement.GetProperty("RequiredRoles").EnumerateArray().Select(r => r.GetString()).ToList();

        return control;
    }

    private List<AvatarControl> DeserializeToggleCollection(JsonElement controlElement)
    {
        var controls = new List<AvatarControl>();

        var controlName = controlElement.GetProperty("Name").GetString();
        var prefix = controlElement.GetProperty("Prefix").GetString();
        var parameters = controlElement.GetProperty("Parameters").EnumerateArray().Select(p => p.GetString()).ToList();
        var valueOff = controlElement.GetProperty("ValueOff").GetSingle();
        var valueOn = controlElement.GetProperty("ValueOn").GetSingle();

        validationLog = $"Deserializing toggle collection {controlName}";
        Console.WriteLine($"AC | {validationLog}");

        foreach (var parameter in parameters)
        {
            var control = new ContTypeToggle {
                Name = controlName + " - " + parameter,
                RequiredRoles = controlElement.GetProperty("RequiredRoles").EnumerateArray().Select(r => r.GetString()).ToList(),
                Parameter = new Parameter {
                    Address = prefix + parameter,
                    Type = ParameterType.Bool,
                    Value = valueOff
                },
                ValueOff = valueOff,
                ValueOn = valueOn
            };
            Console.WriteLine($"AC | {controlName} - adding {parameter}");

            control.Icon = GetControlImage(parameter);
            controls.Add(control);
        }

        return controls;
    }

    public List<Parameter> DeserializeParameterCollection(JsonElement controlElement)
    {
        var parameters = new List<Parameter>();

        var controlName = controlElement.GetProperty("Name").GetString();
        var prefix = controlElement.GetProperty("Prefix").GetString();

        validationLog = $"Deserializing parameter collection {controlName}";
        Console.WriteLine($"AC | {validationLog}");


        var bools = controlElement.GetProperty("Bools").EnumerateArray().Select(p => p.GetString()).ToList();
        foreach (var parameter in bools)
        {
            if (parameter == "") continue;
            var param = new Parameter {
                Address = prefix + parameter,
                Type = ParameterType.Bool,
                Value = 0
            };
            parameters.Add(param);
        }

        var floats = controlElement.GetProperty("Floats").EnumerateArray().Select(p => p.GetString()).ToList();
        foreach (var parameter in floats)
        {
            if (parameter == "") continue;
            var param = new Parameter {
                Address = prefix + parameter,
                Type = ParameterType.Float,
                Value = 0.0f
            };
            parameters.Add(param);
        }

        var ints = controlElement.GetProperty("Ints").EnumerateArray().Select(p => p.GetString()).ToList();
        foreach (var parameter in ints)
        {
            if (parameter == "") continue;
            var param = new Parameter {
                Address = prefix + parameter,
                Type = ParameterType.Int,
                Value = 0
            };
            parameters.Add(param);
        }

        foreach (var parameter in parameters)
        {
            Console.WriteLine($"AC | {controlName} - adding {parameter.Address}");
        }

        return parameters;
    }

    private Parameter DeserializeParameter(JsonElement parameterElement)
    {
        var parameter = new Parameter();
        validationLog = $"Deserializing param {parameterElement.GetProperty("Path").GetString()}";
        Console.WriteLine($"AC | {validationLog}");
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
                LogControls("Unknown parameter type", Severity.Error);
                throw new InvalidOperationException("Unknown parameter type");
        }

        return parameter;
    }

    private string GetControlImage(string imageName)
    {
        if (!Directory.Exists("Images"))
        {
            Directory.CreateDirectory("Images");
        }
        if (!Directory.Exists("Images/Controls"))
        {
            Directory.CreateDirectory("Images/Controls");
        }

        string imageNoSpaces = imageName.Replace(" ", "");

        if (File.Exists($"Images/Controls/{imageNoSpaces}.png"))
        {
            Console.WriteLine($"AC | Found image for {imageName}");
            return $"/api/Images/Controls/{imageNoSpaces}.png";
        }
        else
        {
            Console.WriteLine($"AC | No image found for {imageName}");
            return "images/PowerButton.png";
        }
    }
    #endregion


    //===========================================//
    #region Avatar Functions
    private void HandleOSCMessage(OscMessage messageReceived)
    {
        //Console.WriteLine($"OSC | {messageReceived.Address} : {messageReceived.Arguments[0]}");
        if (selectedAvatar.Parameters.ContainsKey(messageReceived.Address)) //Handle existing Params
        {
            var parameter = selectedAvatar.Parameters[messageReceived.Address];
            float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
            HandleAvatarParam(parameter, value);
        }
        else if (messageReceived.Address == "/avatar/change")
        {
            var avatarID = messageReceived.Arguments[0].ToString();
            HandleAvatarChange(avatarID);
        }
        else if (messageReceived.Address.Contains("StruggleGame"))
        {
            if (StruggleGameParameters.ContainsKey(messageReceived.Address))
            {
                var parameter = StruggleGameParameters[messageReceived.Address];
                float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
                HandleStruggleGameParam(parameter, value);
            }
        }
    }


    public void HandleAvatarChange(string avatarID)
    {
		if (StruggleGameSystem)
		{
			TransferLastStruggleGameState();
		}

		if (selectedAvatar.ID == avatarID)
        {
            LogAvatars($"Avatar {selectedAvatar.Name} already loaded", Severity.Info);
            return;
        }

        if (avatars.Any(a => a.ID == avatarID))
        {
            selectedAvatar = avatars.FirstOrDefault(a => a.ID == avatarID);
            LogAvatars($"Avatar {selectedAvatar.Name} loaded", Severity.Info);

            HandleTrappedSwitch();

            InvokeAvatarControlsUpdate();
        }
        else
        {
            //tuncate avatarID to 13 characters for log
            string avatarIDTruncated = avatarID.Length > 13 ? avatarID.Substring(0, 13) : avatarID;

            LogAvatars($"No avatar for {avatarIDTruncated} found", Severity.Normal);
            selectedAvatar = avatars.FirstOrDefault(a => a.ID == "Global");
            LogAvatars($"Selected avatar Global", Severity.Info);

            InvokeAvatarControlsUpdate();
        }
    }

    public void SetParameterValue(Parameter param) //used by AvatarControls. Upddates the param and sends an OSC message out with it
    {
        if (selectedAvatar.Parameters.ContainsKey(param.Address))
        {
			selectedAvatar.Parameters[param.Address].Value = param.Value;
        }
        OSCService.sendOSCParameter(param);

        InvokeAvatarControlsUpdate();
    }

    public void SwitchAvatar(Avatar avatar) //Sends an OSC paramter to switch the avatar. Used by the UI
    {
        OSCService.sendOSCMessage("/avatar/change", avatar.ID);
        LogAvatars($"Switching Avatar to {avatar.Name}", Severity.Info);
        selectedAvatar = avatar;
        lastSelectedAvatar = selectedAvatar;
    }
    #endregion

    //===========================================//
    #region Avatar Trap
    public bool Trapped = false;
    public DateTime TrapEndTime = DateTime.Now;
    public bool TrapTimerRunning = false;

    private void HandleTrappedSwitch()
    {
        if (!Trapped) return;

        LogAvatars($"Avatar changed while trapped, returning to {lastSelectedAvatar.Name}", Severity.Warning);
        SwitchAvatar(lastSelectedAvatar);
    }

    public void TrapAvatar()
    {
        if (!TrapTimerRunning)
        {
            Trapped = true;
            TrapEndTime = DateTime.Now.AddMinutes(15);
            LogAvatars("Avatar Trapped", Severity.Normal);
            TrappedAwait();
        }
        else
        {
            TrapTimerUpdate(1);

        }
        InvokeAvatarControlsUpdate();
    }

    public void TrapTimerUpdate(int minutes)
    {
        TrapEndTime = TrapEndTime.AddMinutes(minutes);
        LogAvatars($"Trap Timer updated by {minutes} minutes", Severity.Info);

        if (DateTime.Now > TrapEndTime && Trapped)
        {
            Trapped = false;
            LogAvatars("Avatar Trap Ended", Severity.Warning);
        }
        InvokeAvatarControlsUpdate();
    }

    private async Task TrappedAwait()
    {
        TrapTimerRunning = true;
        while (Trapped)
        {
            if (DateTime.Now > TrapEndTime && Trapped)
            {
                Trapped = false;
                LogAvatars("Avatar Trap Ended", Severity.Warning);
            }
            await Task.Delay(10000);
        }
        TrapTimerRunning = false;
        InvokeAvatarControlsUpdate();
    }


    #endregion


    //===========================================//
    #region StruggleGame Handling
    public bool StruggleGameSystem = false;
	public bool StruggleGameIgnoreIncoming = false;
	public bool StruggleGameActive = false;
    private string StruggleGamePath = "/avatar/parameters/StruggleGame/";
    private Dictionary<string, Parameter> StruggleGameParameters = new Dictionary<string, Parameter>();
    private void StruggleGameSetup()
	{
		try
		{
			List<string> bools = new List<string> { "Active" };
			List<string> floats = new List<string> { "Level", "Difficulty", "Shield" };

			foreach (var parameter in bools)
			{
				StruggleGameParameters.Add(StruggleGamePath+parameter, new Parameter { Address = StruggleGamePath+parameter, Type = ParameterType.Bool, Value = 0.0f });
			}
			foreach (var parameter in floats)
			{
				StruggleGameParameters.Add(StruggleGamePath+parameter, new Parameter { Address = StruggleGamePath+parameter, Type = ParameterType.Float, Value = 0.0f });
			}
		}
		catch (Exception e)
		{
			LogControls($"Struggle Game System Failed to load: \n{e}", Severity.Error);
			return;
		}
	}

    private void StruggleGameEnable()
    {
        StruggleGameSystem = true;
        LogControls("Struggle Game System Enabled", Severity.Normal);
    }

    public void HandleStruggleGameParam(Parameter param, float value)
    {
        if (!StruggleGameSystem) StruggleGameEnable();
        if (StruggleGameIgnoreIncoming) return;
        
        if (!StruggleGameParameters.ContainsKey(param.Address))
        {
            //LogControls($"Parameter {param.Address} not found in StruggleGame", Severity.Warning);
            return;
        }

        //Update StruggleGame Avatar Param
        StruggleGameParameters[param.Address].Value = value;

        if (param.Address.Contains("Active"))
        {
            bool ParamActive = value > 0.5;
            if (ParamActive && !StruggleGameActive)
            {
                StruggleGameStart();
                LogControls("Struggle Game Started", Severity.Info);
            }
            else if (!ParamActive && StruggleGameActive)
            {
                if (StruggleGameParameters[StruggleGamePath+"Level"].Value > 0.02)
                {
					StruggleGameEndBad();
				}
				else
                {
					StruggleGameEndGood();
				}
                LogControls("Struggle Game Ended", Severity.Info);
            }
        }
    }

    private async void TransferLastStruggleGameState()
    {
        StruggleGameIgnoreIncoming = true;
        await Task.Delay(1000); //delay to allow avatar to load

        foreach (var parameter in StruggleGameParameters)
        {
            var param = StruggleGameParameters[parameter.Key];
            SetParameterValue(param);
        }
        StruggleGameIgnoreIncoming = false;
    }

    double struggleGamePenaltyValue = 0.0f;

    public void SetStruggleGamePenalty()
    {
        float value = StruggleGameParameters[StruggleGamePath + "Level"].Value;
        struggleGamePenaltyValue = Math.Round(value * 4, MidpointRounding.ToEven) / 4;
        struggleGamePenaltyValue = Math.Truncate(struggleGamePenaltyValue * 100) / 100;
	}

    public void StruggleGameStart()
    {
        StruggleGameActive = true;
        SetStruggleGamePenalty();
		
        LogService.AddLog("AvatarPoints", "OSC Input", $"Struggle game started, {struggleGamePenaltyValue}✦ will be added if failed.", Severity.Info, Variant.Outlined);
    }

	public void StruggleGameEndGood()
    {
		StruggleGameActive = false;
		LogService.AddLog("AvatarPoints", "OSC Input", $"Struggle game finished.", Severity.Info, Variant.Outlined);
    }

    public void StruggleGameEndBad()
	{
		StruggleGameActive = false;
		PointsService.UpdatePoints(struggleGamePenaltyValue);
		LogService.AddLog("AvatarPoints", "OSC Input", $"Struggle game failed, added {struggleGamePenaltyValue}✦ | Total: {PointsService.pointsDisplay}", Severity.Info, Variant.Outlined);
	}
	#endregion


	//===========================================//
	#region Helper function
	public void HandleAvatarParam(Parameter param, float value) //Used for incoming OSC messages. Updates the param in the app and invokes an update for visuals.
    {
        selectedAvatar.Parameters[param.Address].Value = value;
        //hsv handling
        if (selectedAvatar.Controls.Any(c => c is ContTypeHSV contHSV && contHSV.InvertedBrightness && contHSV.ParameterBrightness.Address == param.Address))
        {
            var contHSV = selectedAvatar.Controls.FirstOrDefault(c => c is ContTypeHSV contHSV && contHSV.InvertedBrightness && contHSV.ParameterBrightness.Address == param.Address) as ContTypeHSV;
            contHSV.InvertedBrightnessValue = Math.Abs(1 - value);
        }
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