using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.Classes
{
    public class DriverHelper
    {
        private DbConnection _connection;

        public DriverHelper(DbConnection connection)
        {
            _connection = connection;
        }

        public static Type[] GetDatabaseTypes()
        {
            List<Type> types = new List<Type>() {
                typeof(System.Data.SqlClient.SqlConnection),
                typeof(System.Data.OleDb.OleDbConnection),
                typeof(System.Data.Odbc.OdbcConnection)
            };

            var partial = ".DataSources.";
            var dataResources = typeof(DriverHelper).Assembly.GetManifestResourceNames().Where(n => n.Contains(partial));
            foreach (var dr in dataResources)
            {
                var fileName = dr.Substring(dr.IndexOf(partial) + partial.Length);
                if (!File.Exists(fileName))
                {
                    var stream = typeof(DriverHelper).Assembly.GetManifestResourceStream(dr);
                    var bytes = Common.Common.StreamToBytes(stream, stream.Length);
                    File.WriteAllBytes(fileName, bytes);
                }
                FileInfo finf = new FileInfo(fileName);
                var asm = Assembly.LoadFile(finf.FullName);
                foreach (var t in asm.GetTypes().Where(t => t.GetInterface(typeof(System.Data.IDbConnection).Name) != null))
                {
                    types.Add(t);
                }
            }
            return types.ToArray();
        }

        public string GetTable(string tableName, string databaseName = null, string schema = null)
        {
            if (_connection is SqlConnection)
            {
                return string.Format("[{0}].[{1}].[{2}]", databaseName ?? _connection.Database, schema ?? "dbo", tableName);
            }
            else if (_connection.GetType().Name.ToLower().Contains("sqlite"))
            {
                return string.Format("[{0}]", tableName);
            }
            else if (_connection.GetType().Name.ToLower().Contains("npgsql"))
            {
                return string.Format("\"{0}\".\"{1}\".\"{2}\"", databaseName ?? _connection.Database, schema ?? "public", tableName);
            }
            return tableName;
        }

        public string GetPreTopN(int topN)
        {
            var top = _connection is SqlConnection || _connection is OdbcConnection || _connection is OleDbConnection;
            if (top)
            {
                return "top " + topN.ToString();
            }
            return string.Empty;
        }

        public string GetPostTopN(int topN)
        {
            var top = _connection is SqlConnection || _connection is OdbcConnection || _connection is OleDbConnection;
            if (!top)
            {
                return "limit " + topN.ToString();
            }
            return string.Empty;
        }

        public string GetColumnList(string[] columns)
        {
            if (_connection is SqlConnection)
            {
                return "[" + string.Join("],\r\n\t[", columns) + "]";
            }
            else if (_connection.GetType().Name.ToLower().Contains("sqlite"))
            {
                return string.Join(",\r\n\t", columns);
            }
            else if (_connection.GetType().Name.ToLower().Contains("npgsql"))
            {
                return "\"" + string.Join("\",\r\n\t\"", columns) + "\"";
            }
            return "*";
        }

        public List<string> GetDatabases()
        {
            if (_connection.GetType().Name.ToLower() == "sqlite")
            {
                throw new NotImplementedException();
            }

            return _connection.GetSchema("Databases").Rows.OfType<DataRow>().Select(dr => dr["database_name"].ToString()).ToList();
        }
    }
}
