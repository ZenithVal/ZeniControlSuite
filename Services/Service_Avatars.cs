using System.Text.Json;
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
    private readonly Service_AccessCodes AccessCodes;

    public Service_Avatars(Service_Logs serviceLogs, Service_Points servicePoints, Service_OSC serviceOSC, Service_AccessCodes accessCodes)
    {
        LogService = serviceLogs;
        PointsService = servicePoints;
        OSCService = serviceOSC;
        AccessCodes = accessCodes;
        OSCService.OnOscMessageReceived += HandleOSCMessage;
        AccessCodes.OnCodesChanged += SendVisitorCodeToAvatar;
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
    public List<AvatarControl> GlobalControls { get; private set; } = new();
    public HashSet<string> InvalidAvatarIds { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public string CurrentWornAvatarId { get; private set; } = "Global";
    public int CurrentAccessLevel { get; set; } = 0;
    public string LastLoadedAvatarParameterFile { get; private set; } = string.Empty;
    public string LastLoadedAvatarParameterName { get; private set; } = string.Empty;

    public bool avatarSelectEnabled = true;
    public bool avatarSelectFree = false;

    public double avatarSelectCostMulti = 1.0;

    public Avatar lastSelectedAvatar = new Avatar();
    public Avatar selectedAvatar = new Avatar();
    #endregion


    //===========================================//
    #region Initialization
    private string validationLog = "";
    public void ReloadSettings()
    {
        InitializeAvatarControls();
    }

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
        GlobalControls = new List<AvatarControl>();
        avatars.Clear();
        InvalidAvatarIds.Clear();

        // Deserialize the JSON string
        var jsonDocument = JsonDocument.Parse(jsonString);

        if (jsonDocument.RootElement.TryGetProperty("InvalidAvatars", out var invalidAvatarsElement) && invalidAvatarsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var invalidAvatar in invalidAvatarsElement.EnumerateArray())
            {
                var invalidAvatarId = invalidAvatar.GetString();
                if (!string.IsNullOrWhiteSpace(invalidAvatarId))
                {
                    InvalidAvatarIds.Add(invalidAvatarId);
                }
            }
        }

        // Deserialize GlobalControls
        var globalControlsElement = jsonDocument.RootElement.GetProperty("GlobalControls");
        foreach (var controlElement in globalControlsElement.EnumerateArray())
        {
            var control = DeserializeControl(controlElement);
            GlobalControls.Add(control);
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
                ID = avatarElement.GetProperty("ID").GetString() ?? string.Empty,
                Name = avatarElement.GetProperty("Name").GetString() ?? "Unnamed Avatar",
                Selectable = avatarElement.TryGetProperty("Selectable", out var selectableElement) && selectableElement.GetBoolean(),
                Available = !avatarElement.TryGetProperty("Available", out var availableElement) || availableElement.GetBoolean(),
                Cost = avatarElement.TryGetProperty("Cost", out var costElement) ? costElement.GetDouble() : 0,
                Thumbnail = GetAvatarImage(avatarElement.TryGetProperty("Thumbnail", out var thumbnailElement) ? thumbnailElement.GetString() ?? string.Empty : string.Empty),
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
            Console.WriteLine($"AC | Loading inherited controls");
            if (avatarElement.TryGetProperty("InheritedGlobalControls", out var inheritedControlsElement) && inheritedControlsElement.ValueKind == JsonValueKind.Array)
            foreach (var controlName in inheritedControlsElement.EnumerateArray())
            {
                validationLog = $"loading inherited control {controlName.GetString()}";

                //find a global control with the same name
                var globalControl = GlobalControls.FirstOrDefault(c => c.Name == controlName.GetString());

                if (globalControl != null)
                {
                    var inheritedControl = CloneControl(globalControl);
                    inheritedControl.SourceGlobalName = globalControl.Name;
                    avatar.Controls.Add(inheritedControl);
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
                Controls = CloneControls(GlobalControls),
                Parameters = new Dictionary<string, Parameter>()
            };
            CreateAvatarParamList(globalAvatar);
            avatars.Add(globalAvatar);
        }
        HandleAvatarChange("Global");

        if (!avatars.Any(a => a.ID == "Undefined"))
        {
            var undefiinedAvatar = new Avatar {
                ID = "Undefined",
                Name = "Undefined",
                Controls = CloneControls(GlobalControls),
                Parameters = new Dictionary<string, Parameter>()
            };
            CreateAvatarParamList(undefiinedAvatar);
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
            return "/images/AvatarThumbDefault.png";
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

        validationLog = $"deserializing control access level of {controlName}";
        control.AccessLevel = DeserializeAccessLevel(controlElement);

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
                AccessLevel = DeserializeAccessLevel(controlElement),
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
            return "/images/PowerButton.png";
        }
    }
    #endregion



    private static int DeserializeAccessLevel(JsonElement controlElement)
    {
        if (controlElement.TryGetProperty("AccessLevel", out var accessLevelElement) && accessLevelElement.ValueKind == JsonValueKind.Number)
        {
            return accessLevelElement.GetInt32();
        }

        if (!controlElement.TryGetProperty("RequiredRoles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var level = 0;
        foreach (var role in rolesElement.EnumerateArray().Select(r => r.GetString()))
        {
            level = Math.Max(level, role switch
            {
                "AvBindings1" => 1,
                "AvBindings2" => 2,
                "Admin" => 10,
                "LocalHost" => 10,
                _ => 0
            });
        }

        return level;
    }

    private static List<AvatarControl> CloneControls(IEnumerable<AvatarControl> controls)
    {
        return controls.Select(CloneControl).ToList();
    }

    private static AvatarControl CloneControl(AvatarControl control)
    {
        return control switch
        {
            ContTypeButton button => new ContTypeButton
            {
                Name = button.Name,
                AccessLevel = button.AccessLevel,
                Icon = button.Icon,
                SourceGlobalName = button.SourceGlobalName,
                Parameter = CloneParameter(button.Parameter)
            },
            ContTypeToggle toggle => new ContTypeToggle
            {
                Name = toggle.Name,
                AccessLevel = toggle.AccessLevel,
                Icon = toggle.Icon,
                SourceGlobalName = toggle.SourceGlobalName,
                Parameter = CloneParameter(toggle.Parameter),
                ValueOff = toggle.ValueOff,
                ValueOn = toggle.ValueOn
            },
            ContTypeRadial radial => new ContTypeRadial
            {
                Name = radial.Name,
                AccessLevel = radial.AccessLevel,
                Icon = radial.Icon,
                SourceGlobalName = radial.SourceGlobalName,
                Parameter = CloneParameter(radial.Parameter),
                ValueMin = radial.ValueMin,
                ValueMax = radial.ValueMax
            },
            ContTypeHSV hsv => new ContTypeHSV
            {
                Name = hsv.Name,
                AccessLevel = hsv.AccessLevel,
                Icon = hsv.Icon,
                SourceGlobalName = hsv.SourceGlobalName,
                ParameterHue = CloneParameter(hsv.ParameterHue),
                ParameterSaturation = CloneParameter(hsv.ParameterSaturation),
                ParameterBrightness = CloneParameter(hsv.ParameterBrightness),
                InvertedBrightness = hsv.InvertedBrightness,
                InvertedBrightnessValue = hsv.InvertedBrightnessValue,
                targetColor = hsv.targetColor
            },
            _ => throw new InvalidOperationException($"Unknown control type: {control.GetType().Name}")
        };
    }

    private static Parameter CloneParameter(Parameter parameter)
    {
        return new Parameter(parameter.Address, parameter.Type, parameter.Value);
    }

    //===========================================//
    #region Avatar Functions
    private void HandleOSCMessage(OscMessage messageReceived)
    {
        //Console.WriteLine($"OSC | {messageReceived.Address} : {messageReceived.Arguments[0]}");
        if (messageReceived.Arguments.Count == 0)
        {
            return;
        }

        if (messageReceived.Address == "/avatar/change")
        {
            var avatarID = messageReceived.Arguments[0]?.ToString() ?? "Undefined";
            HandleAvatarChange(avatarID);
        }
        else if (selectedAvatar.Parameters != null && selectedAvatar.Parameters.ContainsKey(messageReceived.Address)) //Handle existing Params
        {
            var parameter = selectedAvatar.Parameters[messageReceived.Address];
            float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
            HandleAvatarParam(parameter, value);
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
        if (string.IsNullOrWhiteSpace(avatarID))
        {
            avatarID = "Undefined";
        }

        if (!string.Equals(CurrentWornAvatarId, avatarID, StringComparison.OrdinalIgnoreCase))
        {
            LastLoadedAvatarParameterFile = string.Empty;
            LastLoadedAvatarParameterName = string.Empty;
        }

        CurrentWornAvatarId = avatarID;

        if (StruggleGameSystem)
        {
            TransferLastStruggleGameState();
        }

        if (selectedAvatar.ID == avatarID)
        {
            LogAvatars($"Avatar {selectedAvatar.Name} already loaded", Severity.Info);
            SendVisitorCodeToAvatar();
            InvokeAvatarControlsUpdate();
            return;
        }

        var matchedAvatar = avatars.FirstOrDefault(a => string.Equals(a.ID, avatarID, StringComparison.OrdinalIgnoreCase));
        if (matchedAvatar != null)
        {
            selectedAvatar = matchedAvatar;
            LogAvatars($"Avatar {selectedAvatar.Name} loaded", Severity.Info);

            HandleTrappedSwitch();
            SendVisitorCodeToAvatar();
            InvokeAvatarControlsUpdate();
            return;
        }

        var avatarIDTruncated = avatarID.Length > 13 ? avatarID[..13] : avatarID;
        LogAvatars($"No avatar for {avatarIDTruncated} found", Severity.Normal);
        selectedAvatar = CreateNoValidAvatarPlaceholder(avatarID);

        SendVisitorCodeToAvatar();
        InvokeAvatarControlsUpdate();
    }

    public bool SelectedAvatarIsValid => !selectedAvatar.IsInvalidPlaceholder && !CurrentAvatarInvalid;
    public bool CurrentAvatarInvalid => !string.IsNullOrWhiteSpace(CurrentWornAvatarId) && InvalidAvatarIds.Contains(CurrentWornAvatarId);
    public bool CurrentAvatarNeedsDecision => selectedAvatar.IsInvalidPlaceholder && !CurrentAvatarInvalid && !string.Equals(CurrentWornAvatarId, "Global", StringComparison.OrdinalIgnoreCase) && !string.Equals(CurrentWornAvatarId, "Undefined", StringComparison.OrdinalIgnoreCase);

    private Avatar CreateNoValidAvatarPlaceholder(string avatarID)
    {
        return new Avatar
        {
            ID = avatarID,
            Name = "No valid avatar selected",
            Selectable = false,
            Available = false,
            Cost = 0,
            Thumbnail = "/images/AvatarThumbDefault.png",
            Controls = new List<AvatarControl>(),
            Parameters = new Dictionary<string, Parameter>(),
            IsInvalidPlaceholder = true
        };
    }

    public Avatar AddCurrentWornAvatarToControls(string? displayName = null)
    {
        var avatarId = string.IsNullOrWhiteSpace(CurrentWornAvatarId) ? "Undefined" : CurrentWornAvatarId;
        var existing = avatars.FirstOrDefault(a => string.Equals(a.ID, avatarId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            selectedAvatar = existing;
            InvokeAvatarControlsUpdate();
            return existing;
        }

        InvalidAvatarIds.Remove(avatarId);
        var avatar = new Avatar
        {
            ID = avatarId,
            Name = ResolveAvatarDisplayName(displayName, avatarId),
            Selectable = true,
            Available = true,
            Cost = 0,
            Thumbnail = "/images/AvatarThumbDefault.png",
            Controls = new List<AvatarControl>(),
            Parameters = new Dictionary<string, Parameter>(),
            RuntimeGenerated = true
        };

        avatars.Add(avatar);
        selectedAvatar = avatar;
        AddMatchedGlobalControlsToSelected(saveAfterAdd: false);
        SaveAvatarControlsJson();
        LogAvatars($"Added avatar {avatar.Name} from current worn avatar", Severity.Info);
        InvokeAvatarControlsUpdate();
        return avatar;
    }

    private string ResolveAvatarDisplayName(string? displayName, string avatarId)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(LastLoadedAvatarParameterName))
        {
            return LastLoadedAvatarParameterName.Trim();
        }

        return $"Avatar {ShortAvatarId(avatarId)}";
    }

    public void MarkCurrentWornAvatarInvalid()
    {
        if (string.IsNullOrWhiteSpace(CurrentWornAvatarId))
        {
            return;
        }

        InvalidAvatarIds.Add(CurrentWornAvatarId);
        selectedAvatar = CreateNoValidAvatarPlaceholder(CurrentWornAvatarId);
        SaveAvatarControlsJson();
        LogAvatars($"Marked avatar {ShortAvatarId(CurrentWornAvatarId)} invalid", Severity.Warning);
        InvokeAvatarControlsUpdate();
    }

    public void RemoveCurrentWornAvatarFromInvalidList()
    {
        if (string.IsNullOrWhiteSpace(CurrentWornAvatarId))
        {
            return;
        }

        if (InvalidAvatarIds.Remove(CurrentWornAvatarId))
        {
            SaveAvatarControlsJson();
        }

        selectedAvatar = CreateNoValidAvatarPlaceholder(CurrentWornAvatarId);
        InvokeAvatarControlsUpdate();
    }


    public AvatarParameterLoadResult LoadCurrentAvatarParametersFromLocalFile()
    {
        var avatarId = CurrentWornAvatarId;
        if (string.IsNullOrWhiteSpace(avatarId) || !avatarId.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase))
        {
            return new AvatarParameterLoadResult(false, "No worn avatar ID is available yet.", 0, string.Empty, string.Empty);
        }

        var oscRoot = GetVrChatOscRootDirectory();
        if (oscRoot == null || !Directory.Exists(oscRoot))
        {
            return new AvatarParameterLoadResult(false, "VRChat OSC folder was not found.", 0, string.Empty, string.Empty);
        }

        string? filePath;
        try
        {
            filePath = Directory.EnumerateFiles(oscRoot, $"{avatarId}.json", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (Exception ex)
        {
            return new AvatarParameterLoadResult(false, $"Failed to search VRChat OSC folder: {ex.Message}", 0, string.Empty, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new AvatarParameterLoadResult(false, $"No OSC JSON file was found for {ShortAvatarId(avatarId)}.", 0, string.Empty, string.Empty);
        }

        try
        {
            var jsonText = File.ReadAllText(filePath);
            if (!string.IsNullOrEmpty(jsonText) && jsonText[0] == '\uFEFF')
            {
                jsonText = jsonText[1..];
            }

            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            var avatarName = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            var discovered = new List<DiscoveredOscParameter>();
            if (root.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var parameterElement in parametersElement.EnumerateArray())
                {
                    if (TryReadAvatarOscJsonParameter(parameterElement, out var parameter))
                    {
                        discovered.Add(parameter);
                    }
                }
            }

            OSCService.ReplaceDiscoveredAvatarParameters(discovered, $"Avatar JSON:{Path.GetFileName(filePath)}");
            LastLoadedAvatarParameterFile = filePath;
            LastLoadedAvatarParameterName = avatarName;

            if (!string.IsNullOrWhiteSpace(avatarName)
                && (selectedAvatar.IsInvalidPlaceholder || string.Equals(selectedAvatar.ID, avatarId, StringComparison.OrdinalIgnoreCase)))
            {
                selectedAvatar.Name = avatarName;
            }

            LogAvatars($"Loaded {discovered.Count} avatar parameter(s) from {Path.GetFileName(filePath)}", Severity.Info);
            InvokeAvatarControlsUpdate();
            return new AvatarParameterLoadResult(true, $"Loaded {discovered.Count} avatar parameter(s).", discovered.Count, filePath, avatarName);
        }
        catch (Exception ex)
        {
            return new AvatarParameterLoadResult(false, $"Failed to read avatar OSC JSON: {ex.Message}", 0, filePath, string.Empty);
        }
    }

    private static string? GetVrChatOscRootDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        return Path.Combine(userProfile, "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
    }

    private static bool TryReadAvatarOscJsonParameter(JsonElement parameterElement, out DiscoveredOscParameter parameter)
    {
        parameter = new DiscoveredOscParameter();

        var name = parameterElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString() ?? string.Empty
            : string.Empty;

        if (!parameterElement.TryGetProperty("input", out var inputElement) || inputElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var address = inputElement.TryGetProperty("address", out var addressElement) && addressElement.ValueKind == JsonValueKind.String
            ? addressElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(address) || !address.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var displayName = !string.IsNullOrWhiteSpace(name)
            ? name
            : address["/avatar/parameters/".Length..];

        if (ShouldIgnoreAvatarParameter(displayName))
        {
            return false;
        }

        var type = inputElement.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
            ? ParseAvatarJsonParameterType(typeElement.GetString())
            : ParameterType.Bool;

        parameter = new DiscoveredOscParameter
        {
            Address = address,
            Type = type,
            Source = "Avatar JSON",
            LastSeen = DateTimeOffset.UtcNow
        };
        return true;
    }

    private static bool ShouldIgnoreAvatarParameter(string parameterName)
    {
        if (parameterName.Contains("$synthetic$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IgnoredAvatarParameterNames.Contains(parameterName))
        {
            return true;
        }

        return IgnoredAvatarParameterSuffixes.Any(suffix => parameterName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static ParameterType ParseAvatarJsonParameterType(string? type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "int" => ParameterType.Int,
            "float" => ParameterType.Float,
            _ => ParameterType.Bool
        };
    }

    private static readonly string[] IgnoredAvatarParameterSuffixes =
    {
        "_Squish",
        "_Stretch",
        "_Angle",
        "_IsPosed",
        "_IsGrabbed"
    };

    private static readonly HashSet<string> IgnoredAvatarParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Viseme",
        "Voice",
        "GestureLeft",
        "GestureRight",
        "GestureLeftWeight",
        "GestureRightWeight",
        "AngularY",
        "VelocityX",
        "VelocityY",
        "VelocityZ",
        "VelocityMagnitude",
        "Upright",
        "Grounded",
        "Seated",
        "AFK",
        "TrackingType",
        "VRMode",
        "MuteSelf",
        "Earmuffs",
        "EarCtrl"
    };

    public int AddMatchedGlobalControlsToSelected(bool saveAfterAdd = true)
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return 0;
        }

        var discoveredAddresses = OSCService.DiscoveredAvatarParameters
            .Select(parameter => parameter.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = GlobalControls
            .Where(control => TryGetPrimaryParameter(control, out var parameter) && discoveredAddresses.Contains(parameter.Address))
            .Where(control => !AvatarHasControl(selectedAvatar, control))
            .ToList();

        foreach (var match in matches)
        {
            AddControlToAvatar(selectedAvatar, match, saveAfterAdd: false, inheritGlobal: true);
        }

        if (matches.Count > 0 && saveAfterAdd)
        {
            SaveAvatarControlsJson();
            InvokeAvatarControlsUpdate();
        }

        return matches.Count;
    }

    public IReadOnlyList<AvatarControl> GetMatchedGlobalControlsNotOnSelected()
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return Array.Empty<AvatarControl>();
        }

        var discoveredAddresses = OSCService.DiscoveredAvatarParameters
            .Select(parameter => parameter.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return GlobalControls
            .Where(control => TryGetPrimaryParameter(control, out var parameter) && discoveredAddresses.Contains(parameter.Address))
            .Where(control => !AvatarHasControl(selectedAvatar, control))
            .OrderBy(control => control.Name)
            .ToList();
    }

    public IReadOnlyList<AvatarControl> GetGlobalControlsNotOnSelected()
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return Array.Empty<AvatarControl>();
        }

        return GlobalControls
            .Where(control => !AvatarHasControl(selectedAvatar, control))
            .OrderBy(control => control.Name)
            .ToList();
    }

    public void AddGlobalControlToSelected(AvatarControl control)
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return;
        }

        AddControlToAvatar(selectedAvatar, control, saveAfterAdd: true, inheritGlobal: true);
    }

    public void AddToggleFromDiscoveredParameter(DiscoveredOscParameter discoveredParameter, int accessLevel, string? name = null, int integerOnValue = 1)
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return;
        }

        if (selectedAvatar.Controls.OfType<ContTypeToggle>().Any(control => string.Equals(control.Parameter.Address, discoveredParameter.Address, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var control = CreateToggleFromDiscoveredParameter(discoveredParameter, accessLevel, name, integerOnValue);
        AddControlToAvatar(selectedAvatar, control, saveAfterAdd: true);
    }

    public void AddToggleFromDiscoveredParameterToGlobal(DiscoveredOscParameter discoveredParameter, int accessLevel, string? name = null, int integerOnValue = 1)
    {
        if (GlobalControls.OfType<ContTypeToggle>().Any(control => string.Equals(control.Parameter.Address, discoveredParameter.Address, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        GlobalControls.Add(CreateToggleFromDiscoveredParameter(discoveredParameter, accessLevel, name, integerOnValue));
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    private ContTypeToggle CreateToggleFromDiscoveredParameter(DiscoveredOscParameter discoveredParameter, int accessLevel, string? name = null, int integerOnValue = 1)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? discoveredParameter.DisplayName : name.Trim();
        var valueOn = discoveredParameter.Type == ParameterType.Int ? integerOnValue : 1.0f;
        return new ContTypeToggle
        {
            Name = displayName,
            AccessLevel = Math.Clamp(accessLevel, 0, 10),
            Icon = GetControlImage(displayName),
            Parameter = new Parameter(discoveredParameter.Address, discoveredParameter.Type, 0),
            ValueOff = 0,
            ValueOn = valueOn
        };
    }

    private void AddControlToAvatar(Avatar avatar, AvatarControl control, bool saveAfterAdd, bool inheritGlobal = false)
    {
        var clone = CloneControl(control);
        clone.SourceGlobalName = inheritGlobal ? (control.SourceGlobalName ?? control.Name) : null;
        avatar.Controls.Add(clone);
        AddControlParametersToAvatar(avatar, clone);

        if (saveAfterAdd)
        {
            SaveAvatarControlsJson();
            InvokeAvatarControlsUpdate();
        }
    }

    private static bool AvatarHasControl(Avatar avatar, AvatarControl control)
    {
        if (TryGetPrimaryParameter(control, out var incomingParameter))
        {
            return avatar.Controls.Any(existing => TryGetPrimaryParameter(existing, out var existingParameter) && string.Equals(existingParameter.Address, incomingParameter.Address, StringComparison.OrdinalIgnoreCase));
        }

        return avatar.Controls.Any(existing => string.Equals(existing.Name, control.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetPrimaryParameter(AvatarControl control, out Parameter parameter)
    {
        switch (control)
        {
            case ContTypeButton button:
                parameter = button.Parameter;
                return true;
            case ContTypeToggle toggle:
                parameter = toggle.Parameter;
                return true;
            case ContTypeRadial radial:
                parameter = radial.Parameter;
                return true;
            case ContTypeHSV hsv:
                parameter = hsv.ParameterHue;
                return true;
            default:
                parameter = new Parameter();
                return false;
        }
    }

    private void AddControlParametersToAvatar(Avatar avatar, AvatarControl control)
    {
        if (control is ContTypeButton button)
        {
            AddToParamToDictionary(avatar, button.Parameter);
        }
        else if (control is ContTypeToggle toggle)
        {
            AddToParamToDictionary(avatar, toggle.Parameter);
        }
        else if (control is ContTypeRadial radial)
        {
            AddToParamToDictionary(avatar, radial.Parameter);
        }
        else if (control is ContTypeHSV hsv)
        {
            AddToParamToDictionary(avatar, hsv.ParameterHue);
            AddToParamToDictionary(avatar, hsv.ParameterSaturation);
            AddToParamToDictionary(avatar, hsv.ParameterBrightness);
        }
    }


    public void SetControlAccessLevel(AvatarControl control, int accessLevel)
    {
        control.AccessLevel = Math.Clamp(accessLevel, 0, 10);
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void RenameControl(AvatarControl control, string? name)
    {
        var cleanedName = string.IsNullOrWhiteSpace(name) ? "Unnamed Control" : name.Trim();
        if (string.Equals(control.Name, cleanedName, StringComparison.Ordinal))
        {
            return;
        }

        var oldName = control.Name;
        var isGlobalControl = GlobalControls.Contains(control);

        control.Name = cleanedName;
        control.Icon = GetControlImage(cleanedName);

        if (isGlobalControl)
        {
            foreach (var avatarControl in avatars.SelectMany(avatar => avatar.Controls))
            {
                if (string.Equals(avatarControl.SourceGlobalName, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    avatarControl.SourceGlobalName = cleanedName;
                }
            }
        }

        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void InvertToggleValues(ContTypeToggle control)
    {
        (control.ValueOff, control.ValueOn) = (control.ValueOn, control.ValueOff);
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void SetToggleIntegerValue(ContTypeToggle control, int value)
    {
        control.ValueOn = value;
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void MoveSelectedControl(AvatarControl control, int direction)
    {
        MoveControlInList(selectedAvatar.Controls, control, direction);
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void RemoveSelectedControl(AvatarControl control)
    {
        if (selectedAvatar.IsInvalidPlaceholder)
        {
            return;
        }

        if (selectedAvatar.Controls.Remove(control))
        {
            SaveAvatarControlsJson();
            InvokeAvatarControlsUpdate();
        }
    }

    public bool SelectedControlIsInheritedGlobal(AvatarControl control)
    {
        return control.IsInheritedGlobalControl;
    }

    public void BreakSelectedControlGlobalLink(AvatarControl control)
    {
        if (selectedAvatar.IsInvalidPlaceholder || !selectedAvatar.Controls.Contains(control))
        {
            return;
        }

        control.SourceGlobalName = null;
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void MoveGlobalControl(AvatarControl control, int direction)
    {
        MoveControlInList(GlobalControls, control, direction);
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    private static void MoveControlInList(List<AvatarControl> controls, AvatarControl control, int direction)
    {
        var index = controls.IndexOf(control);
        if (index < 0)
        {
            return;
        }

        var targetIndex = Math.Clamp(index + direction, 0, controls.Count - 1);
        if (targetIndex == index)
        {
            return;
        }

        controls.RemoveAt(index);
        controls.Insert(targetIndex, control);
    }

    public bool SelectedControlExistsInGlobal(AvatarControl control)
    {
        return GlobalControls.Any(global => AvatarHasSameIdentity(global, control));
    }

    public void AddSelectedControlToGlobal(AvatarControl control)
    {
        if (SelectedControlExistsInGlobal(control))
        {
            return;
        }

        var globalCopy = CloneControl(control);
        globalCopy.SourceGlobalName = null;
        GlobalControls.Add(globalCopy);
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void RemoveGlobalControl(AvatarControl control)
    {
        if (!GlobalControls.Remove(control))
        {
            return;
        }

        foreach (var avatar in avatars)
        {
            avatar.Controls.RemoveAll(avatarControl => string.Equals(avatarControl.SourceGlobalName, control.Name, StringComparison.OrdinalIgnoreCase));
            avatar.Parameters.Clear();
            CreateAvatarParamList(avatar);
        }

        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    private static bool AvatarHasSameIdentity(AvatarControl first, AvatarControl second)
    {
        if (TryGetPrimaryParameter(first, out var firstParameter) && TryGetPrimaryParameter(second, out var secondParameter))
        {
            return string.Equals(firstParameter.Address, secondParameter.Address, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortAvatarId(string avatarId)
    {
        return avatarId.Length > 13 ? avatarId[..13] : avatarId;
    }

    public void SaveAvatarControls()
    {
        SaveAvatarControlsJson();
        InvokeAvatarControlsUpdate();
    }

    public void UpdateAvatarName(Avatar avatar, string? name)
    {
        avatar.Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Avatar" : name.Trim();
        SaveAvatarControls();
    }

    public void UpdateAvatarSelectable(Avatar avatar, bool selectable)
    {
        avatar.Selectable = selectable;
        SaveAvatarControls();
    }

    public void UpdateAvatarAvailable(Avatar avatar, bool available)
    {
        avatar.Available = available;
        SaveAvatarControls();
    }

    public void UpdateAvatarCost(Avatar avatar, double cost)
    {
        avatar.Cost = Math.Max(0, cost);
        SaveAvatarControls();
    }

    private void SaveAvatarControlsJson()
    {
        try
        {
            Directory.CreateDirectory("Configs");
            var payload = new
            {
                InvalidAvatars = InvalidAvatarIds.OrderBy(id => id).ToList(),
                GlobalControls = GlobalControls.Select(SerializeControl).ToList(),
                Avatars = avatars
                    .Where(avatar => !avatar.IsInvalidPlaceholder && !string.Equals(avatar.ID, "Undefined", StringComparison.OrdinalIgnoreCase))
                    .Select(SerializeAvatar)
                    .ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText("Configs/AvatarControls.json", JsonSerializer.Serialize(payload, options));
        }
        catch (Exception ex)
        {
            LogControls($"Failed to save AvatarControls.json: {ex.Message}", Severity.Error);
        }
    }

    private static object SerializeAvatar(Avatar avatar)
    {
        return new
        {
            avatar.ID,
            avatar.Name,
            Thumbnail = Path.GetFileNameWithoutExtension(avatar.Thumbnail.Replace("/api/Images/Avatars/", string.Empty).Replace("images/", string.Empty)),
            avatar.Selectable,
            avatar.Available,
            avatar.Cost,
            Controls = avatar.Controls
                .Where(control => !control.IsInheritedGlobalControl)
                .Select(SerializeControl)
                .ToList(),
            InheritedGlobalControls = avatar.Controls
                .Where(control => control.IsInheritedGlobalControl)
                .Select(control => control.SourceGlobalName!)
                .ToList()
        };
    }

    private static object SerializeControl(AvatarControl control)
    {
        return control switch
        {
            ContTypeButton button => new
            {
                button.Name,
                button.AccessLevel,
                Type = "Button",
                Parameter = SerializeParameter(button.Parameter)
            },
            ContTypeToggle toggle => new
            {
                toggle.Name,
                toggle.AccessLevel,
                Type = "Toggle",
                Parameter = SerializeParameter(toggle.Parameter),
                toggle.ValueOff,
                toggle.ValueOn
            },
            ContTypeRadial radial => new
            {
                radial.Name,
                radial.AccessLevel,
                Type = "Radial",
                Parameter = SerializeParameter(radial.Parameter),
                radial.ValueMin,
                radial.ValueMax
            },
            ContTypeHSV hsv => new
            {
                hsv.Name,
                hsv.AccessLevel,
                Type = "HSV",
                ParameterHue = SerializeParameter(hsv.ParameterHue),
                ParameterSaturation = SerializeParameter(hsv.ParameterSaturation),
                ParameterBrightness = SerializeParameter(hsv.ParameterBrightness),
                hsv.InvertedBrightness
            },
            _ => new { control.Name, control.AccessLevel, Type = "Unknown" }
        };
    }

    private static object SerializeParameter(Parameter parameter)
    {
        return new
        {
            Path = parameter.Address,
            Type = parameter.Type.ToString(),
            Value = SerializeParameterValue(parameter)
        };
    }

    private static object SerializeParameterValue(Parameter parameter)
    {
        return parameter.Type switch
        {
            ParameterType.Bool => parameter.Value >= 0.5f,
            ParameterType.Int => (int)MathF.Round(parameter.Value),
            _ => parameter.Value
        };
    }

    private void SendVisitorCodeToAvatar()
    {
        OSCService.sendOSCParameter(AccessCodes.VisitorCodeParameter);
        LogAvatars($"Sent visitor code {AccessCodes.VisitorCodeDisplay}", Severity.Info);
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
        CurrentWornAvatarId = avatar.ID;
        lastSelectedAvatar = selectedAvatar;
        SendVisitorCodeToAvatar();
        InvokeAvatarControlsUpdate();
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

public sealed record AvatarParameterLoadResult(bool Success, string Message, int Count, string FilePath, string AvatarName);
