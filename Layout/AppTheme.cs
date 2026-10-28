using MudBlazor;

namespace monexa.Layout;

public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1976d2",
            Secondary = "#9c27b0",
            Tertiary = "#00897b",
            Success = "#4caf50",
            Info = "#2196f3",
            Warning = "#ff9800",
            Error = "#f44336",
            AppbarBackground = "#03173d"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#90caf9",
            Secondary = "#ce93d8",
            Tertiary = "#4db6ac",
            Success = "#81c784",
            Info = "#64b5f6",
            Warning = "#ffb74d",
            Error = "#e57373",
            AppbarBackground = "#1e1e1e",
            Background = "#121212",
            Surface = "#1e1e1e",
            BackgroundGray = "#1a1a1a",
            DrawerBackground = "#1e1e1e"
        }
    };
}
