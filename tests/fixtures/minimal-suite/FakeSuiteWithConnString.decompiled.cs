// Decompiled with ilspycmd
// FakeSuiteWithConnString v1.0.0

using System.Data.SqlClient;

namespace FakeSuite.WithConnString.Data
{
    public class OrderRepository
    {
        private static readonly string ConnectionString =
            "Data Source=fake-server;Initial Catalog=FakeDB;Integrated Security=True";

        public void GetOrder(int id)
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            var cmd = new SqlCommand(
                "SELECT * FROM ordertable WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteReader();
        }
    }

    public class LookupRepository
    {
        private static readonly string ConnectionString =
            "Data Source=fake-server;Initial Catalog=FakeDB;Integrated Security=True";

        public void GetStatuses()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT code, description FROM statuslookup", conn);
            cmd.ExecuteReader();
        }
    }
}
