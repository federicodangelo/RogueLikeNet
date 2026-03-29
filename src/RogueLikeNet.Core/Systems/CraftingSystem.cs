using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes crafting requests: validates ingredients, removes them from inventory, and adds the crafted item.
/// </summary>
public class CraftingSystem
{
    public void Update(Arch.Core.World world)
    {
        var actions = new List<(Entity Player, int RecipeId)>();

        var query = new QueryDescription().WithAll<PlayerInput, Inventory>();
        world.Query(in query, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.Craft) return;
            actions.Add((player, input.ItemSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, recipeId) in actions)
        {
            if (!world.IsAlive(player)) continue;
            if (recipeId < 0 || recipeId >= CraftingDefinitions.All.Length) continue;

            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null) continue;

            var recipe = CraftingDefinitions.All[recipeId];
            if (!CraftingDefinitions.CanCraft(recipe, inv.Items)) continue;

            // Remove ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                int remaining = ingredient.Count;
                for (int i = inv.Items.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    if (inv.Items[i].ItemTypeId != ingredient.ItemTypeId) continue;
                    var item = inv.Items[i];
                    int take = Math.Min(remaining, item.StackCount);
                    item.StackCount -= take;
                    remaining -= take;
                    if (item.StackCount <= 0)
                    {
                        inv.Items.RemoveAt(i);
                        if (world.Has<QuickSlots>(player))
                        {
                            ref var qs = ref world.Get<QuickSlots>(player);
                            qs.OnItemRemoved(i);
                        }
                    }
                    else
                    {
                        inv.Items[i] = item;
                    }
                }
            }

            // Add crafted item to inventory
            var resultDef = ItemDefinitions.Get(recipe.ResultItemTypeId);
            var resultItem = new ItemData
            {
                ItemTypeId = recipe.ResultItemTypeId,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = recipe.ResultCount,
            };

            // Try to stack with existing items
            bool stacked = false;
            if (resultDef.Stackable)
            {
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    if (inv.Items[i].ItemTypeId == resultItem.ItemTypeId &&
                        inv.Items[i].StackCount < resultDef.MaxStackSize)
                    {
                        var existing = inv.Items[i];
                        int canAdd = resultDef.MaxStackSize - existing.StackCount;
                        int toAdd = Math.Min(canAdd, resultItem.StackCount);
                        existing.StackCount += toAdd;
                        inv.Items[i] = existing;
                        resultItem.StackCount -= toAdd;
                        if (resultItem.StackCount <= 0) { stacked = true; break; }
                    }
                }
            }

            if (!stacked && resultItem.StackCount > 0 && !inv.IsFull)
            {
                inv.Items.Add(resultItem);
            }
        }
    }
}
