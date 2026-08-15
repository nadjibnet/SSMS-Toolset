using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SsmsToolset.Data;

namespace SsmsToolset.UI
{
    /// <summary>
    /// The panel shown when you pick "SSMS Toolset" on a database node.
    ///
    ///  - <b>Objects</b> tab: searchable inventory of tables/views/procs/functions.
    ///  - <b>Query</b> tab: run ad-hoc SQL against the database's connection.
    /// </summary>
    public partial class ToolsetPanelControl : UserControl
    {
        private readonly string _connectionString;

        private readonly ObservableCollection<DatabaseObject> _objects = new ObservableCollection<DatabaseObject>();
        private ICollectionView _objectsView;
        private string _searchTerm = string.Empty;

        /// <summary>Wired by the host after the frame is shown; docks the tool window.</summary>
        public Action DockAction { get; set; }

        public ToolsetPanelControl(string databaseName, string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
            DatabaseBadge.Text = databaseName;

            _objectsView = CollectionViewSource.GetDefaultView(_objects);
            _objectsView.Filter = FilterObject;
            ObjectsGrid.ItemsSource = _objectsView;

            LoadObjectsAsync();
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
                var loaded = await Task.Run(() => DatabaseObjectService.Load(_connectionString));

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
            if (string.IsNullOrEmpty(_searchTerm))
            {
                return true;
            }

            var dbObject = (DatabaseObject)item;
            return dbObject.SearchKey != null && dbObject.SearchKey.Contains(_searchTerm);
        }

        private void UpdateObjectsStatus()
        {
            int total = _objects.Count;
            if (string.IsNullOrEmpty(_searchTerm))
            {
                ObjectsStatus.Text = total == 1 ? "1 object" : $"{total} objects";
                return;
            }

            int shown = _objectsView.Cast<object>().Count();
            ObjectsStatus.Text = $"{shown} of {total} objects";
        }

        // ── Query tab ───────────────────────────────────────────────────────

        private void ExecuteBtn_Click(object sender, RoutedEventArgs e)
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
