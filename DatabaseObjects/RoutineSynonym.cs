using PaJaMa.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.DatabaseObjects
{
	public class RoutineSynonym : DatabaseObjectWithExtendedProperties
	{
		public override string ObjectName
		{
			get { return Name; }
		}

		public Schema Schema { get; set; }
		public string Name { get; set; }
		public RoutineSynonymType Type { get; set; }

		public override string ObjectType
		{
			get { return Type.ToString(); }
		}

        public override Database ParentDatabase => Schema.ParentDatabase;

        public override string ToString()
		{
			return Schema.SchemaName + "." + Name;
		}

		public static void PopulateRoutinesSynonyms(Database database, DbConnection connection, List<ExtendedProperty> extendedProperties)
		{
            // TODO:
            if (database.IsSQLite) return;

            string qry = string.Empty;
            if (connection is SqlConnection)
            {
                qry = database.Is2000OrLess ? @"select ROUTINE_SCHEMA as ObjectSchema, ROUTINE_NAME as Name, ROUTINE_TYPE as Type, Definition = ROUTINE_DEFINITION
					from INFORMATION_SCHEMA.ROUTINES
				"

                    :
                    @"select ROUTINE_SCHEMA as ObjectSchema, ROUTINE_NAME as Name, ROUTINE_TYPE as Type, Definition = OBJECT_DEFINITION(OBJECT_ID(ROUTINE_SCHEMA + '.' + ROUTINE_NAME)) 
					from INFORMATION_SCHEMA.ROUTINES
				union all
				select s.name, sy.name, 'SYNONYM', 'CREATE SYNONYM [' + s.name + '].[' + sy.name + '] FOR ' + replace(base_object_name, '[' + db_name(parent_object_id) + '].', '') from sys.synonyms sy
				join sys.schemas s on s.schema_id = sy.schema_id
";
            }
            else if (database.IsPostgreSQL)
            {
                qry = @"
select 
	ROUTINE_SCHEMA as ObjectSchema, 
	ROUTINE_NAME as Name, 
	ROUTINE_TYPE as Type,
	ROUTINE_DEFINITION as Definition
from INFORMATION_SCHEMA.ROUTINES
where ROUTINE_SCHEMA <> 'pg_catalog' and ROUTINE_SCHEMA <> 'information_schema'
";
            }
            else
                throw new NotImplementedException();

			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandText = qry;
				using (var rdr = cmd.ExecuteReader())
				{
					if (rdr.HasRows)
					{
						while (rdr.Read())
						{
							var rvs = rdr.ToObject<RoutineSynonym>();
							var schema = database.Schemas.First(s => s.SchemaName == rdr["ObjectSchema"].ToString());
							rvs.Definition = string.IsNullOrEmpty(rvs.Definition) ? string.Empty : rvs.Definition.Trim();
							rvs.Schema = schema;
							rvs.ExtendedProperties = extendedProperties.Where(ep => ep.Level1Object == rvs.ObjectName && ep.ObjectSchema == rvs.Schema.SchemaName
								&& ep.Level1Type.ToLower() == rvs.ObjectType.ToLower()).ToList();
							schema.RoutinesSynonyms.Add(rvs);
						}
					}
				}
			}
		}

		public enum RoutineSynonymType
		{
			Procedure,
			Function,
			Synonym
		}
	}
}
