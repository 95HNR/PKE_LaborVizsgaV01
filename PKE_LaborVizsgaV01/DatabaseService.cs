using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Text;
using System.Globalization;
using Newtonsoft.Json; // logolashoz

namespace UMFST.MIP.Variant1_Bookstore
{
    // Adatbázis szolgáltatás osztály
    public static class DatabaseService
    {
        private const string DbName = "bookstore.sqlite";
        private static readonly string ConnectionString = $"Data Source={DbName};Version=3;";
        private const string InvalidLogFile = "invalid_bookstore.txt";

        private static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }

        // Adatbázis séma inicializálása
        public static void InitializeDatabase()
        {
            // *** KIEGÉSZÍTETT JAVÍTÁS (A "NAGY KALAPÁCS") ***
            // Kényszerítjük a .NET Garbage Collectort, hogy engedjen el minden nyitott fájlkezelőt
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // *** EZ A JAVÍTÁS: Bezárja az összes háttérkapcsolatot (pl. a 'Restock' után) ***
            SQLiteConnection.ClearAllPools();

            if (File.Exists(DbName))
            {
                File.Delete(DbName);
            }
            SQLiteConnection.CreateFile(DbName);

            using (var con = GetConnection())
            {
                con.Open();
                string sql = @"
                    CREATE TABLE Authors (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Country TEXT
                    );
                    CREATE TABLE Books (
                        Isbn TEXT PRIMARY KEY,
                        Title TEXT NOT NULL,
                        AuthorId TEXT,
                        Category TEXT,
                        Price DECIMAL NOT NULL,
                        Stock INTEGER NOT NULL,
                        FOREIGN KEY(AuthorId) REFERENCES Authors(Id)
                    );
                    CREATE TABLE Customers (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Email TEXT
                    );
                    CREATE TABLE Orders (
                        Id TEXT PRIMARY KEY,
                        CustomerId TEXT,
                        OrderDate DATETIME NOT NULL,
                        Status TEXT,
                        FOREIGN KEY(CustomerId) REFERENCES Customers(Id)
                    );
                    CREATE TABLE OrderItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrderId TEXT,
                        BookIsbn TEXT,
                        Quantity INTEGER NOT NULL,
                        UnitPrice DECIMAL NOT NULL,
                        Discount DECIMAL NOT NULL,
                        FOREIGN KEY(OrderId) REFERENCES Orders(Id),
                        FOREIGN KEY(BookIsbn) REFERENCES Books(Isbn)
                    );
                    CREATE TABLE Payments (
                        Id TEXT PRIMARY KEY,
                        OrderId TEXT,
                        Method TEXT,
                        Amount DECIMAL NOT NULL,
                        Captured INTEGER NOT NULL,
                        FOREIGN KEY(OrderId) REFERENCES Orders(Id)
                    );";

                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Adatok beszúrása az adatbázisba, hibás adatok szűrése és logolása
        public static void BulkInsert(BookstoreData data)
        {
            if (File.Exists(InvalidLogFile)) File.Delete(InvalidLogFile);
            var invalidLog = new StringBuilder();
            invalidLog.AppendLine("=== INVALID ENTRIES LOG ===");
            invalidLog.AppendLine(); // Üres sor

            using (var con = GetConnection())
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // 1 szerzők
                        using (var cmd = new SQLiteCommand("INSERT INTO Authors (Id, Name, Country) VALUES (@Id, @Name, @Country)", con))
                        {
                            foreach (var author in data.Authors)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Id", author.Id);
                                cmd.Parameters.AddWithValue("@Name", author.Name);
                                cmd.Parameters.AddWithValue("@Country", author.Country);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 2 konyvek hibas adatok szűrése és logolása
                        using (var cmd = new SQLiteCommand("INSERT INTO Books (Isbn, Title, AuthorId, Category, Price, Stock) VALUES (@Isbn, @Title, @AuthorId, @Category, @Price, @Stock)", con))
                        {
                            foreach (var book in data.Books)
                            {
                                if (string.IsNullOrWhiteSpace(book.Isbn))
                                {
                                    // *** JAVÍTVA: Formázott logolás ***
                                    invalidLog.AppendLine("[Malformed Book]: (Missing ISBN)");
                                    invalidLog.AppendLine(JsonConvert.SerializeObject(book, Formatting.Indented));
                                    invalidLog.AppendLine("--------------------");
                                    continue;
                                }

                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Isbn", book.Isbn);
                                cmd.Parameters.AddWithValue("@Title", book.Title);
                                cmd.Parameters.AddWithValue("@AuthorId", book.AuthorId);
                                cmd.Parameters.AddWithValue("@Category", book.Categories?.Count > 0 ? book.Categories[0] : null);
                                cmd.Parameters.AddWithValue("@Price", book.Price);
                                cmd.Parameters.AddWithValue("@Stock", book.Stock);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3 ugyfelek
                        var customers = new Dictionary<string, Customer>();
                        foreach (var order in data.Orders)
                        {
                            if (order.Customer != null && !customers.ContainsKey(order.Customer.Id))
                            {
                                customers.Add(order.Customer.Id, order.Customer);
                            }
                        }
                        using (var cmd = new SQLiteCommand("INSERT INTO Customers (Id, Name, Email) VALUES (@Id, @Name, @Email)", con))
                        {
                            foreach (var customer in customers.Values)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Id", customer.Id);
                                cmd.Parameters.AddWithValue("@Name", customer.Name);
                                cmd.Parameters.AddWithValue("@Email", customer.Email);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4 rendelesek es rendelesi tételek (Hibás adatok szűrése és logolása)
                        var orderCmd = new SQLiteCommand("INSERT INTO Orders (Id, CustomerId, OrderDate, Status) VALUES (@Id, @CustomerId, @OrderDate, @Status)", con);
                        var itemCmd = new SQLiteCommand("INSERT INTO OrderItems (OrderId, BookIsbn, Quantity, UnitPrice, Discount) VALUES (@OrderId, @BookIsbn, @Quantity, @UnitPrice, @Discount)", con);

                        foreach (var order in data.Orders)
                        {
                            bool isInvalid = false;
                            string reason = "";

                            if (order.Customer?.Name == "Invalid Entry")
                            {
                                isInvalid = true;
                                reason = "(Name is 'Invalid Entry')";
                            }

                            if (!DateTime.TryParse(order.DateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var orderDate))
                            {
                                isInvalid = true;
                                reason = "(Date is 'BAD_DATE')";
                            }

                            if (isInvalid)
                            {
                                //Formázott logolás
                                invalidLog.AppendLine($"[Invalid Order]: {reason}");
                                invalidLog.AppendLine(JsonConvert.SerializeObject(order, Formatting.Indented));
                                invalidLog.AppendLine("--------------------");
                                continue;
                            }

                            orderCmd.Parameters.Clear();
                            orderCmd.Parameters.AddWithValue("@Id", order.Id);
                            orderCmd.Parameters.AddWithValue("@CustomerId", order.Customer?.Id);
                            orderCmd.Parameters.AddWithValue("@OrderDate", orderDate.ToUniversalTime());
                            orderCmd.Parameters.AddWithValue("@Status", order.Status);
                            orderCmd.ExecuteNonQuery();

                            if (order.Items != null)
                            {
                                foreach (var item in order.Items)
                                {
                                    if (item.Quantity <= 0)
                                    {
                                        //Formázott logolás
                                        invalidLog.AppendLine($"[Invalid OrderItem in {order.Id}]: (Quantity <= 0)");
                                        invalidLog.AppendLine(JsonConvert.SerializeObject(item, Formatting.Indented));
                                        invalidLog.AppendLine("--------------------");
                                        continue;
                                    }

                                    itemCmd.Parameters.Clear();
                                    itemCmd.Parameters.AddWithValue("@OrderId", order.Id);
                                    itemCmd.Parameters.AddWithValue("@BookIsbn", item.BookIsbn);
                                    itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    itemCmd.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                                    itemCmd.Parameters.AddWithValue("@Discount", item.Discount ?? 0m);
                                    itemCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 5 fizetések
                        using (var cmd = new SQLiteCommand("INSERT INTO Payments (Id, OrderId, Method, Amount, Captured) VALUES (@Id, @OrderId, @Method, @Amount, @Captured)", con))
                        {
                            foreach (var payment in data.Payments)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Id", payment.Id);
                                cmd.Parameters.AddWithValue("@OrderId", payment.OrderId);
                                cmd.Parameters.AddWithValue("@Method", payment.Method);
                                cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                                cmd.Parameters.AddWithValue("@Captured", payment.Captured ? 1 : 0);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            File.WriteAllText(InvalidLogFile, invalidLog.ToString());
        }

        // 1 Könyvek fül
        public static DataTable GetBooks(string searchTerm = null)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT b.Isbn, b.Title, a.Name AS Author, b.Category, b.Price, b.Stock 
                FROM Books b
                LEFT JOIN Authors a ON b.AuthorId = a.Id";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " WHERE b.Title LIKE @Search OR a.Name LIKE @Search";
            }
            sql += " ORDER BY b.Title";

            using (var con = GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
                }
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        // Készletfeltöltés
        public static void RestockBook(string isbn, int amount)
        {
            string sql = "UPDATE Books SET Stock = Stock + @Amount WHERE Isbn = @Isbn";
            using (var con = GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                con.Open();
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Isbn", isbn);
                cmd.ExecuteNonQuery();
            }
        }

        // 2 Rendelések fül
        public static DataTable GetOrders()
        {
            var dt = new DataTable();
            string sql = @"
                SELECT o.Id, c.Name AS Customer, o.OrderDate, o.Status, 
                       CASE 
                         WHEN (SELECT p.Captured FROM Payments p WHERE p.OrderId = o.Id LIMIT 1) = 1 THEN 'successful'
                         ELSE 'pending/failed'
                       END AS PaymentStatus
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerId = c.Id
                ORDER BY o.OrderDate DESC";

            using (var con = GetConnection())
            using (var adapter = new SQLiteDataAdapter(sql, con))
            {
                adapter.Fill(dt);
            }
            return dt;
        }

        // Rendelési tételek lekérdezése
        public static DataTable GetOrderItems(string orderId)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT b.Title, oi.Quantity, oi.UnitPrice, oi.Discount,
                       (oi.Quantity * oi.UnitPrice - oi.Discount) AS Subtotal
                FROM OrderItems oi
                LEFT JOIN Books b ON oi.BookIsbn = b.Isbn
                WHERE oi.OrderId = @OrderId";

            using (var con = GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        // Összes rendelési érték lekérdezése
        public static decimal GetOrderTotal(string orderId)
        {
            string sql = "SELECT SUM(Quantity * UnitPrice - Discount) FROM OrderItems WHERE OrderId = @OrderId";
            using (var con = GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
            }
        }


        // 3 Riportok ful
        public static string GenerateReport(string storeName, string currency)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{storeName.ToUpper()} REPORT ({DateTime.Now.Year})");
            sb.AppendLine("=======================");

            using (var con = GetConnection())
            {
                con.Open();

                // Teljes eladás
                string salesSql = "SELECT SUM(Amount) FROM Payments WHERE Captured = 1";
                using (var cmd = new SQLiteCommand(salesSql, con))
                {
                    var result = cmd.ExecuteScalar();
                    decimal totalSales = result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                    sb.AppendLine($"Total sales: {totalSales:F2} {currency}");
                }

                // Alacsony készlet
                sb.AppendLine("Books below stock threshold (5):");
                string stockSql = "SELECT Title, Stock FROM Books WHERE Stock < 5";
                using (var cmd = new SQLiteCommand(stockSql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        sb.AppendLine("- None");
                    }
                    while (reader.Read())
                    {
                        sb.AppendLine($"- {reader["Title"]} ({reader["Stock"]} left)");
                    }
                }

                // Legjobban fogyó könyv
                string bestSellerSql = @"
                    SELECT b.Title, SUM(oi.Quantity) AS TotalSold 
                    FROM OrderItems oi 
                    JOIN Books b ON oi.BookIsbn = b.Isbn
                    GROUP BY b.Title 
                    ORDER BY TotalSold DESC 
                    LIMIT 1";
                using (var cmd = new SQLiteCommand(bestSellerSql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        sb.AppendLine($"Best-selling: {reader["Title"]} ({reader["TotalSold"]} units sold)");
                    }
                    else
                    {
                        sb.AppendLine("Best-selling: N/A");
                    }
                }
            }
            return sb.ToString();
        }
    }
}