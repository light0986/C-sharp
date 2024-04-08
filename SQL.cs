using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Json測試
{
    public class SQL
    {
        private static SqlConnection Connection { get; set; }

        public static bool TryConnection(string Server, string DataBase, string ID, string Password)
        {
            try
            {
                string str = "Data Source={0}; Initial Catalog={1}; Integrated Security=false; User ID={2}; Password={3};";
                string ConnectionString = String.Format(str, Server, DataBase, ID, Password);
                Connection = new SqlConnection(ConnectionString);
                Connection.Open();

                return true;
            }
            catch { }

            return false;
        }

        public static object Read(string QueryString)
        {
            if (Connection != null)
            {
                try
                {
                    using (var cmd = Connection.CreateCommand())
                    {
                        cmd.CommandText = QueryString;

                        using (var reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);

                            return dt;
                        };
                    }
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            return null;
        }

        public static bool NonQuery(string QueryString)
        {
            if (Connection != null)
            {
                try
                {
                    using (var cmd = Connection.CreateCommand())
                    {
                        cmd.CommandText = QueryString;
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public static void TryClose()
        {
            try
            {
                if (Connection != null)
                {
                    Connection.Close();
                    Connection = null;
                }
            }
            catch { }
        }
    }
}
