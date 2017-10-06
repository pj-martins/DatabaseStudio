using PaJaMa.DatabaseStudio.Compare.Classes;
using PaJaMa.DatabaseStudio.DatabaseObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaJaMa.DatabaseStudio.Compare.Workspaces
{
	public abstract class WorkspaceBase
	{
        public Database TargetDatabase { get; set; }
		public DatabaseObjectBase TargetObject { get; set; }
		public List<SynchronizationItem> SynchronizationItems { get; private set; }

		private bool _select;
		public bool Select
		{
			get { return _select; }
			set
			{
				if (!SynchronizationItems.Any())
				{
					_select = false;
					return;
				}

				_select = value;
			}
		}

		public WorkspaceBase(Database targetDatabase, DatabaseObjectBase targetObject)
		{
            TargetDatabase = targetDatabase;
			TargetObject = targetObject;
			SynchronizationItems = new List<SynchronizationItem>();
		}
	}

	public abstract class WorkspaceWithSourceBase : WorkspaceBase
	{
		public virtual DatabaseObjectBase SourceObject { get; set; }
		public WorkspaceWithSourceBase(DatabaseObjectBase sourceObject, Database targetDatabase, DatabaseObjectBase targetObject)
			: base(targetDatabase, targetObject)
		{
			SourceObject = sourceObject;
			populateDifferences();
		}

		private void populateDifferences()
		{
			var syncItem = DatabaseObjectSynchronizationBase.GetSynchronization(TargetDatabase, SourceObject);
			SynchronizationItems.AddRange(syncItem.GetSynchronizationItems(TargetObject));

			if (SourceObject is DatabaseObjectWithExtendedProperties)
				SynchronizationItems.AddRange(ExtendedPropertySynchronization.GetExtendedProperties(SourceObject as DatabaseObjectWithExtendedProperties, 
					TargetObject as DatabaseObjectWithExtendedProperties));
		}


	}
}
