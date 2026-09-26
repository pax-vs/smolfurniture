using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace SmolFurniture
{
    public class SmolFurnitureSystem : ModSystem
    {
        public const string ModDomain = "smolfurniture";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockSmolTable", typeof(BlockSmolTable));
            api.RegisterBlockClass("BlockSmolBed", typeof(BlockSmolBed));
            api.RegisterBlockEntityClass("BlockEntitySmolTable", typeof(BlockEntitySmolTable));
            api.RegisterBlockEntityClass("BlockEntitySmolBed", typeof(BlockEntitySmolBed));
            api.RegisterBlockBehaviorClass(
                "SmolFurniture.DoorAttributeRendering",
                typeof(BlockBehaviorSmolDoorAttributeRendering));
            api.RegisterBlockEntityBehaviorClass(
                "SmolFurniture.DoorAttributeRendering",
                typeof(BlockEntityBehaviorSmolDoorAttributeRendering));
            api.RegisterBlockEntityBehaviorClass("SmolFurniture.Door", typeof(BEBehaviorSmolDoor));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            SmolFurnitureHandbook.Register(api);
        }
    }
}
