using MudBlazor;

namespace FamilySplit.Client.Theme;

/// <summary>
/// Single source of truth for the app's MudBlazor theme.
/// Change colors, typography, and layout here — everything else picks them up
/// automatically via the --mud-palette-* CSS variables emitted by MudThemeProvider.
/// </summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            // ── Brand ──────────────────────────────────────────────────────
            Primary = "#4F46E5",
            PrimaryContrastText = "#FFFFFF",
            PrimaryDarken = "#3730A3",
            PrimaryLighten = "#EEF2FF",

            Secondary = "#10B981",
            SecondaryContrastText = "#FFFFFF",

            // ── Semantic ───────────────────────────────────────────────────
            Success = "#10B981",
            SuccessLighten = "#D1FAE5",   // exposed as --mud-palette-success-lighten

            Warning = "#F59E0B",
            WarningLighten = "#FEF3C7",   // exposed as --mud-palette-warning-lighten

            Error = "#EF4444",
            ErrorLighten = "#FEE2E2",     // exposed as --mud-palette-error-lighten

            Info = "#3B82F6",

            // ── Surfaces ───────────────────────────────────────────────────
            Background = "#F8FAFC",
            BackgroundGray = "#F1F5F9",
            Surface = "#FFFFFF",

            DrawerBackground = "#FFFFFF",
            DrawerText = "#0F172A",
            DrawerIcon = "#64748B",

            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A",

            // ── Text ───────────────────────────────────────────────────────
            TextPrimary = "#0F172A",
            TextSecondary = "#64748B",
            TextDisabled = "#94A3B8",

            // ── Actions ────────────────────────────────────────────────────
            ActionDefault = "#64748B",
            ActionDisabled = "#CBD5E1",
            ActionDisabledBackground = "#F1F5F9",

            // ── Lines / overlays ───────────────────────────────────────────
            Divider = "#E2E8F0",
            DividerLight = "#F1F5F9",
            TableLines = "#E2E8F0",
            LinesDefault = "#E2E8F0",

            OverlayDark = "rgba(15,23,42,0.55)",
            OverlayLight = "rgba(248,250,252,0.80)",
        },

        PaletteDark = new PaletteDark
        {
            // ── Brand ──────────────────────────────────────────────────────
            Primary = "#6366F1",
            PrimaryContrastText = "#FFFFFF",
            PrimaryDarken = "#4F46E5",
            PrimaryLighten = "#312E81",

            Secondary = "#10B981",
            SecondaryContrastText = "#FFFFFF",

            // ── Semantic ───────────────────────────────────────────────────
            Success = "#10B981",
            SuccessLighten = "#064E3B",

            Warning = "#F59E0B",
            WarningLighten = "#78350F",

            Error = "#F87171",
            ErrorLighten = "#7F1D1D",

            Info = "#60A5FA",

            // ── Surfaces ───────────────────────────────────────────────────
            Background = "#0F172A",
            BackgroundGray = "#1E293B",
            Surface = "#1E293B",

            DrawerBackground = "#1E293B",
            DrawerText = "#F1F5F9",
            DrawerIcon = "#94A3B8",

            AppbarBackground = "#1E293B",
            AppbarText = "#F1F5F9",

            // ── Text ───────────────────────────────────────────────────────
            TextPrimary = "#F1F5F9",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",

            // ── Actions ────────────────────────────────────────────────────
            ActionDefault = "#94A3B8",
            ActionDisabled = "#334155",
            ActionDisabledBackground = "#1E293B",

            // ── Lines / overlays ───────────────────────────────────────────
            Divider = "#334155",
            DividerLight = "#1E293B",
            TableLines = "#334155",
            LinesDefault = "#334155",

            OverlayDark = "rgba(0,0,0,0.70)",
            OverlayLight = "rgba(15,23,42,0.80)",
        },

        // ── Layout ─────────────────────────────────────────────────────────
        LayoutProperties = new LayoutProperties
        {
            AppbarHeight = "56px",
            DrawerWidthLeft = "240px",
        },

        // Typography font-family is applied globally via app.css
        // (.mud-typography and related overrides) because MudBlazor's
        // Typography object doesn't support a single global font-family override.
    };
}
