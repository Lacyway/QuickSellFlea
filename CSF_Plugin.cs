using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using QuickSellFlea.Patches;

namespace QuickSellFlea;

[BepInPlugin("com.lacyway.csf", "QuickSellFlea", PluginVersion)]
internal class CSF_Plugin : BaseUnityPlugin
{
    public const string PluginVersion = "1.0.0";

    internal static ManualLogSource CSF_Logger;

    public static ConfigEntry<bool> ShowListingPrice { get; set; }

    protected void Awake()
    {
        CSF_Logger = Logger;
        CSF_Logger.LogInfo($"{nameof(CSF_Plugin)} has been loaded.");

        ShowListingPrice = Config.Bind("QuickSellFlea", "Show Listing Price", false,
            new ConfigDescription("Whether to show the listing price in the tooltip, otherwise the total sell value (of all items in the stack, if stackable)"));

        new ItemUiContext_GetItemContextInteractions_Patch().Enable();
    }
}

public struct PostPriceData(RagFairClass ragFair, ContextInteractionsAbstractClass interactionsClass,
        ItemUiContext itemUiContext, ItemInfoInteractionsAbstractClass<EItemInfoButton> infoInteractionsClass,
        Dictionary<EItemInfoButton, string> itemInfoDict, InventoryController inventoryController,
        ItemContextAbstractClass itemContext, HandbookClass handbookClass)
{
    public readonly Item Item => ItemContext.Item;

    public readonly RagFairClass RagFair = ragFair;
    public readonly ContextInteractionsAbstractClass InteractionsClass = interactionsClass;
    public readonly ItemUiContext ItemUiContext = itemUiContext;
    public readonly ItemInfoInteractionsAbstractClass<EItemInfoButton> InfoInteractionsClass = infoInteractionsClass;
    public readonly Dictionary<EItemInfoButton, string> ItemInfoDict = itemInfoDict;
    public readonly InventoryController InventoryController = inventoryController;
    public readonly ItemContextAbstractClass ItemContext = itemContext;
    public readonly HandbookClass HandbookClass = handbookClass;
    public readonly Dictionary<Item, ItemAddress> OfferDict = [];

    public float AveragePrice;
}
