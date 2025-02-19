using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuildingBlocksPlatform.Repository.DtoInterfaces;


namespace BuildingBlocksPlatform.SeedWork
{
    public abstract class OurSimpleEntityDto<TPrimaryKey>: MoEntityDto<TPrimaryKey>, IHasEntityId<TPrimaryKey>
    {
        public new string Id { get; set; } = null!;
    }

}
