using System;
using System.Collections.Generic;

namespace UMFST.MIP.Variant1_Bookstore
{
    // JSON gyökér objektum
    public class BookstoreData
    {
        public Store Store { get; set; }
        public List<Author> Authors { get; set; }
        public List<BookJson> Books { get; set; }
        public List<OrderJson> Orders { get; set; }
        public List<Payment> Payments { get; set; }
        public Newtonsoft.Json.Linq.JArray InvalidSamples { get; set; }
    }

    // --- JSON-specifikus modellek (beágyazáshoz) ---

    public class BookJson
    {
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string AuthorId { get; set; }
        public List<string> Categories { get; set; }
        public decimal Price { get; set; }
        public StockInfo StockInfo { get; set; }
    }

    public class StockInfo
    {
        public int Stock { get; set; }
        public bool OnBackorder { get; set; }
    }

    public class OrderJson
    {
        public string Id { get; set; }
        public DateTime OrderDate { get; set; }
        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; }
        public string Status { get; set; }
        public Payment PaymentDetails { get; set; }
    }


    // --- Adatbázis entitás modellek ---

    public class Author
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
    }

    public class Book
    {
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string AuthorId { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public string OrderId { get; set; }
        public string BookIsbn { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class Payment
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime PaymentDate { get; set; }
    }

    // --- Egyéb JSON modellek ---

    // *** ÚJ OSZTÁLY A CÍMHEZ ***
    // Ez az osztály hiányzott, és ez okozta a JSON hibát.
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
    }

    public class Store
    {
        public string Name { get; set; }
        // *** MÓDOSÍTVA ***
        // 'string Address' helyett 'Address Address'
        public Address Address { get; set; }
        public string Currency { get; set; }
        public List<Employee> Employees { get; set; }
    }

    public class Employee
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
    }
}