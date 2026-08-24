using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SsmsToolset.Data;

namespace SsmsToolset.UI
{
    /// <summary>
    /// The panel shown when you pick "SSMS Toolset" on a database node.
    ///
    ///  - <b>Objects</b> tab: searchable inventory of tables/views/procs/functions,
    ///    with per-row actions (Select Top N, Script as CREATE).
    ///  - <b>Query</b> tab: run ad-hoc SQL against the database's connection.
    ///
    /// The options menu toggles the dark/light theme and where object actions open
    /// their SQL (this panel's Query tab, or a new native SSMS query window).
    /// </summary>
    public partial class ToolsetPanelControl : UserControl
    {
        private readonly string _connectionString;

        /// <summary>Host callback that opens a new native SSMS query with the given SQL.</summary>
        private readonly Action<string> _openInSsmsQuery;

        private readonly ObservableCollection<DatabaseObject> _objects = new ObservableCollection<DatabaseObject>();
        private ICollectionView _objectsView;
        private string _searchTerm = string.Empty;
        private bool _suppressToggle;

        /// <summary>Wired by the host after the frame is shown; docks the tool window.</summary>
        public Action DockAction { get; set; }

        public ToolsetPanelControl(string databaseName, string serverName, string connectionString, Action<string> openInSsmsQuery = null)
        {
            _connectionString = connectionString;
            _openInSsmsQuery = openInSsmsQuery;

            InitializeComponent();
            ToolsetTheme.Apply(this, ToolsetSettings.Theme);
            DatabaseBadge.Text = string.IsNullOrEmpty(serverName)
                ? databaseName
                : $"{databaseName} @ {serverName}";

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

            _objectsView = CollectionViewSource.GetDefaultView(_objects);
            _objectsView.Filter = FilterObject;
            ObjectsGrid.ItemsSource = _objectsView;

            _suppressToggle = true;
            ToggleTables.IsChecked = ToolsetSettings.ShowTables;
            ToggleViews.IsChecked = ToolsetSettings.ShowViews;
            ToggleProcedures.IsChecked = ToolsetSettings.ShowProcedures;
            ToggleFunctions.IsChecked = ToolsetSettings.ShowFunctions;
            _suppressToggle = false;

            ApplyColumnsParamsVisibility();
            LoadObjectsAsync();
        }

        // ── Toolbar: refresh + type toggles ─────────────────────────────────

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => LoadObjectsAsync();

        private void TypeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle)
            {
                return;
            }

            // Persist toggle state, then filter the loaded list live. (Unchecked types
            // are also excluded from the next Refresh query.)
            ToolsetSettings.ShowTables = ToggleTables.IsChecked == true;
            ToolsetSettings.ShowViews = ToggleViews.IsChecked == true;
            ToolsetSettings.ShowProcedures = ToggleProcedures.IsChecked == true;
            ToolsetSettings.ShowFunctions = ToggleFunctions.IsChecked == true;

            _objectsView?.Refresh();
            UpdateObjectsStatus();
        }

        private static bool IsTypeEnabled(string typeLabel)
        {
            switch (typeLabel)
            {
                case "Table": return ToolsetSettings.ShowTables;
                case "View": return ToolsetSettings.ShowViews;
                case "Procedure": return ToolsetSettings.ShowProcedures;
                case "Function": return ToolsetSettings.ShowFunctions;
                default: return true;
            }
        }

        // ── Options menu ────────────────────────────────────────────────────

        private void OptionsBtn_Click(object sender, RoutedEventArgs e)
        {
            SyncOptionChecks();
            OptionsBtn.ContextMenu.PlacementTarget = OptionsBtn;
            OptionsBtn.ContextMenu.Placement = PlacementMode.Bottom;
            OptionsBtn.ContextMenu.IsOpen = true;
        }

        private void SyncOptionChecks()
        {
            ThemeDarkItem.IsChecked = ToolsetSettings.Theme == ToolsetThemeKind.Dark;
            ThemeLightItem.IsChecked = ToolsetSettings.Theme == ToolsetThemeKind.Light;

            TargetSsmsItem.IsEnabled = _openInSsmsQuery != null;
            TargetToolsetItem.IsChecked = ToolsetSettings.QueryTarget == QueryTarget.ToolsetTab;
            TargetSsmsItem.IsChecked = ToolsetSettings.QueryTarget == QueryTarget.NewSsmsQuery;

            ShowColumnsItem.IsChecked = ToolsetSettings.ShowColumnsParams;
        }

        private void ShowColumns_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = ShowColumnsItem.IsChecked == true;
            ToolsetSettings.ShowColumnsParams = enabled;
            ApplyColumnsParamsVisibility();

            // Turning it on requires the extra column/parameter queries, so reload.
            if (enabled)
            {
                LoadObjectsAsync();
            }
        }

        private void ApplyColumnsParamsVisibility()
        {
            if (ColumnsParamsColumn != null)
            {
                ColumnsParamsColumn.Visibility =
                    ToolsetSettings.ShowColumnsParams ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetTheme(ToolsetThemeKind.Dark);
        private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetTheme(ToolsetThemeKind.Light);

        private void SetTheme(ToolsetThemeKind kind)
        {
            ToolsetSettings.Theme = kind;
            ToolsetTheme.Apply(this, kind);
            SyncOptionChecks();
        }

        private void TargetToolset_Click(object sender, RoutedEventArgs e)
        {
            ToolsetSettings.QueryTarget = QueryTarget.ToolsetTab;
            SyncOptionChecks();
        }

        private void TargetSsms_Click(object sender, RoutedEventArgs e)
        {
            ToolsetSettings.QueryTarget = _openInSsmsQuery != null ? QueryTarget.NewSsmsQuery : QueryTarget.ToolsetTab;
            SyncOptionChecks();
        }

        // ── Object actions (row context menu) ───────────────────────────────

        private static DatabaseObject TargetOf(object sender)
            => (sender as FrameworkElement)?.DataContext as DatabaseObject;

        // "Select Top 100 *": plain SELECT TOP (100) *.
        private void SelectTop100_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetOf(sender);
            if (target == null)
            {
                return;
            }

            if (!SqlScriptGenerator.SupportsSelectTop(target))
            {
                ShowSelectTopUnsupported(target);
                return;
            }

            DeliverSql(SqlScriptGenerator.SelectTop(target, 100), executeInToolset: true);
        }

        // "Select Top 1000 (all columns)": explicit column list (round-trips to the DB).
        private async void SelectTop1000_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetOf(sender);
            if (target == null)
            {
                return;
            }

            if (!SqlScriptGenerator.SupportsSelectTop(target))
            {
                ShowSelectTopUnsupported(target);
                return;
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                return;
            }

            try
            {
                string sql = await Task.Run(
                    () => SqlScriptGenerator.SelectTopAllColumns(_connectionString, target, 1000));
                DeliverSql(sql, executeInToolset: true);
            }
            catch (Exception ex)
            {
                MainTabs.SelectedIndex = 1;
                InputBox.Text = $"-- Failed to build SELECT for {target.FullName}: {ex.Message}";
            }
        }

        private void ShowSelectTopUnsupported(DatabaseObject target)
        {
            MainTabs.SelectedIndex = 1;
            InputBox.Text = $"-- Select Top applies to tables and views, not {target.TypeLabel.ToLowerInvariant()}s.";
        }

        // "Update (all columns, commented)": a fully commented-out UPDATE template.
        private async void Update_Click(object sender, RoutedEventArgs e)
            => await BuildModifyScript(TargetOf(sender), o => SqlScriptGenerator.UpdateStatement(_connectionString, o));

        // "Delete from (WHERE key)": a DELETE keyed on the primary key.
        private async void Delete_Click(object sender, RoutedEventArgs e)
            => await BuildModifyScript(TargetOf(sender), o => SqlScriptGenerator.DeleteStatement(_connectionString, o));

        // "Execute (with parameters)": an EXEC / SELECT template for procs/functions.
        private async void Exec_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetOf(sender);
            if (target == null || !target.IsExecutable || string.IsNullOrEmpty(_connectionString))
            {
                return;
            }

            try
            {
                string sql = await Task.Run(() => SqlScriptGenerator.ExecTemplate(_connectionString, target));
                DeliverSql(sql, executeInToolset: false);
            }
            catch (Exception ex)
            {
                MainTabs.SelectedIndex = 1;
                InputBox.Text = $"-- Failed to build EXEC template for {target.FullName}: {ex.Message}";
            }
        }

        /// <summary>
        /// Shared runner for UPDATE/DELETE: builds the SQL off the UI thread and
        /// drops it into the target destination — never executing it.
        /// </summary>
        private async Task BuildModifyScript(DatabaseObject target, Func<DatabaseObject, string> build)
        {
            if (target == null || !target.IsTabular || string.IsNullOrEmpty(_connectionString))
            {
                return;
            }

            try
            {
                string sql = await Task.Run(() => build(target));
                DeliverSql(sql, executeInToolset: false);
            }
            catch (Exception ex)
            {
                MainTabs.SelectedIndex = 1;
                InputBox.Text = $"-- Failed to build statement for {target.FullName}: {ex.Message}";
            }
        }

        private async void ScriptCreate_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetOf(sender);
            if (target == null || string.IsNullOrEmpty(_connectionString))
            {
                return;
            }

            try
            {
                string script = await Task.Run(() => SqlScriptGenerator.BuildCreateScript(_connectionString, target));
                DeliverSql(script, executeInToolset: false);
            }
            catch (Exception ex)
            {
                MainTabs.SelectedIndex = 1;
                InputBox.Text = $"-- Failed to script {target.FullName}: {ex.Message}";
            }
        }

        /// <summary>
        /// Sends generated SQL to the configured destination: a new SSMS query
        /// window, or this panel's Query tab (executing it when asked).
        /// </summary>
        private void DeliverSql(string sql, bool executeInToolset)
        {
            if (ToolsetSettings.QueryTarget == QueryTarget.NewSsmsQuery && _openInSsmsQuery != null)
            {
                try
                {
                    _openInSsmsQuery(sql);
                    return;
                }
                catch (Exception ex)
                {
                    MainTabs.SelectedIndex = 1;
                    InputBox.Text = $"-- Could not open a new SSMS query ({ex.Message}). Showing it here instead:\n\n{sql}";
                    return;
                }
            }

            MainTabs.SelectedIndex = 1;
            InputBox.Text = sql;
            if (executeInToolset)
            {
                RunCurrentQuery();
            }
        }

        // ── Objects tab ─────────────────────────────────────────────────────

        private async void LoadObjectsAsync()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                ObjectsStatus.Text = "No connection available for this database.";
                return;
            }

            ObjectsStatus.Text = "Loading objects...";
            try
            {
                var loaded = await Task.Run(() => DatabaseObjectService.Load(
                    _connectionString,
                    ToolsetSettings.ShowTables,
                    ToolsetSettings.ShowViews,
                    ToolsetSettings.ShowProcedures,
                    ToolsetSettings.ShowFunctions,
                    ToolsetSettings.ShowColumnsParams));

                _objects.Clear();
                foreach (var item in loaded)
                {
                    _objects.Add(item);
                }
                _objectsView.Refresh();
                UpdateObjectsStatus();
            }
            catch (Exception ex)
            {
                ObjectsStatus.Text = "Error loading objects: " + ex.Message;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility =
                string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            _searchTerm = TextNormalizer.Normalize(SearchBox.Text);
            _objectsView?.Refresh();
            UpdateObjectsStatus();
        }

        private bool FilterObject(object item)
        {
            var dbObject = (DatabaseObject)item;

            if (!IsTypeEnabled(dbObject.TypeLabel))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_searchTerm))
            {
                return true;
            }

            return dbObject.SearchKey != null && dbObject.SearchKey.Contains(_searchTerm);
        }

        private void UpdateObjectsStatus()
        {
            int total = _objects.Count;
            int shown = _objectsView.Cast<object>().Count();
            ObjectsStatus.Text = shown == total
                ? (total == 1 ? "1 object" : $"{total} objects")
                : $"{shown} of {total} objects";
        }

        // ── Query tab ───────────────────────────────────────────────────────

        private void ExecuteBtn_Click(object sender, RoutedEventArgs e) => RunCurrentQuery();

        private void RunCurrentQuery()
        {
            string sql = InputBox.Text?.Trim();
            if (string.IsNullOrEmpty(sql))
            {
                return;
            }

            StatusText.Visibility = Visibility.Collapsed;
            ResultGrid.ItemsSource = null;

            if (string.IsNullOrEmpty(_connectionString))
            {
                ShowStatus("No connection available — the Object Explorer node did not expose connection info.", isError: true);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var adapter = new SqlDataAdapter(sql, conn))
                    {
                        var table = new DataTable();
                        adapter.Fill(table);
                        ResultGrid.ItemsSource = table.DefaultView;
                        string rows = table.Rows.Count == 1 ? "1 row" : $"{table.Rows.Count} rows";
                        ShowStatus($"{rows} returned.", isError: false);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, isError: true);
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47))
                : new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
            StatusText.Visibility = Visibility.Visible;
        }

        // ── Header ──────────────────────────────────────────────────────────

        private void DockBtn_Click(object sender, RoutedEventArgs e) => DockAction?.Invoke();
    }
}
