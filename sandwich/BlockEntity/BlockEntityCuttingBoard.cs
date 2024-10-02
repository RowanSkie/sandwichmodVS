using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using sandwich;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ACulinaryArtillery;
using EFRecipes;
using Newtonsoft.Json;
using System.IO;

namespace sandwich;

public class BlockEntityCuttingBoard : BlockEntityDisplay
{
    private readonly InventoryGeneric inventory;

    public const int SlotCount = 1;

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => cuttingBoardInvClassName;
    public override string AttributeTransformCode => attributeOnCuttingBoardTransform;

    public BlockEntityCuttingBoard()
    {
        inventory = new InventoryGeneric(SlotCount, $"{cuttingBoardInvClassName}-0", Api, (_, inv) => new ItemSlotCuttingBoard(inv));
    }

    public bool OnInteract(IPlayer byPlayer)
    {
        ItemSlot invSlot = inventory.First();
        ItemSlot activeslot = byPlayer.InventoryManager.ActiveHotbarSlot;
        //bool hasItemOnBoard = !invSlot.Empty;

        //if (hasItemOnBoard && activeslot.Empty && (byPlayer.Entity.Controls.ShiftKey))
        //{
        //    activeslot.MarkDirty();
        //    invSlot.MarkDirty();
        //    updateMeshes();
        //    MarkDirty(redrawOnClient: true);
        //    return HandleShiftInteraction(byPlayer, invSlot, activeslot);
        //}

        // Check if a custom interaction is possible (e.g., slicing an item on the cutting board)
        if (TryCustomInteraction(byPlayer, invSlot, activeslot))
        {
            activeslot.MarkDirty();
            invSlot.MarkDirty();
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return true;
        }

        // Check if a sandwich can be made by combining the items in the active slot and the cutting board
        if (ItemSandwich.TryAdd(invSlot, activeslot, byPlayer, byPlayer.Entity.World))
        {
            activeslot.MarkDirty();
            invSlot.MarkDirty();
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return true;
        }

        // Check if the active slot item is storable on the cutting board
        bool storable = ItemSlotCuttingBoard.IsStorable(activeslot?.Itemstack?.Collectible);

        // Check if the player is trying to take an item from the cutting board (if the active slot is empty or the item is not storable)
        if ((activeslot.Empty || !storable) && TryTake(byPlayer, 0))
        {
            activeslot.MarkDirty();
            invSlot.MarkDirty();
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return true;
        }

        // Check if the player is trying to place an item on the cutting board
        AssetLocation sound = activeslot?.Itemstack?.Block?.Sounds?.Place;
        if (storable && TryPut(activeslot, 0))
        {
            Api.World.PlaySoundAt(sound ?? soundBuild, byPlayer.Entity, byPlayer, randomizePitch: true, 16f);
            activeslot.MarkDirty();
            invSlot.MarkDirty();
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return true;
        }

        return false;
    }

    //private bool HandleShiftInteraction(IPlayer byPlayer, ItemSlot invSlot, ItemSlot activeslot)
    //{
    //    ItemStack cuttingBoardItem = invSlot?.Itemstack;

    //    // Check for item on the cutting board and handle sandwich layers
    //    if (cuttingBoardItem != null && cuttingBoardItem.Attributes.HasAttribute(attributeSandwichLayers))
    //    {
    //        Api.World.Logger.Event("FOUND SANDWICH LAYERS");

    //        // Get the JSON string directly, ensuring it's treated correctly
    //        string sandwichLayersJson = cuttingBoardItem.Attributes.GetString(attributeSandwichLayers, null);

    //        if (!string.IsNullOrEmpty(sandwichLayersJson))
    //        {
    //            // Parse the JSON string into a JObject for easy navigation
    //            JObject sandwichLayersObject = JObject.Parse(sandwichLayersJson);

    //            // Check if there are any layers
    //            if (sandwichLayersObject.Count > 0)
    //            {
    //                // Get the most recent layer (last added layer)
    //                var mostRecentLayerKey = sandwichLayersObject.Properties().Last();
    //                string itemCode = mostRecentLayerKey.Value.ToString(); // Adjusted to get the string directly

    //                if (!string.IsNullOrEmpty(itemCode))
    //                {
    //                    Api.World.Logger.Event($"Found item code for layer {mostRecentLayerKey.Name}: {itemCode}");

    //                    // Now, use the item code to spawn or give the item to the player
    //                    AssetLocation itemCodeLocation = new AssetLocation(itemCode);
    //                    ItemStack newItem = new ItemStack(Api.World.GetItem(itemCodeLocation), 1);

    //                    // Give item to player or spawn it in the world
    //                    if (byPlayer.InventoryManager.TryGiveItemstack(newItem))
    //                    {
    //                        Api.World.Logger.Event($"Gave item {itemCode} back to the player.");
    //                    }
    //                    else
    //                    {
    //                        Api.World.SpawnItemEntity(newItem, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
    //                        Api.World.Logger.Event($"Spawned item {itemCode} at position.");
    //                    }

    //                    // Now remove the most recent layer from the sandwichLayers
    //                    sandwichLayersObject.Remove(mostRecentLayerKey.Name);

    //                    // Update the sandwichLayers attribute on the cutting board item
    //                    string updatedLayersJson = sandwichLayersObject.ToString();
    //                    cuttingBoardItem.Attributes.SetString(attributeSandwichLayers, updatedLayersJson);
    //                    Api.World.Logger.Event("Removed the most recent layer and updated sandwich layers.");
    //                }
    //                else
    //                {
    //                    Api.World.Logger.Event($"No valid 'code' found for the most recent layer {mostRecentLayerKey.Name}.");
    //                }
    //            }
    //            else
    //            {
    //                Api.World.Logger.Event("No layers found in sandwichLayers.");
    //            }
    //        }
    //        else
    //        {
    //            Api.World.Logger.Event("Failed to parse sandwich layers from JSON.");
    //        }
    //    }
    //    else
    //    {
    //        Api.World.Logger.Event("No sandwich layers found on the cutting board.");
    //        return false;
    //    }

    //    return true; // Return true to indicate success
    //}

    private bool TryCustomInteraction(IPlayer byPlayer, ItemSlot invSlot, ItemSlot activeslot)
    {
        CuttingBoardProperties props = CuttingBoardProperties.GetProps(invSlot?.Itemstack?.Collectible);

        EnumTool? tool = activeslot?.Itemstack?.Collectible?.Tool;

        if (tool == EnumTool.Knife || tool == EnumTool.Sword)
        {
            if (!inventory[0].Empty)
            {
                ItemStack breadStack = inventory[0].Itemstack;
                string breadPath = breadStack?.Collectible?.Code?.Path;

                if (breadPath != null && BreadStorage.BreadTypes.ContainsKey(breadPath))
                {
                    Api.World.Logger.Event($"The item path is: {breadPath}");

                    string breadType = breadPath.Split('-')[1];
                    AssetLocation breadSliceAsset = new AssetLocation($"sandwich:slicedbread-{breadType}-perfect");
                    Api.World.Logger.Event($"The item type is: {breadType}");

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Spawn 4 bread slices
                        ItemStack breadSlices = new ItemStack(Api.World.GetItem(breadSliceAsset), 4);
                        ItemSandwich sandwichItem = new ItemSandwich();

                        Api.World.SpawnItemEntity(breadSlices, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        sandwichItem.OnCreatedBySlicing(inventory[0], breadSlices, breadStack, 4);
                        inventory[0].TakeOutWhole();
                        MarkDirty(true);
                        updateMeshes();

                        Api.World.Logger.Event($"Sliced {breadType} bread into slices.");
                    }
                    return true;
                } else if (breadPath != null && breadPath.StartsWith("bushmeat-") && breadPath.EndsWith("-cooked"))
                {
                    Api.World.Logger.Event($"The item path is: {breadPath}");

                    AssetLocation breadSliceAsset = new AssetLocation($"sandwich:slicedmeat");

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Spawn 4 bread slices
                        ItemStack breadSlices = new ItemStack(Api.World.GetItem(breadSliceAsset), 2);
                        ItemSandwich sandwichItem = new ItemSandwich();

                        Api.World.SpawnItemEntity(breadSlices, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        inventory[0].TakeOutWhole();
                        MarkDirty(true);
                        updateMeshes();
                    }
                    return true;
                }
                else if (breadPath != null && breadPath.StartsWith("bushmeat-") && breadPath.EndsWith("-cooked"))
                {
                    Api.World.Logger.Event($"The item path is: {breadPath}");

                    AssetLocation breadSliceAsset = new AssetLocation($"sandwich:slicedmeat");

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Spawn 4 bread slices
                        ItemStack breadSlices = new ItemStack(Api.World.GetItem(breadSliceAsset), 2);
                        ItemSandwich sandwichItem = new ItemSandwich();

                        Api.World.SpawnItemEntity(breadSlices, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        inventory[0].TakeOutWhole();
                        MarkDirty(true);
                        updateMeshes();
                    }
                    return true;
                }
                else if (breadPath != null && breadPath.StartsWith("redmeat-") && breadPath.EndsWith("-cooked"))
                {
                    Api.World.Logger.Event($"The item path is: {breadPath}");

                    AssetLocation breadSliceAsset = new AssetLocation($"sandwich:slicedmeat");

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Spawn 4 bread slices
                        ItemStack breadSlices = new ItemStack(Api.World.GetItem(breadSliceAsset), 2);
                        ItemSandwich sandwichItem = new ItemSandwich();

                        Api.World.SpawnItemEntity(breadSlices, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        inventory[0].TakeOutWhole();
                        MarkDirty(true);
                        updateMeshes();
                    }
                    return true;
                }
                else if (breadPath != null && breadPath.StartsWith("cheese-") && breadPath.EndsWith("-cheddar-1slice"))
                {

                    AssetLocation breadSliceAsset = new AssetLocation($"sandwich:slicedcheese");

                    if (Api.Side == EnumAppSide.Server)
                    {
                        // Spawn 4 bread slices
                        ItemStack breadSlices = new ItemStack(Api.World.GetItem(breadSliceAsset), 4);
                        ItemSandwich sandwichItem = new ItemSandwich();

                        Api.World.SpawnItemEntity(breadSlices, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        inventory[0].TakeOutWhole();
                        MarkDirty(true);
                        updateMeshes();
                    }
                    return true;
                }
                return false; // Bread item did not match the expected format
            }
            return false; // Inventory slot is empty
        }
        return false; // Tool is not a Knife or Sword
    }




    private bool TryPut(ItemSlot slot, int slotId)
    {
        if (Inventory.Count > slotId && inventory[slotId].Empty)
        {
            int amount = slot.TryPutInto(Api.World, inventory[slotId]);
            return amount > 0;
        }
        return false;
    }

    private bool TryTake(IPlayer byPlayer, int slotId)
    {
        if (Inventory.Count > slotId && !inventory[slotId].Empty)
        {
            ItemStack stack = inventory[slotId].TakeOutWhole();
            if (byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                AssetLocation sound = stack?.Block?.Sounds?.Place;
                Api.World.PlaySoundAt(sound ?? soundBuild, byPlayer.Entity, byPlayer, randomizePitch: true, 16f);
            }
            if (stack.StackSize > 0)
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            return true;
        }
        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        ItemSlot slot = inventory.First();
        sb.AppendLine(slot.Empty ? Lang.Get(langEmpty) : Lang.Get(langContents0x1, slot.StackSize, slot.GetStackName()));

        if (slot.Empty)
        {
            return;
        }

        SandwichProperties props = SandwichProperties.FromStack(slot.Itemstack, forPlayer.Entity.World);
        if (props == null)
        {
            return;
        }

        props.GetDescription(slot, sb, Api.World);
    }

    protected override float[][] genTransformationMatrices()
    {
        Vec3f[] Offsets = Block?.Attributes?[attributeOffsets]?.AsObject<Vec3f[]>();
        if (Offsets == null || Offsets.Length < SlotCount)
        {
            return Array.Empty<float[]>();
        }
        float[][] tfMatrices = new float[SlotCount][];
        for (int index = 0; index < tfMatrices.Length; index++)
        {
            Vec3f off = Offsets[index];
            off = new Matrixf()
                .RotateDeg(Block.Shape.RotateXYZCopy)
                .TransformVector(off.ToVec4f(0f))
                .XYZ;

            Matrixf mat = new Matrixf()
            .Translate(off.X, off.Y, off.Z)
            .Translate(0.5f, 0.5f, 0.5f)
            .RotateDeg(Block.Shape.RotateXYZCopy)
            .Translate(-0.5f, -0.5f, -0.5f);

            tfMatrices[index] = mat.Values;
        }
        return tfMatrices;
    }


}