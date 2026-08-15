using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SsmsToolset.UI
{
    /// <summary>
    /// The panel shown when you pick "SSMS Toolset" on a database node. It runs SQL
    /// against that database's connection and shows the grid — the seed of the
    /// Azure Data Studio-style toolset (search, scripting, SELECT TOP N come next).
    /// </summary>
    public partial class ToolsetPanelControl : UserControl
    {
        private readonly string _connectionString;

        /// <summary>Wired by the host after the frame is shown; docks the tool window.</summary>
        public Action DockAction { get; set; }

        public ToolsetPanelControl(string databaseName, string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
            DatabaseBadge.Text = databaseName;
        }

        private void DockBtn_Click(object sender, RoutedEventArgs e) => DockAction?.Invoke();

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
    }
}
