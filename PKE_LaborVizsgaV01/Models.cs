using Newtonsoft.Json;
using System;
using System.Collections.Generic;

// Névtér: UMFST.MIP.Variant1_Bookstore (vagy a te projektneved, pl. PKE_LaborVizsgaV01)
namespace UMFST.MIP.Variant1_Bookstore
{
    // JSON gyökér objektum
    public class BookstoreData
    {
        public Store Store { get; set; }
        public List<Author> Authors { get; set; }
        public List<Book> Books { get; set; }
        public List<OrderJson> Orders { get; set; }
        public List<Payment> Payments { get; set; }
        public object Meta { get; set; } // Nem használjuk, de létezik
    }


    // 1 uzletek
    public class Address
    {
        [JsonProperty("line1")]
        public string Line1 { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        [JsonProperty("zip")]
        public string Zip { get; set; }
    }

    // alkalmazottak
    public class Employee
    {
        public string Id { get; set; }
        public string Name { get; set; }
        [JsonProperty("role")]
        public string Position { get; set; } // 'role' a JSON-ban, 'Position' a kódban
        [JsonProperty("hiredAt")]
        public DateTime HiredAt { get; set; }
    }

    // adatbázis modell a Store táblához
    public class Store
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Currency { get; set; }
        public Address Address { get; set; }
        public List<Employee> Employees { get; set; }
    }

    //2 szerzők
    public class Author
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
    }

    // 3 konyvek
    public class Book
    {
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string AuthorId { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public List<string> Categories { get; set; }
        // dimensions és weight objektumokat most nem tároljuk el
    }

    // 4 rendelesek
    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class OrderItem
    {
        [JsonProperty("isbn")]
        public string BookIsbn { get; set; }

        [JsonProperty("qty")]
        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        // A 'discount' nem kötelező (O2-nél hiányzik), ezért nullable
        public decimal? Discount { get; set; }
    }

    public class OrderPaymentJson // Ez a 'payment' a 'orders' alatt
    {
        public string Method { get; set; }
        // 'transactions'-t most nem dolgozzuk fel
    }

    public class OrderJson // Ez az 'orders' tömb eleme
    {
        public string Id { get; set; }

        [JsonProperty("date")]
        public string DateString { get; set; } // Stringként olvassuk a "BAD_DATE" miatt

        public Customer Customer { get; set; }
        public List<OrderItem> Items { get; set; }

        [JsonProperty("payment")]
        public OrderPaymentJson PaymentInfo { get; set; }

        public string Status { get; set; }
    }

    // adatbázis modell az Order táblához
    public class Order
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
    }


    // 5 fizetesek (A gyökér 'payments' tömb eleme)
    public class Payment
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public bool Captured { get; set; }
    }
}