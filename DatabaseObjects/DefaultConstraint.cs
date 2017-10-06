using PaJaMa.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.DatabaseObjects
{
	public class DefaultConstraint : DatabaseObjectBase
	{
		public override string ObjectName
		{
			get { return ConstraintName; }
		}

		public Table Table { get; set; }
		public string ConstraintName { get; set; }
		public Column Column { get; set; }
		public string ColumnDefault { get; set; }

        private Database _database;
        public override Database ParentDatabase
        {
            get { return _database; }
        }

        public static void PopulateConstraints(Database database, DbConnection connection)
		{
            if (database.IsSQLite) return;

            string qry = string.Empty;
            if (connection is SqlConnection)
            {
                qry = database.Is2000OrLess ? @"select t.name as TableName, d.name as ConstraintName, c.name as ColumnName, co.COLUMN_DEFAULT as ColumnDefault, TABLE_SCHEMA as SchemaName 
from sysconstraints dc
join sysobjects d on d.id = dc.constid
join syscolumns c on c.colid = dc.colid
join sysobjects t on t.id = d.parent_obj
	and t.id = c.id
join INFORMATION_SCHEMA.COLUMNS co on co.COLUMN_NAME = c.name and co.TABLE_NAME = t.name
where d.xtype = 'D'"
                    :
                    @"SELECT t.name as TableName, co.name as ConstraintName, c.Name as ColumnName, co.definition as ColumnDefault, s.name as SchemaName
FROM sys.all_columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.default_constraints co ON c.default_object_id = co.object_id";
            }
            else if (database.IsPostgreSQL)
            {
                qry = @"select 
	t.relname as TableName,
	c.conname as ConstraintName,
	a.attname as ColumnName,
	d.adsrc as ColumnDefault,
	n.nspname as SchemaName
from pg_constraint c
join pg_class t on t.oid = c.conrelid
join pg_attribute a on a.attrelid = c.conrelid and ARRAY[attnum] <@ c.conkey
join pg_attrdef d on d.adnum = a.attnum and d.adrelid = t.oid
join pg_catalog.pg_namespace n on n.oid = t.relnamespace
";
            }
            else
                throw new NotImplementedException();

			using (var fromCmd = connection.CreateCommand())
			{
				fromCmd.CommandText = qry;

				using (var rdr = fromCmd.ExecuteReader())
				{
					if (rdr.HasRows)
					{
						while (rdr.Read())
						{
							var constraint = rdr.ToObject<DefaultConstraint>();
                            constraint._database = database;
							var schema = database.Schemas.First(s => s.SchemaName == rdr["SchemaName"].ToString());
							constraint.Table = schema.Tables.First(t => t.TableName == rdr["TableName"].ToString());
							constraint.Column = constraint.Table.Columns.First(c => c.ObjectName == rdr["ColumnName"].ToString());
							constraint.Table.DefaultConstraints.Add(constraint);
						}
						rdr.Close();
					}
				}
			}


		}
	}
}
