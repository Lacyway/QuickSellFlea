using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using QuickSellFlea.Patches;
using UnityEngine;

namespace QuickSellFlea;

[BepInPlugin("com.lacyway.csf", "QuickSellFlea", PluginVersion)]
internal class CSF_Plugin : BaseUnityPlugin
{
    public const string PluginVersion = "1.2.0";

    internal static ManualLogSource CSF_Logger;
    private static readonly EPostingCurrency[] _currencyValues =
        (EPostingCurrency[])Enum.GetValues(typeof(EPostingCurrency));

    public static ConfigEntry<bool> ShowListingPrice { get; set; }
    public static ConfigEntry<EPostingCurrency> PostingCurrency { get; set; }
    public static ConfigEntry<KeyboardShortcut> ChangeCurrencyKey { get; set; }

    protected void Awake()
    {
        CSF_Logger = Logger;
        CSF_Logger.LogInfo($"{nameof(CSF_Plugin)} has been loaded.");

        ShowListingPrice = Config.Bind("QuickSellFlea", "Show Listing Price", false,
            new ConfigDescription("Whether to show the listing price in the tooltip, otherwise the total sell value (of all items in the stack, if stackable)"));
        PostingCurrency = Config.Bind("QuickSellFlea", "Posting Currency", EPostingCurrency.RUB,
            new ConfigDescription("The currency to post the listings in"));
        ChangeCurrencyKey = Config.Bind("QuickSellFlea", "Change Currency Key", new KeyboardShortcut(KeyCode.Insert),
            new ConfigDescription("The key used to quickly change posting currency"));

        new ItemUiContext_GetItemContextInteractions_Patch().Enable();
    }

    protected void Update()
    {
        if (Input.GetKeyDown(ChangeCurrencyKey.Value.MainKey) && AreModifiersPressed(ChangeCurrencyKey.Value))
        {
            ChangeCurrency();
        }
    }

    private void ChangeCurrency()
    {
        var currentIndex = Array.IndexOf(_currencyValues, PostingCurrency.Value);
        PostingCurrency.Value = _currencyValues[(currentIndex + 1) % _currencyValues.Length];
        if (Singleton<NotificationManagerClass>.Instantiated)
        {
            NotificationManagerClass.DisplayMessageNotification($"Currency set to {PostingCurrency.Value}",
                iconType: ENotificationIconType.Note);
        }
    }

    private static bool AreModifiersPressed(KeyboardShortcut shortcut)
    {
        foreach (var key in shortcut.Modifiers)
        {
            if (!Input.GetKey(key))
            {
                return false;
            }
        }

        return true;
    }
}

public enum EPostingCurrency : int
{
    RUB = 1,
    USD = 120,
    EUR = 133
}

public struct PostPriceData(RagFairClass ragFair, ContextInteractionsAbstractClass interactionsClass,
        ItemUiContext itemUiContext, ItemInfoInteractionsAbstractClass<EItemInfoButton> infoInteractionsClass,
        Dictionary<EItemInfoButton, string> itemInfoDict, InventoryController inventoryController,
        ItemContextAbstractClass itemContext, HandbookClass handbookClass, bool selectAll)
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
    public readonly bool SelectAll = selectAll;
    public readonly Dictionary<Item, ItemAddress> OfferDict = [];
    public List<Item> Items;

    public float AveragePrice;
}
