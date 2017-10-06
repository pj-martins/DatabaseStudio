using PaJaMa.DatabaseStudio.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.DatabaseObjects
{
	public class Database : IDatabase
	{
		public string DatabaseName { get; private set; }
		public string DataSource { get; private set; }
		public List<Schema> Schemas { get; private set; }
		public List<ServerLogin> ServerLogins { get; private set; }
		public List<DatabasePrincipal> Principals { get; private set; }
		public List<Permission> Permissions { get; private set; }
		public List<Credential> Credentials { get; private set; }
		public string ConnectionString { get; private set; }
		public bool Is2000OrLess { get; private set; }
        public Type DriverType { get; private set; }

		public Database(Type driverType, string connectionString)
		{
			Schemas = new List<Schema>();
			ServerLogins = new List<ServerLogin>();
			Principals = new List<DatabasePrincipal>();
			Permissions = new List<Permission>();
			Credentials = new List<Credential>();

            using (var conn = Activator.CreateInstance(driverType) as DbConnection)
			{
                conn.ConnectionString = connectionString;
				conn.Open();
				var parts = conn.ServerVersion.Split('.');
				if (Convert.ToInt16(parts[0]) <= 8)
					Is2000OrLess = true;
				DatabaseName = conn.Database;
				DataSource = conn.DataSource;
				conn.Close();
			}

            DriverType = driverType;
			ConnectionString = connectionString;
		}

		public void PopulateChildren(bool condensed, BackgroundWorker worker)
		{
			using (var conn = Activator.CreateInstance(DriverType) as DbConnection)
			{
                conn.ConnectionString = ConnectionString;
				conn.Open();
				var extendedProperties = ExtendedProperty.GetExtendedProperties(conn, Is2000OrLess);
				Schema.PopulateSchemas(this, conn, extendedProperties);
				Table.PopulateTables(this, conn, extendedProperties, worker);
				DatabaseObjectBase.PopulateObjects(this, conn, extendedProperties, condensed, worker);
				conn.Close();
			}
		}

		public List<DatabaseObjectBase> GetDatabaseObjects(bool filter)
		{
			var lst = (from s in Schemas
					   from rs in s.RoutinesSynonyms
					   select rs).OfType<DatabaseObjectBase>().ToList();

			lst.AddRange((from s in Schemas
						  from v in s.Views
						  select v).OfType<DatabaseObjectBase>());

			lst.AddRange(Principals.Where(p => !filter || !"|INFORMATION_SCHEMA|sys|guest|public|".Contains("|" + p.PrincipalName + "|")).ToList());
			lst.AddRange(Schemas.Where(s => !filter || !"|INFORMATION_SCHEMA|dbo|".Contains("|" + s.SchemaName + "|")).ToList());
			//lst.AddRange(Principals.Where(p => !"|INFORMATION_SCHEMA|sys|guest|public|dbo|".Contains("|" + p.PrincipalName + "|")).ToList());
			//lst.AddRange(Schemas.Where(s => !"|INFORMATION_SCHEMA|dbo|".Contains("|" + s.SchemaName + "|")).ToList());
			lst.AddRange(ServerLogins.Where(l => !filter || l.LoginType != LoginType.WindowsLogin).ToList());
			lst.AddRange(Permissions.ToList());
			lst.AddRange(Credentials.ToList());
			return lst;
		}

		public void ChangeDatabase(string newDatabase)
		{
            if (DriverType == typeof(System.Data.SqlClient.SqlConnection))
            {
                var connBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString);
			connBuilder.InitialCatalog = newDatabase;
			ConnectionString = connBuilder.ConnectionString;
            }
            else
                throw new NotImplementedException();
		}

		//public void Disconnect()
		//{
		//	if (Connection != null)
		//	{
		//		if (Connection.State == System.Data.ConnectionState.Open)
		//			Connection.Close();
		//		Connection.Dispose();
		//		Connection = null;
		//	}
		//}

		public override string ToString()
		{
			return DatabaseName;
		}
	}
}
