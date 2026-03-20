// Decompiled with ilspycmd
// FakeSuiteDynamicSql v1.0.0

using System.Data.SqlClient;

namespace FakeSuite.DynamicSql.Data
{
    public class DynamicQueryRunner
    {
        private readonly string _connectionString;

        public DynamicQueryRunner(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void RunQuery(string tableName)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = "SELECT * FROM " + tableName;
            var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteReader();
        }
    }
}
