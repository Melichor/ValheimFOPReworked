using HarmonyLib;
using UnityEngine;

namespace ValheimFOPReworked.Patches
{
    internal static class UiPatches
    {
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData),
            typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
        private static class TooltipLine
        {
            private static void Postfix(ItemDrop.ItemData item, int qualityLevel, bool crafting, ref string __result)
            {
                // Recipe tooltips (crafting) describe the item in general, not one item with its experience
                if (!crafting && item != null && qualityLevel == item.m_quality && ItemXp.IsRefinable(item))
                {
                    __result += "\n" + Texts.Tooltip(item);
                }
            }
        }

        /// <summary>An experience bar above the durability bar of every inventory slot that has gathered some.</summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        private static class ExperienceBar
        {
            private const string BarName = "FOPReworked_XpBar";
            private const float BarHeight = 4f;
            private static readonly Color Filling = new Color(0.62f, 0.45f, 1f);
            private static readonly Color Full = new Color(1f, 0.84f, 0f);

            private static void Postfix(InventoryGrid __instance)
            {
                foreach (InventoryElement element in __instance.m_elements)
                {
                    GuiBar bar = FindBar(element);
                    if (bar)
                    {
                        bar.gameObject.SetActive(false);
                    }
                }
                int width = __instance.m_inventory.GetWidth();
                foreach (ItemDrop.ItemData item in __instance.m_inventory.GetAllItems())
                {
                    if (!ItemXp.CanEarn(item))
                    {
                        continue;
                    }
                    float xp = ItemXp.Get(item);
                    InventoryElement element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
                    if (xp <= 0f || !element)
                    {
                        continue;
                    }
                    GuiBar bar = FindBar(element) ?? CreateBar(__instance, element);
                    float fraction = xp / ItemXp.Required(item);
                    bar.gameObject.SetActive(true);
                    bar.SetValue(fraction);
                    // A full bar pulses gently, the item is waiting for the Forge
                    bar.SetColor(fraction >= 1f
                        ? Color.Lerp(Full, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 4f))
                        : Filling);
                }
            }

            private static GuiBar FindBar(InventoryElement element)
            {
                Transform bar = element.m_durability.transform.parent.Find(BarName);
                return bar ? bar.GetComponent<GuiBar>() : null;
            }

            private static GuiBar CreateBar(InventoryGrid grid, InventoryElement element)
            {
                // Copied from the slot prefab, whose durability bar is still at its full width. In the 64x64 slot the
                // durability bar is a 54x5 strip along the bottom; the level and hotbar number take the top corners,
                // so the experience bar is a thinner strip just above the durability bar.
                GuiBar template = grid.m_elementPrefab.GetComponent<InventoryElement>().m_durability;
                RectTransform durability = (RectTransform)element.m_durability.transform;
                GameObject go = Object.Instantiate(template.gameObject, durability.parent);
                go.name = BarName;
                RectTransform rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(durability.sizeDelta.x, BarHeight);
                rect.anchoredPosition = durability.anchoredPosition +
                                        new Vector2(0f, (durability.sizeDelta.y + BarHeight) / 2f + 1f);
                return go.GetComponent<GuiBar>();
            }
        }
    }
}
