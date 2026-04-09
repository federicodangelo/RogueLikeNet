using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes crafting requests: validates ingredients, removes them from inventory, and adds the crafted item.
/// </summary>
public class CraftingSystem
{
    public void Update(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Craft) continue;

            int recipeId = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            var recipe = GameData.Instance.Recipes.Get(recipeId);
            if (recipe == null) continue;
            if (!RecipeRegistry.CanCraft(recipe, player.Inventory.Items)) continue;

            // Remove ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                int remaining = ingredient.Count;
                for (int i = player.Inventory.Items.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    if (player.Inventory.Items[i].ItemTypeId != ingredient.NumericItemId) continue;
                    var item = player.Inventory.Items[i];
                    int take = Math.Min(remaining, item.StackCount);
                    item.StackCount -= take;
                    remaining -= take;
                    if (item.StackCount <= 0)
                    {
                        player.Inventory.Items.RemoveAt(i);
                        player.QuickSlots.OnItemRemoved(i);
                    }
                    else
                    {
                        player.Inventory.Items[i] = item;
                    }
                }
            }

            // Add crafted item
            var resultDef = GameData.Instance.Items.Get(recipe.Result.NumericItemId);
            if (resultDef == null) return;
            var resultItem = new ItemData
            {
                ItemTypeId = recipe.Result.NumericItemId,
                StackCount = recipe.Result.Count,
            };

            bool stacked = false;
            if (resultDef.Stackable)
            {
                for (int i = 0; i < player.Inventory.Items.Count; i++)
                {
                    if (player.Inventory.Items[i].ItemTypeId == resultItem.ItemTypeId &&
                        player.Inventory.Items[i].StackCount < resultDef.MaxStackSize)
                    {
                        var existing = player.Inventory.Items[i];
                        int canAdd = resultDef.MaxStackSize - existing.StackCount;
                        int toAdd = Math.Min(canAdd, resultItem.StackCount);
                        existing.StackCount += toAdd;
                        player.Inventory.Items[i] = existing;
                        resultItem.StackCount -= toAdd;
                        if (resultItem.StackCount <= 0) { stacked = true; break; }
                    }
                }
            }

            if (!stacked && resultItem.StackCount > 0 && !player.Inventory.IsFull)
            {
                player.Inventory.Items.Add(resultItem);
            }
        }
    }
}
