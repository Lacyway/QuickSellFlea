using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.Communications;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using QuickSellFlea.Patches;
using UnityEngine;

namespace QuickSellFlea;

[BepInPlugin("com.lacyway.csf", "QuickSellFlea", PluginVersion)]
[BepInDependency("com.tyfon.uifixes", BepInDependency.DependencyFlags.SoftDependency)]
internal class CSF_Plugin : BaseUnityPlugin
{
    public const string PluginVersion = "1.4.4";

    internal static ManualLogSource CSF_Logger;
    private static readonly EPostingCurrency[] _currencyValues =
        (EPostingCurrency[])Enum.GetValues(typeof(EPostingCurrency));

    public static ConfigEntry<KeyboardShortcut> Hotkey { get; set; }
    public static ConfigEntry<bool> ShowListingPrice { get; set; }
    public static ConfigEntry<bool> BypassLimit { get; set; }
    public static ConfigEntry<bool> SkipMode { get; set; }
    public static ConfigEntry<EPostingCurrency> PostingCurrency { get; set; }
    public static ConfigEntry<KeyboardShortcut> ChangeCurrencyKey { get; set; }

    protected void Awake()
    {
        CSF_Logger = Logger;
        CSF_Logger.LogInfo($"{nameof(CSF_Plugin)} v{PluginVersion} has been loaded.");

        Hotkey = Config.Bind("QuickSellFlea", "Hotkey", new KeyboardShortcut(KeyCode.LeftControl),
            new ConfigDescription("The key held to show the quick sell interaction"));
        ShowListingPrice = Config.Bind("QuickSellFlea", "Show Listing Price", false,
            new ConfigDescription("Whether to show the listing price in the tooltip, otherwise the total sell value (of all items in the stack, if stackable)"));
        BypassLimit = Config.Bind("QuickSellFlea", "Bypass Limit", false,
            new ConfigDescription("Whether to flea posting limits should be ignored"));
        SkipMode = Config.Bind("QuickSellFlea", "Skip Mode", true,
            new ConfigDescription("Disables vanilla blinking effect when waiting for the callback from the server.\nEnabled by default to bypass a bug in the SPT server where aforementioned callback sometimes never triggers, leaving some items blinking infinitely"));
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
        if (Singleton<NotificationManager>.Instantiated)
        {
            NotificationManager.DisplayMessageNotification($"Currency set to {PostingCurrency.Value}",
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

public enum EPostingCurrency
{
    RUB = 1,
    USD = 120,
    EUR = 133
}

public struct PostPriceData(RagFair ragFair, InventoryController inventoryController,
        ItemContext itemContext, Handbook handbookClass, bool selectAll,
        SimpleContextMenuButton button)
{
    public readonly Item Item => ItemContext.Item;

    public readonly RagFair RagFair = ragFair;
    public readonly InventoryController InventoryController = inventoryController;
    public readonly ItemContext ItemContext = itemContext;
    public readonly Handbook HandbookClass = handbookClass;
    public readonly bool SelectAll = selectAll;
    public readonly Dictionary<Item, ItemAddress> OfferDict = [];
    public readonly SimpleContextMenuButton SimpleContextMenuButton = button;
    public List<Item> Items;

    public float AveragePrice;
}
