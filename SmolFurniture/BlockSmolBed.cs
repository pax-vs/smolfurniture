using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SmolFurniture
{
    public class BlockSmolBed : Block, IClaimTraverseable
    {
        public override bool TryPlaceBlock(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemStack itemstack,
            BlockSelection blockSel,
            ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing[] orientation = SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation orientedCode = CodeWithVariant("side", orientation[0].Code);
            Block orientedBlock = world.BlockAccessor.GetBlock(orientedCode);
            orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            return true;
        }

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            BlockEntitySmolBed bed = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySmolBed;
            if (bed == null || bed.MountedBy != null)
            {
                return false;
            }

            EntityBehaviorTiredness tiredness = byPlayer.Entity.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (tiredness != null && tiredness.Tiredness <= 8)
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "nottiredenough", Lang.Get("not-tired-enough"));
                }
                else
                {
                    byPlayer.Entity.TryUnmount();
                }

                return false;
            }

            int temporalStormSleeping = api.World.Config.GetString("temporalStormSleeping", "0").ToInt();
            if (temporalStormSleeping == 0 &&
                api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength > 0)
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "cantsleep-tempstorm", Lang.Get("cantsleep-tempstorm"));
                }
                else
                {
                    byPlayer.Entity.TryUnmount();
                }

                return false;
            }

            return byPlayer.Entity.TryMount(bed);
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            BlockEntitySmolBed bed = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySmolBed;
            bed?.MountedBy?.TryUnmount();
            base.OnBlockRemoved(world, pos);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier = 1f)
        {
            return new[] { new ItemStack(world.BlockAccessor.GetBlock(CodeWithVariant("side", "north"))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(CodeWithVariant("side", "north")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(Variant["side"]);
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];
            return CodeWithVariant("side", nowFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(Variant["side"]);
            return facing.Axis == axis ? CodeWithVariant("side", facing.Opposite.Code) : Code;
        }

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder description,
            IWorldAccessor world,
            bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, description, world, withDebugInfo);

            double efficiency = inSlot.Itemstack.Collectible.Attributes["sleepEfficiency"].AsDouble();
            double sleepHours = efficiency * world.Calendar.HoursPerDay / 2;
            description.AppendLine("\n" + Lang.Get("Lets you sleep for {0} hours a day", sleepHours.ToString("#.#")));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float efficiency = Attributes?["sleepEfficiency"].AsFloat(0.5f) ?? 0.5f;
            double sleepHours = Math.Round(efficiency * world.Calendar.HoursPerDay / 2, 2);
            return base.GetPlacedBlockInfo(world, pos, forPlayer) +
                Lang.Get("Lets you sleep for up to {0} hours", sleepHours);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(
            IWorldAccessor world,
            BlockSelection selection,
            IPlayer forPlayer)
        {
            return new[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "blockhelp-bed-sleep",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override void OnEntityCollide(
            IWorldAccessor world,
            Entity entity,
            BlockPos pos,
            BlockFacing facing,
            Vec3d collideSpeed,
            bool isImpact)
        {
            if (isImpact && facing.Axis == EnumAxis.Y)
            {
                if (Sounds?.Break != null && Math.Abs(collideSpeed.Y) > 0.2)
                {
                    world.PlaySoundAt(Sounds.Break, entity);
                }

                entity.Pos.Motion.Y = GameMath.Clamp(-entity.Pos.Motion.Y * 0.8, -0.5, 0.5);
            }
        }
    }
}
