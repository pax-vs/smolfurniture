using Vintagestory.API.Common;

namespace SmolFurniture
{
    public class BlockSmolTable : Block
    {
        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            BlockEntitySmolTable table = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySmolTable;
            return table?.OnInteract(byPlayer) == true || base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
