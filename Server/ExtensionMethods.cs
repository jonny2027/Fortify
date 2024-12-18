using Microsoft.Data.Sqlite;
using MySqlConnector;
using System.Data;
using System.Data.Common;
using System.Text;

namespace PersistenceServer
{
    public static class ExtensionMethods
    {
        public static string ReadMmoString(this BinaryReader reader)
        {
            try
            {
                int strLen = reader.ReadInt32();
                var stringbytes = reader.ReadBytes(strLen);
                string result = Encoding.UTF8.GetString(stringbytes);
                return result;
            }
            catch {
                return "";
            }
        }

        /*
        * DataTable extensions
        */

        public static bool HasRows(this DataTable dt)
        {
            return dt.Rows.Count > 0;
        }

        public static void PrintColumnNames(this DataTable dt)
        {
            List<string> columns = new();
            foreach(DataColumn column in dt.Columns)
            {
                columns.Add(column.ColumnName);
            }
            Console.WriteLine(string.Join(", ", columns));
        }

        public static int? GetBigInt(this DataTable dt, int row, string column)
        {
            return (int?)dt.Rows[row].Field<long?>(column);
        }

        public static int? GetBigInt(this DataTable dt, int row, int column)
        {
            return (int?)dt.Rows[row].Field<long?>(column);
        }

        public static int? GetInt(this DataTable dt, int row, string column)
        {
            // annoyingly enough, sqlite's integer is of different (8 bytes) size from mysql's (4 bytes)
            try
            {
                return dt.Rows[row].Field<int?>(column);
            }
            catch
            {
                return dt.GetBigInt(row, column);
            }
        }

        public static int? GetInt(this DataTable dt, int row, int column)
        {
            // annoyingly enough, sqlite's integer is of different (8 bytes) size from mysql's (4 bytes)
            try
            {
                return dt.Rows[row].Field<int?>(column);
            }
            catch
            {
                return dt.GetBigInt(row, column);
            }
        }

        public static string? GetString(this DataTable dt, int row, string column)
        {
            return dt.Rows[row].Field<string?>(column);
        }

        public static string? GetString(this DataTable dt, int row, int column)
        {
            return dt.Rows[row].Field<string?>(column);
        }

        public static byte[] GetBinaryArray(this DataTable dt, int row, string column)
        {
            return (byte[])dt.Rows[row][column];
        }

        public static byte[] GetBinaryArray(this DataTable dt, int row, int column)
        {
            return (byte[])dt.Rows[row][column];
        }

        /*
         * ~DataTable
         */

        /*
         * DataRow extensions
         */
        public static string? GetString(this DataRow dr, string column)
        {
            return dr.Field<string?>(column);
        }

        public static string? GetString(this DataRow dr, int column)
        {
            return dr.Field<string?>(column);
        }

        public static int? GetBigInt(this DataRow dr, string column)
        {
            return (int?)dr.Field<long?>(column);
        }

        public static int? GetBigInt(this DataRow dr, int column)
        {
            return (int?)dr.Field<long?>(column);
        }

        public static int? GetInt(this DataRow dr, string column)
        {
            try
            {
                return dr.Field<int?>(column);
            }
            catch
            {
                return dr.GetBigInt(column);
            }
        }

        public static int? GetInt(this DataRow dr, int column)
        {
            try
            {
                return dr.Field<int?>(column);
            }
            catch
            {
                return dr.GetBigInt(column);
            }
        }

        /*
         * ~DataRow
         */

        public static void AddParam(this DbCommand? cmd, string param, object? value)
        {
            if (cmd is MySqlCommand mysqlCmd)
            {
                mysqlCmd.Parameters.AddWithValue(param, value);
                return;
            }

            if (cmd is SqliteCommand sqliteCmd)
            {
                sqliteCmd.Parameters.AddWithValue(param, value);
                return;
            }
            throw new Exception("AddParam is not defined for your Sql Type");
        }

        // not tested
        //public static bool GetBool(this DataTable dt, int row, string column)
        //{
        //    return dt.Rows[row].Field<bool>(column);
        //}

        // not tested
        //public static float GetFloat(this DataTable dt, int row, string column)
        //{
        //    return dt.Rows[row].Field<float>(column);
        //}        
    }
}
