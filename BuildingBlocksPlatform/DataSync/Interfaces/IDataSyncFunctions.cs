using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocksPlatform.DataSync.Interfaces
{
    public interface IDataSyncFunctions
    {
        public void SetAsSelfHost();
        public bool IsSelfHostOperation();

        public bool IsUploading();

        public string GetSelfHostHeaderKey();

        public string GetLocalAddress();
    }
}
