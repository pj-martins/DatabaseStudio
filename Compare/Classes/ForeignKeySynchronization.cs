using PaJaMa.Common;
using PaJaMa.DatabaseStudio.DatabaseObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.Compare.Classes
{
    public class ForeignKeySynchronization : DatabaseObjectSynchronizationBase<ForeignKey>
    {
        public ForeignKeySynchronization(Database targetDatabase, ForeignKey foreignKey)
            : base(targetDatabase, foreignKey)
        {
        }

        public override List<SynchronizationItem> GetCreateItems()
        {
            var createString = string.Format(@"
ALTER TABLE {0} {8}{7}{9} ADD CONSTRAINT {1} FOREIGN KEY({2})
REFERENCES {3} ({4})
ON DELETE {5}
ON UPDATE {6}
",
    databaseObject.ChildTable.QueryNameWithSchema,
    databaseObject.QueryObjectName,
    string.Join(",", databaseObject.Columns.Select(c => c.ChildColumn.QueryObjectName).ToArray()),
    databaseObject.ParentTable.QueryNameWithSchema,
    string.Join(",", databaseObject.Columns.Select(c => c.ParentColumn.QueryObjectName).ToArray()),
    databaseObject.DeleteRule,
    databaseObject.UpdateRule,
    databaseObject.ParentDatabase.IsSQLServer ? databaseObject.WithCheck : string.Empty,
    databaseObject.ParentDatabase.IsSQLServer ? " WITH" : string.Empty,
    databaseObject.ParentDatabase.IsSQLServer ? "CHECK" : string.Empty
    );
            if (databaseObject.ParentDatabase.IsSQLServer)
                createString += string.Format(@"
ALTER TABLE {0}
CHECK CONSTRAINT {1}
", databaseObject.ChildTable.QueryNameWithSchema,
    databaseObject.QueryObjectName);
            return getStandardItems(createString, 7);
        }

        public override List<SynchronizationItem> GetDropItems()
        {
            return getStandardDropItems(string.Format(@"
ALTER TABLE {0} DROP CONSTRAINT {1}
", databaseObject.ChildTable.QueryNameWithSchema, databaseObject.QueryObjectName));
        }

        public override List<SynchronizationItem> GetAlterItems(DatabaseObjectBase target)
        {
            var diffs = GetPropertyDifferences(target);
            var diff = getColumnDifference(target);
            if (diff != null)
                diffs.Add(diff);

            if (diffs.Any())
            {
                var syncItem = new SynchronizationItem(databaseObject);
                syncItem.Differences.AddRange(diffs);
                syncItem.AddScript(0, GetRawDropText());
                syncItem.AddScript(1, GetRawCreateText());
                return new List<SynchronizationItem>() { syncItem };
            }

            return new List<SynchronizationItem>();
        }

        private Difference getColumnDifference(DatabaseObjectBase target)
        {
            var targetFk = target as ForeignKey;
            if (targetFk.Columns.Any(f => !databaseObject.Columns.Any(c => c.ParentColumn.ColumnName == f.ParentColumn.ColumnName
                && c.ChildColumn.ColumnName == f.ChildColumn.ColumnName))
                || databaseObject.Columns.Any(f => !targetFk.Columns.Any(c => c.ParentColumn.ColumnName == f.ParentColumn.ColumnName
                && c.ChildColumn.ColumnName == f.ChildColumn.ColumnName)))
                return new Difference()
                {
                    PropertyName = "Columns",
                    SourceValue = string.Join("\r\n", databaseObject.Columns.Select(c => c.ParentColumn.ColumnName + " - " + c.ChildColumn.ColumnName).ToArray()),
                    TargetValue = string.Join("\r\n", targetFk.Columns.Select(c => c.ParentColumn.ColumnName + " - " + c.ChildColumn.ColumnName).ToArray()),
                };
            return null;
        }
    }
}
