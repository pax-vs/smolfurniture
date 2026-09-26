using AttributeRenderingLibrary;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SmolFurniture
{
    /// <summary>
    /// ARL renderer adapted for animated doors. The normal ARL block-entity renderer
    /// is replaced with a non-rendering variant store; BEBehaviorSmolDoor uses those
    /// variants when it builds the vanilla door animation mesh.
    /// </summary>
    public class BlockBehaviorSmolDoorAttributeRendering : BlockBehaviorShapeTexturesFromAttributes
    {
        public BlockBehaviorSmolDoorAttributeRendering(Block block) : base(block)
        {
        }

        protected override void AddMissingBlockEntityBehavior()
        {
            const string behaviorName = "SmolFurniture.DoorAttributeRendering";
            if (block.BlockEntityBehaviors.IndexOf(entry => entry.Name == behaviorName) >= 0)
            {
                return;
            }

            block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(new BlockEntityBehaviorType
            {
                Name = behaviorName,
                properties = null
            });
        }
    }

    public class BlockEntityBehaviorSmolDoorAttributeRendering : BlockEntityBehaviorShapeTexturesFromAttributes
    {
        public BlockEntityBehaviorSmolDoorAttributeRendering(BlockEntity blockEntity) : base(blockEntity)
        {
        }

        protected override void Init()
        {
            if (Api?.Side == EnumAppSide.Client)
            {
                Blockentity.GetBehavior<BEBehaviorSmolDoor>()?.ReloadAttributeMesh(Variants);
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return false;
        }
    }

    public class BEBehaviorSmolDoor : BEBehaviorDoor
    {
        private MeshData variantOriginalMesh;
        private Shape variantShape;
        private string variantMeshKey;
        private AnimatableRenderer variantRenderer;

        public BEBehaviorSmolDoor(BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public override void OnBlockPlaced(ItemStack byItemStack, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.OnBlockPlaced(byItemStack, byPlayer, blockSel);

            // Vanilla door placement reinitializes the animator with the block's
            // default mesh after ARL has initialized the wood-variant mesh.
            if (Api is ICoreClientAPI && byItemStack != null)
            {
                ReloadAttributeMesh(Variants.FromStack(byItemStack));
            }
        }

        public void ReloadAttributeMesh(Variants variants)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            BlockBehaviorSmolDoorAttributeRendering renderingBehavior =
                Block?.GetBehavior<BlockBehaviorSmolDoorAttributeRendering>();

            if (capi == null || renderingBehavior == null || variants == null || !variants.Any)
            {
                return;
            }

            string meshKey = $"smolfurniture-door-{Block.Code}-{variants}";
            if (variantOriginalMesh != null && variantMeshKey == meshKey)
            {
                RestoreVariantAnimator();
                return;
            }

            Shape shape = renderingBehavior.GetShape(
                null,
                Pos,
                variants,
                null,
                out CompositeShape compositeShape);

            if (shape == null || compositeShape == null)
            {
                return;
            }

            UniversalShapeTextureSource textureSource = new UniversalShapeTextureSource(
                capi,
                capi.BlockTextureAtlas,
                shape,
                compositeShape.Base.ToString());

            if (Block.Textures != null)
            {
                foreach (var texture in Block.Textures)
                {
                    textureSource.textures[texture.Key] = texture.Value;
                }
            }

            ShapeOverlayHelper.BakeVariantTextures(
                capi,
                textureSource,
                variants,
                renderingBehavior.texturesByType);

            compositeShape.IgnoreElements = renderingBehavior.GetShapeIgnoreElements(variants, compositeShape);
            TesselationMetaData meta = new TesselationMetaData
            {
                QuantityElements = compositeShape.QuantityElements,
                SelectiveElements = renderingBehavior.GetShapeSelectiveElements(variants, compositeShape),
                IgnoreElements = compositeShape.IgnoreElements
            };

            variantOriginalMesh = animUtil.CreateMesh(
                meshKey,
                shape,
                out variantShape,
                textureSource,
                meta);
            variantMeshKey = meshKey;

            animUtil.InitializeAnimator(meshKey, variantOriginalMesh, variantShape, null);
            variantRenderer = animUtil.renderer;
            UpdateMeshAndAnimations();

            if (opened)
            {
                float easingSpeed = Block.Attributes?["easingSpeed"].AsFloat(10) ?? 10;
                animUtil.StartAnimation(new AnimationMetaData
                {
                    Animation = "opened",
                    Code = "opened",
                    EaseInSpeed = easingSpeed,
                    EaseOutSpeed = easingSpeed
                });
            }

            capi.World.BlockAccessor.MarkBlockDirty(Pos);
        }

        private void RestoreVariantAnimator()
        {
            if (variantOriginalMesh == null || variantShape == null || variantMeshKey == null ||
                animUtil == null || (variantRenderer != null && object.ReferenceEquals(variantRenderer, animUtil.renderer)))
            {
                return;
            }

            animUtil.InitializeAnimator(variantMeshKey, variantOriginalMesh, variantShape, null);
            variantRenderer = animUtil.renderer;
            UpdateMeshAndAnimations();
            (Api as ICoreClientAPI)?.World.BlockAccessor.MarkBlockDirty(Pos);
        }

        protected override void UpdateMeshAndAnimations()
        {
            if (variantOriginalMesh == null)
            {
                base.UpdateMeshAndAnimations();
                return;
            }

            mesh = variantOriginalMesh.Clone();
            if (RotateYRad != 0)
            {
                float rotation = invertHandles ? -RotateYRad : RotateYRad;
                mesh = mesh.Rotate(0, rotation, 0);
                animUtil.renderer.rotationDeg.Y = rotation * GameMath.RAD2DEG;
            }

            if (invertHandles)
            {
                Matrixf matrix = new Matrixf();
                matrix.Translate(0.5f, 0.5f, 0.5f).Scale(-1, 1, 1).Translate(-0.5f, -0.5f, -0.5f);
                mesh.MatrixTransform(matrix.Values);

                animUtil.renderer.backfaceCulling = false;
                animUtil.renderer.ScaleX = -1;
            }
        }
    }
}
