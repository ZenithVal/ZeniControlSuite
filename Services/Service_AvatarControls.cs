using System.Text.Json;
using CoreOSC;
using MudBlazor;
using MudBlazor.Utilities;
using OpenShock.SDK.CSharp.Models;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;

public class Service_AvatarControls : IHostedService
{
    private readonly Service_Logs LogService;
    private readonly Service_Points PointsService;
    private readonly Service_OSC OSCService;

    public Service_AvatarControls(Service_Logs serviceLogs, Service_Points servicePoints, Service_OSC serviceOSC)
    {
        LogService = serviceLogs;
		PointsService = servicePoints;
		OSCService = serviceOSC;
        OSCService.OnOscMessageReceived += HandleOSCMessage;
	}

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

    public bool avatarSelectEnabled = true;
    public bool avatarSelectFree = false;
    public bool avatarTrapped = false;

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

        StruggleGameSetup();

        avatarsLoaded = true;
        InvokeAvatarControlsUpdate();
    }

    private string GetAvatarImage(string ThumbnailName)
    {
        if (!Directory.Exists("Images"))
        {
            Directory.CreateDirectory("Images");
        }

        if (File.Exists($"Images/Avatars/{ThumbnailName}.png"))
        {
            Console.WriteLine($"AC | Found image for {ThumbnailName}");
            return $"/api/images?imageName="+$"/Avatars/"+$"{ThumbnailName}.png";
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

        validationLog = $"deserializing control icons of {controlName}";
        control.Icon = GetImage(controlName);

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

            control.Icon = GetImage(parameter);
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
                Log("Unknown parameter type", Severity.Error);
                throw new InvalidOperationException("Unknown parameter type");
        }

        return parameter;
    }
	#endregion


	//===========================================//
	#region Avatar Functions
	private void HandleOSCMessage(OscMessage messageReceived)
	{
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
		else if (StruggleGameSystem && messageReceived.Address.Contains("StruggleGame"))
		{
			if (StruggleGameAvatar.Parameters.ContainsKey(messageReceived.Address))
			{
				var parameter = StruggleGameAvatar.Parameters[messageReceived.Address];
				float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
				HandleStruggleGameParam(parameter, value);
			}
		}
	}


	public void HandleAvatarChange(string avatarID)
    {

		if (selectedAvatar.ID == avatarID)
        {
            //Usually an avatar reset or switching worlds.
            return;
        }

		if (StruggleGameSystem)
		{
			StruggleGameResync();
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

	public void SetParameterValue(Parameter param) //used by AvatarControls. Upddates the param and sends an OSC message out with it
	{
        if (!selectedAvatar.Parameters.ContainsKey(param.Address))
        {
            Log($"Parameter {param.Address} not found in {selectedAvatar.Name}", Severity.Warning);
            return;
        }
        selectedAvatar.Parameters[param.Address].Value = param.Value;
		OSCService.sendOSCParameter(param);

		InvokeAvatarControlsUpdate();
	}

    public void SwitchAvatar(Avatar avatar) //Sends an OSC paramter to switch the avatar. Used by the UI
    {
        OSCService.sendOSCMessage("/avatar/change", avatar.ID);
        selectedAvatar = avatar;
        lastSelectedAvatar = selectedAvatar;
    }
    #endregion

    //===========================================//
    #region StruggleGame Handling
    public bool StruggleGameSystem = false;
	public bool StruggleGameActive = false;
	public Avatar StruggleGameAvatar = new Avatar();

    List<Parameter> StruggleGameLastState = new List<Parameter>();
	private void StruggleGameSetup()
    {
        if (avatars.Any(a => a.ID == "StruggleGame"))
        {
            StruggleGameSystem = true;
            Log("Struggle Game System Enabled", Severity.Info);
            StruggleGameAvatar = avatars.FirstOrDefault(a => a.ID == "StruggleGame");
        }
        else
        {
            StruggleGameSystem = false;
            return;
        }
    }

    public void HandleStruggleGameParam(Parameter param, float value)
    {
        if (!StruggleGameAvatar.Parameters.ContainsKey(param.Address))
        {
            Log($"Parameter {param.Address} not found in StruggleGame", Severity.Warning);
            return;
        }
        StruggleGameAvatar.Parameters[param.Address].Value = value;

        if (param.Address.Contains("Active"))
        {
            bool ParamActive = value > 0.5;
			if (ParamActive && !StruggleGameActive)
            {
                StruggleGameStart();
				Log("Struggle Game Started", Severity.Info);
			}
			else if (!ParamActive && StruggleGameActive)
            {
                StruggleGameEnd();
				Log("Struggle Game Ended", Severity.Info);
			}
		}
    }

    public void StruggleGameResync()
    {
        foreach (var param in StruggleGameLastState)
        {
            SetParameterValue(param);
		}
	}

    public void StruggleGameStart()
	{
		StruggleGameActive = true;
		PointsService.UpdatePoints(0.25);
		LogService.AddLog("AvatarPoints", "OSC Input", $"Struggle game started, added +0.25p | Total: {PointsService.pointsTruncated}", Severity.Info, Variant.Outlined);
        
        try
        {
            foreach (var param in StruggleGameAvatar.Parameters)
            {
                if (StruggleGameLastState.Any(p => p.Address == param.Key))
                {
					StruggleGameLastState.FirstOrDefault(p => p.Address == param.Key).Value = param.Value.Value;
				}
				else
                {
					StruggleGameLastState.Add(new Parameter {
						Address = param.Key,
						Type = param.Value.Type,
						Value = param.Value.Value
					});
				}
			}
		}
		catch (Exception e)
        {
			Console.WriteLine(e.Message);
		}
	}

	public void StruggleGameEnd()
	{
		StruggleGameActive = false;
		PointsService.UpdatePoints(-0.25);
		LogService.AddLog("AvatarPoints", "OSC Input", $"Struggle game succeeded, removed 0.25p | Total: {PointsService.pointsTruncated}", Severity.Info, Variant.Outlined);
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

    private string GetImage(string imageName)
    {
        if (!Directory.Exists("Images"))
        {
            Directory.CreateDirectory("Images");
        }

        string imageNoSpaces = imageName.Replace(" ", "");

        if (File.Exists($"Images/{imageNoSpaces}.png"))
        {
            Console.WriteLine($"AC | Found image for {imageName}");
            return $"/api/Images/{imageNoSpaces}.png";
        }
        else
        {
            Console.WriteLine($"AC | No image found for {imageName}");
            return "images/PowerButton.png";
        }
    }
    #endregion

}