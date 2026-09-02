using System;
using System.IO;

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
    /// Toolset preferences, shared by every panel and persisted to disk so they
    /// survive an SSMS restart. Settings are loaded on first use and saved
    /// whenever a value changes.
    /// </summary>
    public static class ToolsetSettings
    {
        private static ToolsetThemeKind _theme = ToolsetThemeKind.Dark;
        private static QueryTarget _queryTarget = QueryTarget.NewSsmsQuery;
        private static bool _showTables = true;
        private static bool _showViews = true;
        private static bool _showProcedures = true;
        private static bool _showFunctions = true;
        private static bool _showColumnsParams;

        static ToolsetSettings()
        {
            Load();
        }

        public static ToolsetThemeKind Theme
        {
            get => _theme;
            set => Set(ref _theme, value);
        }

        public static QueryTarget QueryTarget
        {
            get => _queryTarget;
            set => Set(ref _queryTarget, value);
        }

        public static bool ShowTables
        {
            get => _showTables;
            set => Set(ref _showTables, value);
        }

        public static bool ShowViews
        {
            get => _showViews;
            set => Set(ref _showViews, value);
        }

        public static bool ShowProcedures
        {
            get => _showProcedures;
            set => Set(ref _showProcedures, value);
        }

        public static bool ShowFunctions
        {
            get => _showFunctions;
            set => Set(ref _showFunctions, value);
        }

        /// <summary>
        /// Whether to load and show the (potentially slow) "Columns/Params" column.
        /// Off by default so a normal object load stays fast.
        /// </summary>
        public static bool ShowColumnsParams
        {
            get => _showColumnsParams;
            set => Set(ref _showColumnsParams, value);
        }

        private static void Set<T>(ref T field, T value)
        {
            if (!Equals(field, value))
            {
                field = value;
                Save();
            }
        }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SsmsToolset",
            "settings.ini");

        private static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "theme":
                            if (Enum.TryParse(value, true, out ToolsetThemeKind theme)) _theme = theme;
                            break;
                        case "querytarget":
                            if (Enum.TryParse(value, true, out QueryTarget target)) _queryTarget = target;
                            break;
                        case "showtables":
                            if (bool.TryParse(value, out bool t)) _showTables = t;
                            break;
                        case "showviews":
                            if (bool.TryParse(value, out bool v)) _showViews = v;
                            break;
                        case "showprocedures":
                            if (bool.TryParse(value, out bool p)) _showProcedures = p;
                            break;
                        case "showfunctions":
                            if (bool.TryParse(value, out bool f)) _showFunctions = f;
                            break;
                        case "showcolumnsparams":
                            if (bool.TryParse(value, out bool cp)) _showColumnsParams = cp;
                            break;
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable settings: fall back to defaults.
            }
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string contents =
                    $"Theme={_theme}\r\n" +
                    $"QueryTarget={_queryTarget}\r\n" +
                    $"ShowTables={_showTables}\r\n" +
                    $"ShowViews={_showViews}\r\n" +
                    $"ShowProcedures={_showProcedures}\r\n" +
                    $"ShowFunctions={_showFunctions}\r\n" +
                    $"ShowColumnsParams={_showColumnsParams}\r\n";
                File.WriteAllText(SettingsPath, contents);
            }
            catch
            {
                // Non-fatal: settings just won't persist this time.
            }
        }
    }
}
