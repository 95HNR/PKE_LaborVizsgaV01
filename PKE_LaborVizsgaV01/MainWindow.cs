using Newtonsoft.Json;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UMFST.MIP.Variant1_Bookstore
{
    public partial class MainWindow : Form
    {
        private const string JsonUrl = "https://cdn.shopify.com/s/files/1/0883/3282/8936/files/data_bookstore_final.json?v=1762418524";
        private const string InvalidLogFile = "invalid_bookstore.txt";
        private const string ReportFile = "sales_report.txt";

        private string _storeName = "BOOKVERSE";
        private string _storeCurrency = "EUR";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Load(object sender, EventArgs e)
        {
            dgvBooks.AutoGenerateColumns = true;
            dgvOrders.AutoGenerateColumns = true;
            dgvOrderItems.AutoGenerateColumns = true;
            lblStatus.Text = "Ready. Please reset the database to load data.";
        }

        // Event handler a 'Reset' gombhoz
        private async void btnReset_Click(object sender, EventArgs e)
        {
            lblStatus.Text = "Resetting database... Downloading JSON...";
            this.Enabled = false;

            try
            {
                // 1. JSON letöltése
                string jsonContent;
                using (var client = new HttpClient())
                {
                    jsonContent = await client.GetStringAsync(JsonUrl);
                }
                lblStatus.Text = "JSON downloaded. Parsing data...";

                // 2. JSON feldolgozása
                var data = JsonConvert.DeserializeObject<BookstoreData>(jsonContent);
                _storeName = data.Store.Name;
                _storeCurrency = data.Store.Currency;

                // 3. Hibás minták logolása
                if (File.Exists(InvalidLogFile)) File.Delete(InvalidLogFile);
                if (data.InvalidSamples != null)
                {
                    var invalidLog = new StringBuilder();
                    invalidLog.AppendLine("=== INVALID SAMPLES LOG ===");
                    foreach (var sample in data.InvalidSamples)
                    {
                        // *** ITT A JAVÍTÁS ***
                        // Egyértelműsítve a Formatting enum
                        invalidLog.AppendLine(sample.ToString(Newtonsoft.Json.Formatting.None));
                    }
                    File.WriteAllText(InvalidLogFile, invalidLog.ToString());
                }

                // 4. Adatbázis inicializálása
                lblStatus.Text = "Initializing database schema...";
                await Task.Run(() => DatabaseService.InitializeDatabase());

                // 5. Adatok importálása
                lblStatus.Text = "Importing data into database...";
                await Task.Run(() => DatabaseService.BulkInsert(data));

                // 6. UI frissítése
                lblStatus.Text = "Loading UI components...";
                LoadBooksTab();
                LoadOrdersTab();
                LoadReportsTab();

                lblStatus.Text = "Database reset successful!";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "An error occurred during reset.";
                MessageBox.Show($"Error: {ex.Message}", "Database Reset Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Enabled = true;
            }
        }

        // --- Tab 1: Könyvek ---

        private void LoadBooksTab(string searchTerm = null)
        {
            try
            {
                dgvBooks.DataSource = DatabaseService.GetBooks(searchTerm);
                dgvBooks.Columns["Isbn"].Width = 120;
                dgvBooks.Columns["Title"].Width = 250;
                dgvBooks.Columns["Author"].Width = 150;
                dgvBooks.Columns["Price"].DefaultCellStyle.Format = "c";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading books: {ex.Message}");
            }
        }

        // Event handler a 'Filter' (Search) gombhoz
        private void btnFilter_Click(object sender, EventArgs e)
        {
            LoadBooksTab(txtSearchBooks.Text);
        }

        private void btnRestock_Click(object sender, EventArgs e)
        {
            if (dgvBooks.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a book to restock.", "No Book Selected");
                return;
            }

            try
            {
                string isbn = dgvBooks.SelectedRows[0].Cells["Isbn"].Value.ToString();
                DatabaseService.RestockBook(isbn, 10);

                int selectedRow = dgvBooks.SelectedRows[0].Index;
                LoadBooksTab(txtSearchBooks.Text);
                dgvBooks.ClearSelection();
                if (dgvBooks.Rows.Count > selectedRow)
                {
                    dgvBooks.Rows[selectedRow].Selected = true;
                }

                lblStatus.Text = $"Restocked '{isbn}' by 10 units.";
                LoadReportsTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restocking book: {ex.Message}");
            }
        }

        // --- Tab 2: Rendelések ---

        private void LoadOrdersTab()
        {
            try
            {
                dgvOrders.DataSource = DatabaseService.GetOrders();
                dgvOrders.ClearSelection();
                dgvOrderItems.DataSource = null;
                lblOrderTotal.Text = "Total: 0.00";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading orders: {ex.Message}");
            }
        }

        private void dgvOrders_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvOrders.SelectedRows.Count == 0)
            {
                dgvOrderItems.DataSource = null;
                lblOrderTotal.Text = "Total: 0.00";
                return;
            }

            try
            {
                var selectedRow = dgvOrders.SelectedRows[0];
                string orderId = selectedRow.Cells["Id"].Value.ToString();

                dgvOrderItems.DataSource = DatabaseService.GetOrderItems(orderId);

                decimal total = DatabaseService.GetOrderTotal(orderId);
                lblOrderTotal.Text = $"Total: {total:F2} {_storeCurrency}";

                string status = selectedRow.Cells["Status"].Value.ToString().ToLower();
                string paymentStatus = selectedRow.Cells["PaymentStatus"].Value.ToString().ToLower();

                if (status == "pending" || status == "failed" || paymentStatus == "pending" || paymentStatus == "failed")
                {
                    selectedRow.DefaultCellStyle.BackColor = Color.LightYellow;
                    selectedRow.DefaultCellStyle.ForeColor = Color.Black;
                }
                else
                {
                    selectedRow.DefaultCellStyle.BackColor = dgvOrders.DefaultCellStyle.BackColor;
                    selectedRow.DefaultCellStyle.ForeColor = dgvOrders.DefaultCellStyle.ForeColor;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading order details: {ex.Message}");
            }
        }

        // --- Tab 3: Riportok ---
        private void LoadReportsTab()
        {
            try
            {
                txtReport.Text = DatabaseService.GenerateReport(_storeName, _storeCurrency);
            }
            catch (Exception ex)
            {
                txtReport.Text = $"Error generating report: {ex.Message}";
            }
        }

        // Event handler az 'Export' gombhoz
        private void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllText(ReportFile, txtReport.Text);
                MessageBox.Show($"Report successfully exported to '{ReportFile}' in the application folder.", "Export Successful");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export report: {ex.Message}", "Export Error");
            }
        }
    }
}