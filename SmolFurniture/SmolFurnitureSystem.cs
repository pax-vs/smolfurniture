using Vintagestory.API.Common;

namespace SmolFurniture
{
    public class SmolFurnitureSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockSmolTable", typeof(BlockSmolTable));
            api.RegisterBlockClass("BlockSmolBed", typeof(BlockSmolBed));
            api.RegisterBlockEntityClass("BlockEntitySmolTable", typeof(BlockEntitySmolTable));
            api.RegisterBlockEntityClass("BlockEntitySmolBed", typeof(BlockEntitySmolBed));
        }
    }
}
