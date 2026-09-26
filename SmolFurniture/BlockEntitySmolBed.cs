using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SmolFurniture
{
    public class BlockEntitySmolBed : BlockEntityBed, IMountableSeat, IMountable
    {
        private readonly EntityPos centeredMountPosition = new EntityPos();
        private float mountHeight = 0.3125f;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Cuboidf[] collisionBoxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
            if (collisionBoxes != null && collisionBoxes.Length > 0)
            {
                mountHeight = collisionBoxes[0].Y2;
            }
        }

        private EntityPos CenteredMountPosition
        {
            get
            {
                BlockFacing facing = BlockFacing.FromCode(Block.LastCodePart()) ?? BlockFacing.NORTH;
                centeredMountPosition.SetPos(Pos);
                centeredMountPosition.Yaw = facing.HorizontalAngleIndex * GameMath.PIHALF + GameMath.PIHALF;
                return centeredMountPosition.Add(0.5, mountHeight, 0.5);
            }
        }

        EntityPos IMountableSeat.SeatPosition => CenteredMountPosition;

        EntityPos IMountable.Position => CenteredMountPosition;
    }
}
