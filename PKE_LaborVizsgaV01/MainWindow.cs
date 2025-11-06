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
    // Fő ablak osztálya
    public partial class MainWindow : Form
    {
        // A JSON adatfájl URL címe
        private const string JsonUrl = "https://cdn.shopify.com/s/files/1/0883/3282/8936/files/data_bookstore_final.json?v=1762418524";
        // A kért riportfájl
        private const string ReportFile = "sales_report.txt";
        // Az InvalidLogFile konstanst áthelyeztük a DatabaseService.cs-be

        // A változók itt kapnak értéket, hogy ne legyenek 'null'
        private string storeName = string.Empty;
        private string storeCurrency = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Ablak betöltése esemény kezelője
        private async void MainWindow_Load(object sender, EventArgs e)
        {
            // DataGridView oszlopok automatikus generálása
            dgvBooks.AutoGenerateColumns = true;
            dgvOrders.AutoGenerateColumns = true;
            dgvOrderItems.AutoGenerateColumns = true;
            lblStatus.Text = "Ready. Please reset the database to load data.";
        }

        // Adatbázis visszaállítása gomb kezelője
        private async void btnReset_Click(object sender, EventArgs e)
        {
            lblStatus.Text = "Resetting database... Downloading JSON...";
            this.Enabled = false;

            try
            {
                // 1 json letöltése
                string jsonContent;
                using (var client = new HttpClient())
                {
                    jsonContent = await client.GetStringAsync(JsonUrl);
                }
                lblStatus.Text = "JSON downloaded. Parsing data...";

                // 2 json feldolgozása
                // ez a reszt a JSON-ból beolvassa az üzlet nevét és pénznemét
                var data = JsonConvert.DeserializeObject<BookstoreData>(jsonContent);
                storeName = data.Store.Name;
                storeCurrency = data.Store.Currency;

                // 3 adatbázis inicializálása
                lblStatus.Text = "Initializing database schema...";
                await Task.Run(() => DatabaseService.InitializeDatabase());

                // 4 adatok importálása esetleges hibák naplózása es szurese
                lblStatus.Text = "Importing data and logging invalid entries...";
                await Task.Run(() => DatabaseService.BulkInsert(data));

                // 5 ui frissítése
                lblStatus.Text = "Loading UI components...";
                LoadBooksTab();
                LoadOrdersTab();
                LoadReportsTab();

                // Frissített sikeres üzenet
                lblStatus.Text = "Database reset successful! (Invalid entries logged to invalid_bookstore.txt)";
            }
            catch (HttpRequestException httpEx)
            {
                lblStatus.Text = "Error: Failed to download file.";
                MessageBox.Show(
                   "Nem sikerült letölteni a JSON adatfájlt a szerverről. Ellenőrizd az internetkapcsolatot.\n\nRészletek: " + httpEx.Message,
                   "Letöltési Hiba",
                   MessageBoxButtons.OK,
                   MessageBoxIcon.Error);
            }
            catch (JsonException jsonEx)
            {
                lblStatus.Text = "Error: Failed to process JSON data.";
                MessageBox.Show(
                         "Hiba történt a JSON fájl feldolgozása közben.\n\nRészletek: " + jsonEx.Message,
                         "JSON Feldolgozási Hiba",
                         MessageBoxButtons.OK,
                         MessageBoxIcon.Warning);
            }
            catch (DllNotFoundException dllEx)
            {
                lblStatus.Text = "Error: Critical component missing.";
                MessageBox.Show(
                         "Hiba: Egy kritikus adatbázis-komponens (e_sqlite3.dll) hiányzik vagy nem tölthető be.\n\nA program futása folytatódik, de az adatbázis-műveletek nem fognak működni.",
                         "Adatbázis Hiba",
                         MessageBoxButtons.OK,
                         MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "An unexpected error occurred.";
                MessageBox.Show(
                         "Váratlan hiba történt az adatbázis-visszaállítás során.\n\nRészletek: " + ex.Message,
                         "Általános Hiba",
                         MessageBoxButtons.OK,
                         MessageBoxIcon.Error);
            }
            finally
            {
                this.Enabled = true;
            }
        }


        // 1 könyvek ful

        private void LoadBooksTab(string searchTerm = null)
        {
            try
            {
                dgvBooks.DataSource = DatabaseService.GetBooks(searchTerm);
                if (dgvBooks.Columns.Count > 0)
                {
                    dgvBooks.Columns["Isbn"].Width = 120;
                    dgvBooks.Columns["Title"].Width = 250;
                    dgvBooks.Columns["Author"].Width = 150;
                    dgvBooks.Columns["Price"].DefaultCellStyle.Format = "c";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a könyvek betöltésekor: {ex.Message}", "Adatbázis Hiba");
            }
        }

        // Szűrés gomb kezelője
        private void btnFilter_Click(object sender, EventArgs e)
        {
            LoadBooksTab(txtSearchBooks.Text);
        }

        // Újrakészletezés gomb kezelője
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
                MessageBox.Show($"Hiba az újrakészletezéskor: {ex.Message}", "Adatbázis Hiba");
            }
        }

        // 2 rendelesek ful

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
                MessageBox.Show($"Hiba a rendelések betöltésekor: {ex.Message}", "Adatbázis Hiba");
            }
        }

        // Rendelés kiválasztás változás kezelése
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
                lblOrderTotal.Text = $"Total: {total:F2} {storeCurrency}";

                string status = selectedRow.Cells["Status"].Value.ToString().ToLower();
                string paymentStatus = selectedRow.Cells["PaymentStatus"].Value.ToString().ToLower();

                if (status == "pending" || paymentStatus.Contains("pending"))
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
                MessageBox.Show($"Hiba a rendelés részleteinek betöltésekor: {ex.Message}", "Adatbázis Hiba");
            }
        }

        // 3 riportok ful
        private void LoadReportsTab()
        {
            try
            {
                //jsonbol generalt ertekek hasznalata a storeName es storeCurrency valtozokban
                txtReport.Text = DatabaseService.GenerateReport(storeName, storeCurrency);
            }
            catch (Exception ex)
            {
                txtReport.Text = $"Hiba a riport generálásakor: {ex.Message}";
            }
        }

        // Riport exportálása fájlba
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