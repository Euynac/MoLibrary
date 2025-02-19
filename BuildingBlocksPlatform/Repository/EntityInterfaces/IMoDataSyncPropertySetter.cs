using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocksPlatform.Repository.EntityInterfaces
{
    public interface IMoDataSyncPropertySetter
    {
        void SetDataSyncProperties(object targetObject);
    }
}
