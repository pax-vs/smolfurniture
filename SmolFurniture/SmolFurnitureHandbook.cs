using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SmolFurniture
{
    internal static class SmolFurnitureHandbook
    {
        const string AttributeRoot = "types";

        static readonly HandbookDefinition[] Definitions =
        {
            new HandbookDefinition(
                "smolchair",
                "smolchair",
                "handbook-smolchair",
                new[] { ("cloth", "plain"), ("wood", "aged") }),
            new HandbookDefinition(
                "tallsmolchair-north",
                "tallsmolchair",
                "handbook-tallsmolchair",
                new[] { ("cloth", "plain"), ("wood", "aged") }),
            new HandbookDefinition(
                "smoltable",
                "smoltable",
                "handbook-smoltable",
                new[] { ("wood", "aged") }),
            new HandbookDefinition(
                "smolbed-north",
                "smolbed",
                "handbook-smolbed",
                new[] { ("cloth", "plain") }),
            new HandbookDefinition(
                "smoldoorsleekpanel",
                "smoldoorsleekpanel",
                "handbook-smoldoorsleekpanel",
                new[] { ("wood", "aged") }),
            new HandbookDefinition(
                "smoldoorsleekwindowed",
                "smoldoorsleekwindowed",
                "handbook-smoldoorsleekwindowed",
                new[] { ("wood", "aged") })
        };

        public static void Register(ICoreClientAPI api)
        {
            ModSystemSurvivalHandbook handbook = api.ModLoader.GetModSystem<ModSystemSurvivalHandbook>();
            if (handbook == null)
            {
                return;
            }

            handbook.OnInitCustomPages += pages => AddPages(api, pages);
        }

        static void AddPages(ICoreClientAPI api, List<GuiHandbookPage> pages)
        {
            foreach (HandbookDefinition definition in Definitions)
            {
                Block block = api.World.GetBlock(
                    new AssetLocation(SmolFurnitureSystem.ModDomain, definition.BlockPath));
                if (block == null || block.Id == 0)
                {
                    continue;
                }

                List<ItemStack> stacks = GetHandbookStacks(api, block);
                if (stacks.Count == 0)
                {
                    stacks.Add(CreateStack(block, definition.DefaultTypes));
                }

                GridRecipe[] recipes = api.World.GridRecipes
                    .Where(recipe => recipe.ShowInCreatedBy && IsRecipeFor(recipe, block))
                    .ToArray();

                pages.Add(new SmolFurnitureVariantHandbookPage(
                    definition.PageCode,
                    Lang.Get(SmolFurnitureSystem.ModDomain + ":" + definition.TitleLangCode),
                    stacks,
                    recipes));
            }
        }

        static List<ItemStack> GetHandbookStacks(ICoreClientAPI api, Block block)
        {
            List<ItemStack> stacks = block.GetHandBookStacks(api) ?? new List<ItemStack>();
            List<ItemStack> deduped = new List<ItemStack>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (ItemStack stack in stacks)
            {
                if (stack == null)
                {
                    continue;
                }

                if (seen.Add(GuiHandbookItemStackPage.PageCodeForStack(stack)))
                {
                    deduped.Add(stack);
                }
            }

            return deduped;
        }

        static ItemStack CreateStack(Block block, IEnumerable<(string Key, string Value)> values)
        {
            ItemStack stack = new ItemStack(block, 1);
            ITreeAttribute types = stack.Attributes.GetOrAddTreeAttribute(AttributeRoot);
            foreach ((string key, string value) in values)
            {
                types.SetString(key, value);
            }

            return stack;
        }

        static bool IsRecipeFor(GridRecipe recipe, Block block)
        {
            AssetLocation outputCode = recipe.RecipeOutput?.ResolvedItemStack?.Collectible?.Code;
            return outputCode != null && outputCode.Equals(block.Code);
        }

        internal static RichTextComponentBase[] BuildPageComponents(
            string title,
            List<ItemStack> stacks,
            GridRecipe[] recipes,
            ICoreClientAPI api,
            ItemStack[] allStacks,
            ActionConsumable<string> openDetailPageFor)
        {
            List<RichTextComponentBase> components = new List<RichTextComponentBase>();
            SlideshowItemstackTextComponent header = AddVariantHeader(
                components,
                title,
                stacks,
                api,
                openDetailPageFor);

            if (recipes.Length > 0)
            {
                components.Add(new ClearFloatTextComponent(api, 7));
                components.Add(new RichTextComponent(
                    api,
                    Lang.Get("Crafting") + "\n",
                    CairoFont.WhiteSmallText()));

                SlideshowGridRecipeTextComponent recipeComponent = AddGridRecipeComponent(
                    components,
                    recipes,
                    api,
                    allStacks,
                    openDetailPageFor);
                header.overrideCurrentItemStack = () =>
                    recipeComponent.GenerateCurrentVisibleOutputStack() ?? stacks[0];
            }

            return components.ToArray();
        }

        static SlideshowItemstackTextComponent AddVariantHeader(
            List<RichTextComponentBase> components,
            string title,
            List<ItemStack> stacks,
            ICoreClientAPI api,
            ActionConsumable<string> openDetailPageFor)
        {
            SlideshowItemstackTextComponent header = new SlideshowItemstackTextComponent(
                api,
                stacks.ToArray(),
                100,
                EnumFloat.Left,
                stack => openDetailPageFor(PageCodeForStack(api, stack)));

            components.Add(header);
            components.AddRange(VtmlUtil.Richtextify(
                api,
                title + "\n",
                CairoFont.WhiteSmallishText()));
            components.Add(new ClearFloatTextComponent(api, 8));
            return header;
        }

        static SlideshowGridRecipeTextComponent AddGridRecipeComponent(
            List<RichTextComponentBase> components,
            GridRecipe[] recipes,
            ICoreClientAPI api,
            ItemStack[] allStacks,
            ActionConsumable<string> openDetailPageFor)
        {
            SlideshowGridRecipeTextComponent recipeComponent = new SlideshowGridRecipeTextComponent(
                api,
                recipes,
                40,
                EnumFloat.Inline,
                stack => openDetailPageFor(PageCodeForStack(api, stack)),
                allStacks)
            {
                VerticalAlign = EnumVerticalAlign.Top,
                PaddingRight = 8,
                PaddingLeft = 2
            };
            components.Add(recipeComponent);

            components.Add(new RichTextComponent(api, " = ", CairoFont.WhiteMediumText())
            {
                VerticalAlign = EnumVerticalAlign.Middle,
                PaddingRight = 5
            });

            ItemStack[] outputStacks = recipes
                .Select(recipe => recipe.RecipeOutput?.ResolvedItemStack)
                .Where(stack => stack != null)
                .ToArray();

            components.Add(new SlideshowItemstackTextComponent(
                api,
                outputStacks,
                40,
                EnumFloat.Inline,
                stack => openDetailPageFor(PageCodeForStack(api, stack)))
            {
                VerticalAlign = EnumVerticalAlign.Middle,
                ShowStackSize = true,
                overrideCurrentItemStack = recipeComponent.GenerateCurrentVisibleOutputStack
            });
            components.Add(new ClearFloatTextComponent(api, 3));
            return recipeComponent;
        }

        internal static string PageCodeForStack(ICoreClientAPI api, ItemStack stack)
        {
            return stack?.Collectible?
                    .GetCollectibleInterface<IHandBookPageCodeProvider>()?
                    .HandbookPageCodeForStack(api.World, stack)
                ?? GuiHandbookItemStackPage.PageCodeForStack(stack);
        }

        sealed class HandbookDefinition
        {
            public HandbookDefinition(
                string blockPath,
                string pagePath,
                string titleLangCode,
                (string Key, string Value)[] defaultTypes)
            {
                BlockPath = blockPath;
                PageCode = "block-" + SmolFurnitureSystem.ModDomain + ":" + pagePath;
                TitleLangCode = titleLangCode;
                DefaultTypes = defaultTypes;
            }

            public string BlockPath { get; }
            public string PageCode { get; }
            public string TitleLangCode { get; }
            public (string Key, string Value)[] DefaultTypes { get; }
        }
    }

    internal sealed class SmolFurnitureVariantHandbookPage : GuiHandbookPage
    {
        readonly string pageCode;
        readonly string title;
        readonly List<ItemStack> stacks;
        readonly GridRecipe[] recipes;
        readonly string searchText;
        readonly InventoryBase inventory;
        readonly DummySlot dummySlot;
        LoadedTexture texture;

        public SmolFurnitureVariantHandbookPage(
            string pageCode,
            string title,
            List<ItemStack> stacks,
            GridRecipe[] recipes)
        {
            this.pageCode = pageCode;
            this.title = title;
            this.stacks = stacks;
            this.recipes = recipes;
            inventory = new CreativeInventoryTab(1, "not-used", null);
            dummySlot = new DummySlot(stacks[0], inventory);
            string assetSearchAlias = pageCode.Substring(pageCode.IndexOf(':') + 1);
            searchText = (title + " " + assetSearchAlias + " " +
                    string.Join(" ", stacks.Select(stack => stack.GetName())))
                .ToSearchFriendly();
        }

        public override string PageCode => pageCode;
        public override string CategoryCode => "stack";
        public override bool IsDuplicate => false;
        public override float SearchWeightOffset => 0;

        public override void RenderListEntryTo(
            ICoreClientAPI api,
            float dt,
            double x,
            double y,
            double cellWidth,
            double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);
            float padding = (float)GuiElement.scaled(10);
            int index = (int)(api.ElapsedMilliseconds / 1000 % stacks.Count);

            dummySlot.Itemstack = stacks[index];
            api.Render.RenderItemstackToGui(
                dummySlot,
                x + padding + size / 2,
                y + size / 2,
                100,
                size,
                ColorUtil.WhiteArgb,
                true,
                false,
                false);

            if (texture == null)
            {
                texture = new TextTextureUtil(api).GenTextTexture(title, CairoFont.WhiteSmallText());
            }

            api.Render.Render2DTexturePremultipliedAlpha(
                texture.TextureId,
                x + size + GuiElement.scaled(25),
                y + size / 4 - GuiElement.scaled(3),
                texture.Width,
                texture.Height,
                50);
        }

        public override void ComposePage(
            GuiComposer detailViewGui,
            ElementBounds textBounds,
            ItemStack[] allStacks,
            ActionConsumable<string> openDetailPageFor)
        {
            detailViewGui.AddRichtext(
                SmolFurnitureHandbook.BuildPageComponents(
                    title,
                    stacks,
                    recipes,
                    detailViewGui.Api,
                    allStacks,
                    openDetailPageFor),
                textBounds,
                "richtext");
        }

        public override PageText GetPageText()
        {
            return new PageText
            {
                Title = title.ToSearchFriendly(),
                Text = searchText
            };
        }

        public override void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
