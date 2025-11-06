using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Text;

// Namespace changed
namespace UMFST.MIP.Variant1_Bookstore
{
    public static class DatabaseService
    {
        private const string DbName = "bookstore.sqlite";
        private static readonly string ConnectionString = $"Data Source={DbName};Version=3;";

        private static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }

        public static void InitializeDatabase()
        {
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
                        Bio TEXT
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
                        Amount DECIMAL NOT NULL,
                        Status TEXT NOT NULL,
                        PaymentDate DATETIME,
                        FOREIGN KEY(OrderId) REFERENCES Orders(Id)
                    );";

                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void BulkInsert(BookstoreData data)
        {
            using (var con = GetConnection())
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Insert Authors
                        using (var cmd = new SQLiteCommand("INSERT INTO Authors (Id, Name, Bio) VALUES (@Id, @Name, @Bio)", con))
                        {
                            foreach (var author in data.Authors)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Id", author.Id);
                                cmd.Parameters.AddWithValue("@Name", author.Name);
                                cmd.Parameters.AddWithValue("@Bio", author.Bio);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Insert Books
                        using (var cmd = new SQLiteCommand("INSERT INTO Books (Isbn, Title, AuthorId, Category, Price, Stock) VALUES (@Isbn, @Title, @AuthorId, @Category, @Price, @Stock)", con))
                        {
                            foreach (var book in data.Books)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Isbn", book.Isbn);
                                cmd.Parameters.AddWithValue("@Title", book.Title);
                                cmd.Parameters.AddWithValue("@AuthorId", book.AuthorId);
                                cmd.Parameters.AddWithValue("@Category", book.Categories?.Count > 0 ? book.Categories[0] : null);
                                cmd.Parameters.AddWithValue("@Price", book.Price);
                                cmd.Parameters.AddWithValue("@Stock", book.StockInfo.Stock);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Insert Customers
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

                        // Insert Orders and OrderItems
                        var orderCmd = new SQLiteCommand("INSERT INTO Orders (Id, CustomerId, OrderDate, Status) VALUES (@Id, @CustomerId, @OrderDate, @Status)", con);
                        var itemCmd = new SQLiteCommand("INSERT INTO OrderItems (OrderId, BookIsbn, Quantity, UnitPrice, Discount) VALUES (@OrderId, @BookIsbn, @Quantity, @UnitPrice, @Discount)", con);

                        foreach (var order in data.Orders)
                        {
                            orderCmd.Parameters.Clear();
                            orderCmd.Parameters.AddWithValue("@Id", order.Id);
                            orderCmd.Parameters.AddWithValue("@CustomerId", order.Customer?.Id);
                            orderCmd.Parameters.AddWithValue("@OrderDate", order.OrderDate);
                            orderCmd.Parameters.AddWithValue("@Status", order.Status);
                            orderCmd.ExecuteNonQuery();

                            if (order.Items != null)
                            {
                                foreach (var item in order.Items)
                                {
                                    itemCmd.Parameters.Clear();
                                    itemCmd.Parameters.AddWithValue("@OrderId", order.Id);
                                    itemCmd.Parameters.AddWithValue("@BookIsbn", item.BookIsbn);
                                    itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    itemCmd.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                                    itemCmd.Parameters.AddWithValue("@Discount", item.Discount);
                                    itemCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Insert Payments
                        using (var cmd = new SQLiteCommand("INSERT INTO Payments (Id, OrderId, Amount, Status, PaymentDate) VALUES (@Id, @OrderId, @Amount, @Status, @PaymentDate)", con))
                        {
                            foreach (var order in data.Orders)
                            {
                                if (order.PaymentDetails != null)
                                {
                                    cmd.Parameters.Clear();
                                    cmd.Parameters.AddWithValue("@Id", order.PaymentDetails.Id);
                                    cmd.Parameters.AddWithValue("@OrderId", order.Id);
                                    cmd.Parameters.AddWithValue("@Amount", order.PaymentDetails.Amount);
                                    cmd.Parameters.AddWithValue("@Status", order.PaymentDetails.Status);
                                    cmd.Parameters.AddWithValue("@PaymentDate", order.PaymentDetails.PaymentDate);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            foreach (var payment in data.Payments)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@Id", payment.Id);
                                cmd.Parameters.AddWithValue("@OrderId", payment.OrderId);
                                cmd.Parameters.AddWithValue("@Amount", payment.Amount);
                                cmd.Parameters.AddWithValue("@Status", payment.Status);
                                cmd.Parameters.AddWithValue("@PaymentDate", payment.PaymentDate);
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
        }

        // --- Tab 1: Books ---
        public static DataTable GetBooks(string searchTerm = null)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT b.Isbn, b.Title, a.Name AS Author, b.Category, b.Price, b.Stock 
                FROM Books b
                JOIN Authors a ON b.AuthorId = a.Id";

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

        // --- Tab 2: Orders ---
        public static DataTable GetOrders()
        {
            var dt = new DataTable();
            string sql = @"
                SELECT o.Id, c.Name AS Customer, o.OrderDate, o.Status, 
                       (SELECT p.Status FROM Payments p WHERE p.OrderId = o.Id LIMIT 1) AS PaymentStatus
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

        public static DataTable GetOrderItems(string orderId)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT b.Title, oi.Quantity, oi.UnitPrice, oi.Discount,
                       (oi.Quantity * oi.UnitPrice * (1 - oi.Discount)) AS Subtotal
                FROM OrderItems oi
                JOIN Books b ON oi.BookIsbn = b.Isbn
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

        public static decimal GetOrderTotal(string orderId)
        {
            string sql = "SELECT SUM(Quantity * UnitPrice * (1 - Discount)) FROM OrderItems WHERE OrderId = @OrderId";
            using (var con = GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                con.Open();
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                var result = cmd.ExecuteScalar();
                return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
            }
        }

        // --- Tab 3: Reports ---
        public static string GenerateReport(string storeName, string currency)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{storeName.ToUpper()} REPORT ({DateTime.Now.Year})");
            sb.AppendLine("=======================");

            using (var con = GetConnection())
            {
                con.Open();

                // Total Sales
                string salesSql = "SELECT SUM(Amount) FROM Payments WHERE Status = 'successful'";
                using (var cmd = new SQLiteCommand(salesSql, con))
                {
                    var result = cmd.ExecuteScalar();
                    decimal totalSales = result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                    sb.AppendLine($"Total sales: {totalSales:F2} {currency}");
                }

                // Low Stock
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

                // Best-selling book
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