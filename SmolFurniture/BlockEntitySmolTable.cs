using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SmolFurniture
{
    public class BlockEntitySmolTable : BlockEntityContainer
    {
        private readonly InventoryGeneric inventory;
        private MealMeshCache mealMeshCache;
        private MeshData pieMesh;

        public BlockEntitySmolTable()
        {
            inventory = new InventoryGeneric(1, null, null);
        }

        public override InventoryBase Inventory => inventory;

        public override string InventoryClassName => "smolfurniture-smoltable";

        private ItemStack PieStack => inventory[0].Itemstack;

        private BlockPie PieBlock => PieStack?.Block as BlockPie;

        private float PieSurfaceHeight => Block?.Attributes?["pieSurfaceHeight"].AsFloat(0.65625f) ?? 0.65625f;

        private bool HasAnyFilling
        {
            get
            {
                ItemStack[] contents = PieBlock?.GetContents(Api.World, PieStack);
                return contents != null &&
                    (contents[1] != null || contents[2] != null || contents[3] != null || contents[4] != null);
            }
        }

        private bool HasAllFilling
        {
            get
            {
                ItemStack[] contents = PieBlock?.GetContents(Api.World, PieStack);
                return contents != null &&
                    contents[1] != null && contents[2] != null && contents[3] != null && contents[4] != null;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            mealMeshCache = api.ModLoader.GetModSystem<MealMeshCache>();
            RebuildPieMesh();
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);

            if (Api.Side != EnumAppSide.Server || PieStack?.Collectible.Code.Path != "rot")
            {
                return;
            }

            ItemStack rottenStack = PieStack;
            inventory[0].Itemstack = null;
            Api.World.SpawnItemEntity(rottenStack, Pos.ToVec3d().Add(0.5, PieSurfaceHeight, 0.5));
            MarkDirty(true);
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (PieBlock == null)
            {
                return TryPlaceRawPie(hotbarSlot) || TryStartPie(hotbarSlot, byPlayer);
            }

            EnumTool? tool = hotbarSlot?.Itemstack?.Collectible.GetTool(hotbarSlot);
            if (tool == EnumTool.Knife || tool == EnumTool.Sword)
            {
                return CycleTopCrust();
            }

            if (hotbarSlot?.Empty == false && PieBlock.State == "raw")
            {
                bool added = TryAddIngredientFrom(hotbarSlot, byPlayer);
                if (added)
                {
                    PieStack.Attributes.SetBool("bakeable", HasAllFilling);
                    SyncPie();
                }

                return added;
            }

            if (hotbarSlot?.Empty != false)
            {
                PickUpPie(byPlayer);
                return true;
            }

            return false;
        }

        private bool TryPlaceRawPie(ItemSlot slot)
        {
            BlockPie heldPieBlock = slot?.Itemstack?.Block as BlockPie;
            if (heldPieBlock?.State != "raw")
            {
                return false;
            }

            inventory[0].Itemstack = slot.TakeOut(1);
            slot.MarkDirty();
            SyncPie();
            return true;
        }

        private bool TryStartPie(ItemSlot slot, IPlayer byPlayer)
        {
            InPieProperties pieProperties = slot?.Itemstack?.ItemAttributes?["inPieProperties"]
                .AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain);

            if (pieProperties?.PartType != EnumPiePartType.Crust)
            {
                return false;
            }

            if (slot.StackSize < 2)
            {
                TriggerError(byPlayer, "notpieable", Lang.Get("Need at least 2 dough"));
                return true;
            }

            BlockPie rawPieBlock = Api.World.GetBlock(new AssetLocation("game:pie-raw")) as BlockPie;
            if (rawPieBlock == null)
            {
                Api.Logger.Error("[Smol Furniture] Could not resolve game:pie-raw while forming a pie at {0}.", Pos);
                return true;
            }

            ItemStack doughStack = slot.TakeOut(2);
            ItemStack pieStack = new ItemStack(rawPieBlock);
            rawPieBlock.SetContents(pieStack, new[] { doughStack, null, null, null, null, null });
            pieStack.Attributes.SetInt("pieSize", 4);
            pieStack.Attributes.SetBool("bakeable", false);
            inventory[0].Itemstack = pieStack;

            SyncPie();
            return true;
        }

        private bool CycleTopCrust()
        {
            if (PieBlock.State != "raw")
            {
                return false;
            }

            ItemStack[] contents = PieBlock.GetContents(Api.World, PieStack);
            if (!HasAnyFilling || contents[5] == null)
            {
                return true;
            }

            inventory[0].Itemstack = BlockPie.CycleTopCrustType(PieStack);
            SyncPie();
            return true;
        }

        private void PickUpPie(IPlayer byPlayer)
        {
            if (Api.Side != EnumAppSide.Server)
            {
                return;
            }

            ItemStack pickedUpStack = PieStack;
            inventory[0].Itemstack = null;

            if (!byPlayer.InventoryManager.TryGiveItemstack(pickedUpStack))
            {
                Api.World.SpawnItemEntity(
                    pickedUpStack,
                    Pos.ToVec3d().Add(0.5, PieSurfaceHeight + 0.125, 0.5));
            }

            Api.World.Logger.Audit(
                "{0} took 1x{1} from a Smol table at {2}.",
                byPlayer.PlayerName,
                pickedUpStack.Collectible.Code,
                Pos);

            SyncPie();
        }

        private bool TryAddIngredientFrom(ItemSlot slot, IPlayer byPlayer)
        {
            InPieProperties pieProperties = slot.Itemstack?.ItemAttributes?["inPieProperties"]
                .AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain);

            if (pieProperties == null)
            {
                TriggerError(byPlayer, "notpieable", Lang.Get("This item can not be added to pies"));
                return false;
            }

            if (slot.StackSize < 2)
            {
                TriggerError(byPlayer, "notpieable", Lang.Get("Need at least 2 items each"));
                return false;
            }

            BlockPie pieBlock = PieBlock;
            if (pieBlock == null)
            {
                return false;
            }

            ItemStack[] contents = pieBlock.GetContents(Api.World, PieStack);
            bool isFull = contents[1] != null && contents[2] != null && contents[3] != null && contents[4] != null;
            bool hasFilling = contents[1] != null || contents[2] != null || contents[3] != null || contents[4] != null;

            if (isFull)
            {
                if (pieProperties.PartType == EnumPiePartType.Crust)
                {
                    if (contents[5] == null)
                    {
                        contents[5] = slot.TakeOut(2);
                        pieBlock.SetContents(PieStack, contents);
                        PieStack.Attributes.SetString("topCrustType", "full");
                    }
                    else
                    {
                        inventory[0].Itemstack = BlockPie.CycleTopCrustType(PieStack);
                    }

                    return true;
                }

                TriggerError(byPlayer, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                return false;
            }

            if (pieProperties.PartType != EnumPiePartType.Filling)
            {
                TriggerError(byPlayer, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                return false;
            }

            if (!hasFilling)
            {
                contents[1] = slot.TakeOut(2);
                pieBlock.SetContents(PieStack, contents);
                return true;
            }

            EnumFoodCategory[] foodCategories = contents.Select(BlockPie.FillingFoodCategory).ToArray();
            InPieProperties[] stackProperties = contents
                .Select(stack => stack?.ItemAttributes?["inPieProperties"]
                    .AsObject<InPieProperties>(null, stack.Collectible.Code.Domain))
                .ToArray();

            ItemStack comparedStack = slot.Itemstack;
            EnumFoodCategory foodCategory = BlockPie.FillingFoodCategory(comparedStack);
            bool equal = true;
            bool foodCategoryEquals = true;
            bool allowMixing = true;
            IEnumerable<string> mixingCodes = pieProperties.MixingCodes;

            for (int i = 1; (equal || foodCategoryEquals || mixingCodes.Any()) && i < contents.Length - 1; i++)
            {
                if (comparedStack == null)
                {
                    continue;
                }

                equal &= contents[i] == null ||
                    comparedStack.Equals(Api.World, contents[i], GlobalConstants.IgnoredStackAttributes);
                foodCategoryEquals &= contents[i] == null || foodCategories[i] == foodCategory;
                allowMixing &= stackProperties[i]?.AllowMixing != false;
                mixingCodes = stackProperties[i]?.MixingCodes.Intersect(mixingCodes) ?? mixingCodes;

                comparedStack = contents[i];
                foodCategory = foodCategories[i];
            }

            int emptySlotIndex = 2 + (contents[2] != null ? 1 + (contents[3] != null ? 1 : 0) : 0);

            if (equal)
            {
                contents[emptySlotIndex] = slot.TakeOut(2);
                pieBlock.SetContents(PieStack, contents);
                return true;
            }

            if (!foodCategoryEquals && !mixingCodes.Any())
            {
                TriggerError(byPlayer, "piefullfilling", Lang.Get("piemaking-unabletomixingredient"));
                return false;
            }

            if (!allowMixing)
            {
                TriggerError(byPlayer, "piefullfilling", Lang.Get("piemaking-mixingnotallowed"));
                return false;
            }

            contents[emptySlotIndex] = slot.TakeOut(2);
            pieBlock.SetContents(PieStack, contents);
            return true;
        }

        private void TriggerError(IPlayer byPlayer, string code, string message)
        {
            (Api as ICoreClientAPI)?.TriggerIngameError(this, code, message);
        }

        private void SyncPie()
        {
            RebuildPieMesh();
            inventory[0].MarkDirty();
            MarkDirty(true);
        }

        private void RebuildPieMesh()
        {
            pieMesh = null;

            if (Api == null || Api.Side == EnumAppSide.Server || PieStack == null || mealMeshCache == null)
            {
                return;
            }

            MeshData sourceMesh = mealMeshCache.GetPieMesh(PieStack);
            pieMesh = sourceMesh?.Clone().Translate(0, PieSurfaceHeight, 0);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (pieMesh != null)
            {
                mesher.AddMeshData(pieMesh);
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder description)
        {
            if (PieStack == null)
            {
                return;
            }

            if (MealMeshCache.ContentsRotten(inventory))
            {
                description.Append(Lang.Get("Rotten"));
                return;
            }

            description.Append(BlockEntityShelf.PerishableInfoCompact(Api, inventory[0], 0, true));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                RebuildPieMesh();
                MarkDirty(true);
            }
        }
    }
}
