using EFT.InventoryLogic;

namespace QuickSellFlea.Utils;

internal static class ItemUtils
{
    public static bool CanSell(this Item item)
    {
        if (!item.CanSellOnRagfair)
        {
            return false;
        }

        if (item.IsNotEmpty())
        {
            return false;
        }

        if (RagFairClass.Settings.isOnlyFoundInRaidAllowed && !item.CanSellOnRagfairRaidRelated)
        {
#if DEBUG
            CSF_Plugin.CSF_Logger.LogWarning("Flea only allows FiR, but item is not FiR, skipping");
#endif
            return false;
        }

        var parentItems = item.GetAllParentItems();
        return !parentItems.Any(i => i is InventoryEquipment);
    }
}
