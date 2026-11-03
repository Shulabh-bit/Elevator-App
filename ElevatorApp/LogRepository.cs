using System;
using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;

namespace ElevatorApp
{
    public static class LogRepository
    {
        private static readonly object Sync = new object();

        
        private const string Server = @"(localdb)\MSSQLLocalDB";
        private const string DbName = "ElevatorLogDB";

        
        private static readonly string MdfPath =
            Path.Combine(AppContext.BaseDirectory, "Data", DbName + ".mdf");

        private static string MasterConn
        {
            get { return "Server=" + Server + ";Database=master;Integrated Security=true;TrustServerCertificate=true"; }
        }
        private static string DbConn
        {
            get { return "Server=" + Server + ";Database=" + DbName + ";Integrated Security=true;TrustServerCertificate=true"; }
        }

        private const string SelectCols = "SELECT Id, ElevatorId, Floor, Status, LoggedAt FROM ElevatorLog";

        public static void Initialise()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MdfPath));
            lock (Sync)
            {
                using (var conn = new SqlConnection(MasterConn))
                {
                    conn.Open();

                    
                    object registered;
                    using (var cmd = new SqlCommand(
                        "SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(@n) AND type = 0", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", DbName);
                        registered = cmd.ExecuteScalar();
                    }

                    
                    if (registered != null &&
                        !string.Equals((string)registered, MdfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var cmd = new SqlCommand("EXEC sp_detach_db @dbname = @n", conn))
                        {
                            cmd.Parameters.AddWithValue("@n", DbName);
                            cmd.ExecuteNonQuery();
                        }
                        registered = null;
                    }

                    if (registered == null)
                    {
                        string sql = File.Exists(MdfPath)
                            ? "CREATE DATABASE [" + DbName + "] ON (FILENAME = N'" + MdfPath + "') FOR ATTACH"
                            : "CREATE DATABASE [" + DbName + "] ON PRIMARY (NAME = N'" + DbName +
                              "', FILENAME = N'" + MdfPath + "')";
                        using (var cmd = new SqlCommand(sql, conn)) cmd.ExecuteNonQuery();
                    }
                }

                using (var conn = new SqlConnection(DbConn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "IF OBJECT_ID('dbo.ElevatorLog') IS NULL " +
                        "CREATE TABLE dbo.ElevatorLog (" +
                        "Id INT IDENTITY(1,1) PRIMARY KEY, " +
                        "ElevatorId INT NOT NULL, Floor INT NOT NULL, " +
                        "Status NVARCHAR(100) NOT NULL, LoggedAt DATETIME NOT NULL)", conn))
                        cmd.ExecuteNonQuery();
                }
            }
        }

       
        private static T WithAdapter<T>(string select, Func<SqlDataAdapter, DataTable, T> work)
        {
            try
            {
                lock (Sync)
                    using (var conn = new SqlConnection(DbConn))
                    using (var adapter = new SqlDataAdapter(select, conn))
                    using (var builder = new SqlCommandBuilder(adapter))
                    {
                        adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                        var table = new DataTable("ElevatorLog");
                        adapter.Fill(table);
                        return work(adapter, table);
                    }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Database error: " + ex.Message, ex);
            }
        }

        public static DataTable GetAll()
        {
            return WithAdapter(SelectCols + " ORDER BY Id DESC", (a, t) => t);
        }

        public static void Add(int elevatorId, int floor, string status)
        {
            WithAdapter("SELECT TOP 0 Id, ElevatorId, Floor, Status, LoggedAt FROM ElevatorLog",
                (adapter, table) =>
                {
                    DataRow r = table.NewRow();
                    r["ElevatorId"] = elevatorId;
                    r["Floor"] = floor;
                    r["Status"] = status;
                    r["LoggedAt"] = DateTime.Now;
                    table.Rows.Add(r);
                    adapter.Update(table);   
                    return 0;
                });
        }

        public static void Delete(int id)
        {
            WithAdapter(SelectCols + " WHERE Id = " + id, (adapter, table) =>
            {
                for (int i = table.Rows.Count - 1; i >= 0; i--) table.Rows[i].Delete();
                adapter.Update(table);
                return 0;
            });
        }

        public static void ClearAll()
        {
            WithAdapter(SelectCols, (adapter, table) =>
            {
                for (int i = table.Rows.Count - 1; i >= 0; i--) table.Rows[i].Delete();
                adapter.Update(table);
                return 0;
            });
        }
    }
}