// Decompiled with ilspycmd
// FakeSuite v1.0.0

using System;
using System.Collections.Generic;

namespace FakeSuite.Orders
{
    /// <summary>
    /// Manages order lifecycle for the Fake Suite application.
    /// </summary>
    public class OrderManager
    {
        private readonly IOrderRepository _repository;

        public OrderManager(IOrderRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Retrieves an order by its identifier.
        /// </summary>
        public Order GetOrder(string orderId)
        {
            return _repository.FindById(orderId);
        }

        /// <summary>
        /// Creates a new order and persists it.
        /// </summary>
        public Order CreateOrder(string customerId, IEnumerable<OrderLine> lines)
        {
            var order = new Order { CustomerId = customerId, Lines = new List<OrderLine>(lines) };
            _repository.Save(order);
            return order;
        }
    }

    public interface IOrderRepository
    {
        Order FindById(string id);
        void Save(Order order);
    }

    public class Order
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public List<OrderLine> Lines { get; set; }
        public string Status { get; set; }
    }

    public class OrderLine
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

namespace FakeSuite.Data
{
    using System.Data.SqlClient;

    /// <summary>
    /// ADO.NET data access for orders. Reads from ordertable and orderline.
    /// </summary>
    public class SqlOrderRepository : FakeSuite.Orders.IOrderRepository
    {
        private readonly string _connectionString;

        public SqlOrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public FakeSuite.Orders.Order FindById(string id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM ordertable WHERE fcorderid = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            // ... mapping omitted for brevity
            return null;
        }

        public void Save(FakeSuite.Orders.Order order)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var cmd = new SqlCommand("INSERT INTO ordertable (fcorderid, fccustid, fcstatus) VALUES (@id, @cust, @status)", conn);
            cmd.Parameters.AddWithValue("@id", order.Id);
            cmd.Parameters.AddWithValue("@cust", order.CustomerId);
            cmd.Parameters.AddWithValue("@status", order.Status ?? "OP");
            cmd.ExecuteNonQuery();
        }
    }
}
