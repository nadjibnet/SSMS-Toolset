namespace SsmsToolset.UI
{
    public enum ToolsetThemeKind
    {
        Dark,
        Light
    }

    /// <summary>Where an object action (Select Top, script) opens its SQL.</summary>
    public enum QueryTarget
    {
        ToolsetTab,
        NewSsmsQuery
    }

    /// <summary>
    /// Session-wide toolset preferences, shared by every panel. Kept in memory for
    /// now; persisting to disk can come later.
    /// </summary>
    public static class ToolsetSettings
    {
        public static ToolsetThemeKind Theme { get; set; } = ToolsetThemeKind.Dark;

        public static QueryTarget QueryTarget { get; set; } = QueryTarget.ToolsetTab;
    }
}
