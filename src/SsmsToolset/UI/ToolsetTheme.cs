using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SsmsToolset.UI
{
    /// <summary>
    /// Supplies the brush palette for the dark and light themes and applies it to
    /// a control's resources. The XAML references these by key via DynamicResource,
    /// so re-applying a different palette re-themes the panel live.
    /// </summary>
    public static class ToolsetTheme
    {
        // Brush resource keys used from XAML.
        public static readonly string[] Keys =
        {
            "T.Window", "T.Card", "T.Input", "T.Border", "T.Text", "T.TextMuted",
            "T.Accent", "T.Label", "T.BadgeBg", "T.BadgeFg", "T.HeaderBg", "T.HeaderFg",
            "T.AltRow", "T.GridLine", "T.AccentBtnBg", "T.AccentBtnFg",
            "T.SubtleBtnBg", "T.SubtleBtnFg", "T.CodeText", "T.TabBg", "T.TabSelBg",
            "T.SelBg", "T.SelFg"
        };

        public static void Apply(FrameworkElement target, ToolsetThemeKind kind)
        {
            var palette = kind == ToolsetThemeKind.Light ? Light() : Dark();
            foreach (var pair in palette)
            {
                target.Resources[pair.Key] = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(pair.Value));
            }
        }

        private static Dictionary<string, string> Dark() => new Dictionary<string, string>
        {
            ["T.Window"]       = "#1E1E1E",
            ["T.Card"]         = "#252526",
            ["T.Input"]        = "#1E1E1E",
            ["T.Border"]       = "#3C3C3C",
            ["T.Text"]         = "#D4D4D4",
            ["T.TextMuted"]    = "#858585",
            ["T.Accent"]       = "#569CD6",
            ["T.Label"]        = "#DCDCAA",
            ["T.BadgeBg"]      = "#264F78",
            ["T.BadgeFg"]      = "#9CDCFE",
            ["T.HeaderBg"]     = "#3C3C3C",
            ["T.HeaderFg"]     = "#DCDCAA",
            ["T.AltRow"]       = "#252526",
            ["T.GridLine"]     = "#333333",
            ["T.AccentBtnBg"]  = "#0E639C",
            ["T.AccentBtnFg"]  = "#FFFFFF",
            ["T.SubtleBtnBg"]  = "#2D2D2D",
            ["T.SubtleBtnFg"]  = "#CCCCCC",
            ["T.CodeText"]     = "#CE9178",
            ["T.TabBg"]        = "#2D2D2D",
            ["T.TabSelBg"]     = "#1E1E1E",
            ["T.SelBg"]        = "#094771",
            ["T.SelFg"]        = "#FFFFFF"
        };

        private static Dictionary<string, string> Light() => new Dictionary<string, string>
        {
            ["T.Window"]       = "#FFFFFF",
            ["T.Card"]         = "#F3F3F3",
            ["T.Input"]        = "#FFFFFF",
            ["T.Border"]       = "#CCCCCC",
            ["T.Text"]         = "#1E1E1E",
            ["T.TextMuted"]    = "#6A6A6A",
            ["T.Accent"]       = "#0E639C",
            ["T.Label"]        = "#795E26",
            ["T.BadgeBg"]      = "#CCE4F7",
            ["T.BadgeFg"]      = "#0B5394",
            ["T.HeaderBg"]     = "#E7E7E7",
            ["T.HeaderFg"]     = "#444444",
            ["T.AltRow"]       = "#F7F7F7",
            ["T.GridLine"]     = "#DDDDDD",
            ["T.AccentBtnBg"]  = "#0E639C",
            ["T.AccentBtnFg"]  = "#FFFFFF",
            ["T.SubtleBtnBg"]  = "#E7E7E7",
            ["T.SubtleBtnFg"]  = "#333333",
            ["T.CodeText"]     = "#A31515",
            ["T.TabBg"]        = "#E7E7E7",
            ["T.TabSelBg"]     = "#FFFFFF",
            ["T.SelBg"]        = "#ADD6FF",
            ["T.SelFg"]        = "#000000"
        };
    }
}
