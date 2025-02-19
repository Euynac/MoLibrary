using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocksPlatform.DataSync.Interfaces
{
    public interface IDataSyncFunctions
    {
        public bool IsSelfHostOperation();

        public string GetLocalAddress();
    }
}
