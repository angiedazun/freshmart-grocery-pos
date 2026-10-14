using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace GroceryPOS.Data
{
    /// <summary>Thin ADO.NET helper. The database and schema are created automatically on first run.</summary>
    public static class Db
    {
        public static string Server { get; private set; }
        public static string Database { get; private set; }

        static Db()
        {
            Server = ConfigurationManager.AppSettings["SqlServer"];
            Database = ConfigurationManager.AppSettings["Database"];
            if (string.IsNullOrWhiteSpace(Server)) Server = @".\SQLEXPRESS";
            if (string.IsNullOrWhiteSpace(Database)) Database = "GroceryPOSDB";
        }

        static string BuildConn(string db)
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = Server,
                InitialCatalog = db,
                IntegratedSecurity = true,
                ConnectTimeout = 8,
                ApplicationName = "GroceryPOS",
                TrustServerCertificate = true
            };
            return b.ConnectionString;
        }

        public static string ConnectionString { get { return BuildConn(Database); } }

        public static SqlConnection Open()
        {
            var c = new SqlConnection(ConnectionString);
            c.Open();
            return c;
        }

        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        public static DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                var dt = new DataTable();
                using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
                return dt;
            }
        }

        public static int Exec(string sql, params SqlParameter[] ps)
        {
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                var v = cmd.ExecuteScalar();
                return v == DBNull.Value ? null : v;
            }
        }

#if DEBUG
        public static void SetDatabase(string name) { Database = name; }

        public static void DropDatabase()
        {
            SqlConnection.ClearAllPools();
            using (var c = new SqlConnection(BuildConn("master")))
            {
                c.Open();
                using (var cmd = new SqlCommand(
                    "IF DB_ID(@n) IS NOT NULL BEGIN DECLARE @s NVARCHAR(500) = N'ALTER DATABASE ' + QUOTENAME(@n) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@n); EXEC(@s); END", c))
                {
                    cmd.Parameters.AddWithValue("@n", Database);
                    cmd.ExecuteNonQuery();
                }
            }
        }
#endif

        /// <summary>Creates the database (if missing), the schema and the starter data.</summary>
        public static void Initialize()
        {
            using (var c = new SqlConnection(BuildConn("master")))
            {
                c.Open();
                using (var cmd = new SqlCommand(
                    "IF DB_ID(@n) IS NULL BEGIN DECLARE @s NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@n); EXEC(@s); END", c))
                {
                    cmd.Parameters.AddWithValue("@n", Database);
                    cmd.ExecuteNonQuery();
                }
            }

            foreach (var stmt in Schema.Statements) Exec(stmt);
            Schema.SeedIfEmpty();
            Schema.SeedWeighedOnce();
        }
    }
}
