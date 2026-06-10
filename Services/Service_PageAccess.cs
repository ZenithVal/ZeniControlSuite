using System.Security.Claims;

namespace ZeniControlSuite.Services;

public sealed class Service_PageAccess
{
    public event Action? OnPageAccessChanged;

    private static readonly PageAccessDefinition[] _definitions =
    {
        new("AvatarControls", "Avatar Controls", "/AvatarControls", true),
        new("AvatarSelect", "Avatar Select", "/AvatarSelect", true),
        new("GamesPoints", "Games & Points", "/Games&Points", true),
        new("Bindings", "Binding Unlocks", "/Bindings", true),
        new("Devices", "Device Controls", "/Devices", true),
    };

    private readonly object _stateLock = new();
    private readonly Dictionary<string, bool> _pageStates = _definitions.ToDictionary(definition => definition.Key, definition => definition.EnabledByDefault);

    public IReadOnlyList<PageAccessEntry> Pages
    {
        get
        {
            lock (_stateLock)
            {
                return _definitions
                    .Select(definition => new PageAccessEntry(definition.Key, definition.Label, definition.Path, IsEnabledUnlocked(definition.Key)))
                    .ToList();
            }
        }
    }

    public bool IsEnabled(string key)
    {
        lock (_stateLock)
        {
            return IsEnabledUnlocked(key);
        }
    }

    public void SetEnabled(string key, bool enabled)
    {
        var changed = false;

        lock (_stateLock)
        {
            if (_pageStates.TryGetValue(key, out var current) && current != enabled)
            {
                _pageStates[key] = enabled;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    private bool IsEnabledUnlocked(string key)
    {
        return _pageStates.TryGetValue(key, out var enabled) && enabled;
    }

    public void NotifyChanged()
    {
        OnPageAccessChanged?.Invoke();
    }

    public string FirstAvailablePath()
    {
        return Pages.FirstOrDefault(page => page.Enabled)?.Path ?? "/";
    }

    public bool TryGetManagedPageByPath(string path, out PageAccessEntry page)
    {
        var normalized = NormalizePath(path);
        page = Pages.FirstOrDefault(candidate => NormalizePath(candidate.Path) == normalized) ?? PageAccessEntry.Empty;
        return !string.IsNullOrWhiteSpace(page.Key);
    }

    public bool IsManagedPath(string path)
    {
        return TryGetManagedPageByPath(path, out _);
    }

    public bool UserCanSeeNav(ClaimsPrincipal user, PageAccessEntry page)
    {
        if (user.IsInRole("Admin") || user.IsInRole("LocalHost"))
        {
            return true;
        }

        return page.Enabled && user.IsInRole("Visitor");
    }

    public bool ShouldRedirectFromPath(string path)
    {
        return TryGetManagedPageByPath(path, out var page) && !page.Enabled;
    }

    private static bool SetIfChanged(ref bool field, bool value)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        return true;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return path.TrimEnd('/').ToLowerInvariant() switch
        {
            "" => "/",
            var normalized => normalized
        };
    }

    private sealed record PageAccessDefinition(string Key, string Label, string Path, bool EnabledByDefault);
}

public sealed record PageAccessEntry(string Key, string Label, string Path, bool Enabled)
{
    public static PageAccessEntry Empty { get; } = new(string.Empty, string.Empty, string.Empty, false);
}
