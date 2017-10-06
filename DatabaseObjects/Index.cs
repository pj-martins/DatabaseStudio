using PaJaMa.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.DatabaseObjects
{
    public class Index : DatabaseObjectBase
    {
        public Table Table { get; set; }
        public string IndexName { get; set; }
        public string IndexType { get; set; }
        public bool IsUnique { get; set; }

        public bool HasBeenDropped { get; set; }

        public List<IndexColumn> IndexColumns { get; set; }

        public override string ObjectName
        {
            get { return IndexName; }
        }

        private Database _parentDatabase;
        public override Database ParentDatabase => _parentDatabase;

        public static void PopulateIndexes(Database database, DbConnection connection)
        {
            var indexes = new List<Index>();

            if (database.IsSQLite)
            {
                using (var cmd = connection.CreateCommand())
                {
                    foreach (var tbl in database.Schemas.First().Tables)
                    {
                        cmd.CommandText = $"pragma index_list({tbl.TableName})";
                        DataTable dt = new DataTable();
                        using (var rdr = cmd.ExecuteReader())
                        {
                            dt.Load(rdr);
                        }

                        foreach (DataRow dr in dt.Rows)
                        {
                            var index = indexes.FirstOrDefault(i => i.IndexName == dr["name"].ToString()
                                && i.Table.TableName == tbl.TableName);

                            if (index == null)
                            {
                                index = new Index()
                                {
                                    IndexName = dr["name"].ToString(),
                                    // TODO
                                    IndexType = "NONCLUSTERED",
                                    IsUnique = dr["unique"].ToString() == "1",
                                    Table = tbl,
                                    _parentDatabase = database
                                };
                                index.IndexColumns = new List<IndexColumn>();
                                index.Table.Indexes.Add(index);
                                indexes.Add(index);
                            }

                            cmd.CommandText = $"pragma index_info({index.IndexName})";
                            using (var rdr = cmd.ExecuteReader())
                            {
                                if (rdr.HasRows)
                                {
                                    while (rdr.Read())
                                    {
                                        index.IndexColumns.Add(new IndexColumn()
                                        {
                                            ColumnName = rdr["name"].ToString(),
                                            Ordinal = Convert.ToInt16(rdr["seqno"])
                                        });
                                    }
                                }
                            }
                        }

                        foreach (var index in indexes)
                        {
                            cmd.CommandText = $"select * from sqlite_master where type = 'index' and name = '{index.IndexName}' and tbl_name = '{index.Table.TableName}'";
                            using (var rdr = cmd.ExecuteReader())
                            {
                                if (rdr.HasRows)
                                {
                                    while (rdr.Read())
                                    {
                                        index.Definition = rdr["sql"].ToString();
                                    }
                                }
                            }
                        }
                    }
                }

                return;
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = getIndexSchema(database, database.Is2000OrLess);

                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.HasRows)
                    {
                        while (rdr.Read())
                        {
                            string indexName = rdr["IndexName"].ToString();
                            string tableName = rdr["TableName"].ToString();
                            var schema = database.Schemas.First(s => s.SchemaName == rdr["SchemaName"].ToString());
                            var index = indexes.FirstOrDefault(i => i.IndexName == indexName && i.Table.TableName == tableName && i.Table.Schema.SchemaName
                                == schema.SchemaName);
                            if (index == null)
                            {
                                index = rdr.ToObject<Index>();
                                index.IndexColumns = new List<IndexColumn>();

                                // stale index?
                                index.Table = schema.Tables.FirstOrDefault(t => t.TableName == tableName);
                                if (index.Table == null) continue;

                                index.Table.Indexes.Add(index);
                                indexes.Add(index);
                            }
                            var indexCol = rdr.ToObject<IndexColumn>();
                            index.IndexColumns.Add(indexCol);
                        }
                        rdr.Close();
                    }
                }
            }
        }

        private static string getIndexSchema(Database database, bool is200OrLess)
        {
            if (database.ConnectionType == typeof(SqlConnection))
            {
                return is200OrLess ?
                    @"select 
	t.name as TableName,
	i.name as IndexName, 
	c.name as ColumnName, 
	IndexType = case when i.status & 16 <> 0 then 'CLUSTERED' else 'NONCLUSTERED' end,
	KeyNo as Ordinal,
	IsUnique = convert(bit, case when i.status & 2 <> 0 then 1 else 0 end),
	Descending = convert(bit,isnull(INDEXKEY_PROPERTY(i.id,
                                   i.indid,
                                   keyno,
                                   'IsDescending'), 0)),
	IsPrimaryKey = convert(bit, case when i.status & 2048 <> 0 then 1 else 0 end),
	SchemaName = 'dbo'
from sysindexkeys ik
join syscolumns c on c.id = ik.id
	and ik.colid = c.colid
join sysindexes i on i.indid = ik.indid
	and i.id = ik.id
join sysobjects t on t.id = c.id
where i.name not like '_WA_Sys%' and t.xtype = 'U'"
                    :
                    @"SELECT 
	 TableName = t.name,
     IndexName = ind.name,
     ColumnName = col.name,
	 IndexType = ind.type_desc,
	 Ordinal = ic.key_ordinal,
	 IsUnique = is_unique,
	 Descending = is_descending_key,
	 SchemaName = sc.name
FROM 
     sys.indexes ind 
INNER JOIN 
     sys.index_columns ic ON  ind.object_id = ic.object_id and ind.index_id = ic.index_id 
INNER JOIN 
     sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id 
INNER JOIN 
     sys.tables t ON ind.object_id = t.object_id 
join sys.schemas sc on sc.schema_id = t.schema_id
WHERE 
     t.is_ms_shipped = 0 and is_unique_constraint = 0 and is_primary_key = 0";
            }
            else if (database.IsPostgreSQL)
            {
                return @"
select 
	t.relname as TableName,
	i.relname as IndexName,
	a.attname as ColumnName,
	case when ix.indisclustered = true then 'CLUSTERED' else 'NONCLUSTERED' end as IndexType,
	ix.indisunique as IsUnique,
	n.nspname as SchemaName,
	-- TODO:
	1 as Ordinal,
	false as Descending
from pg_index ix
join pg_class t on t.oid = ix.indrelid
join pg_class i on i.oid = ix.indexrelid
join pg_attribute a on a.attrelid = t.oid and a.attnum = ANY(ix.indkey)
join pg_catalog.pg_namespace n on n.oid = t.relnamespace
where t.relkind = 'r' and t.relname not like 'pg_%' and indisprimary = false";
            }
            else
                throw new NotImplementedException();
        }
    }

    public class IndexColumn
    {
        public string ColumnName { get; set; }
        public bool Descending { get; set; }
        public int Ordinal { get; set; }
    }
}
