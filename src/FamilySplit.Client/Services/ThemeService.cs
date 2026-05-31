using Microsoft.JSInterop;

namespace FamilySplit.Client.Services;

public enum ThemeMode { System, Light, Dark }

/// <summary>
/// Singleton service that tracks the user's theme preference and the resolved
/// IsDarkMode value. Persists the preference to localStorage via JS interop.
/// </summary>
public sealed class ThemeService
{
    private const string StorageKey = "fs_theme";

    private readonly IJSRuntime _js;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public bool IsDarkMode { get; private set; }

    // The last known OS/browser preference — cached so SetModeAsync callers
    // don't need to supply it.
    private bool _systemIsDark;

    public event Action? OnChanged;

    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>
    /// Called once on app start. Reads saved preference from localStorage and
    /// applies it, using <paramref name="systemIsDark"/> for the System default.
    /// </summary>
    public async Task InitAsync(bool systemIsDark)
    {
        _systemIsDark = systemIsDark;
        var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        Mode = stored switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        Resolve();
    }

    /// <summary>Sets the mode, persists it, and notifies listeners.</summary>
    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        Resolve();
        var value = mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            _ => "system",
        };
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, value);
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Called when the OS preference changes. Only affects the resolved value
    /// when Mode is System.
    /// </summary>
    public void UpdateSystemPreference(bool systemIsDark)
    {
        _systemIsDark = systemIsDark;
        if (Mode != ThemeMode.System) return;
        Resolve();
        OnChanged?.Invoke();
    }

    private void Resolve() =>
        IsDarkMode = Mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => _systemIsDark,
        };
}
