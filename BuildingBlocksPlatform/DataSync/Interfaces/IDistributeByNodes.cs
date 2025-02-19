using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocksPlatform.DataSync.Interfaces
{
    public interface IDistributeByNodes
    {
        /// <summary>
        ///  一级节点同步数据需要分发的节点，以"/"分隔，如ZGGG/ZGHA/ZHCC/ZHHH
        /// </summary>
        public string DistributeNodes { get; set; }
    }
}
