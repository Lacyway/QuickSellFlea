using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using EFT.Utilities;
using QuickSellFlea.Utils;
using SPT.Reflection.Patching;
using UIFixesInterop;
using UnityEngine;

namespace QuickSellFlea.Patches;

internal class ItemUiContext_GetItemContextInteractions_Patch : ModulePatch
{
    private static PostPriceData _postPriceData;
    private static bool _priceReady;

    public static bool CanPost = true;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(ItemUiContext)
            .GetMethod(nameof(ItemUiContext.ShowContextMenu));
    }

    [PatchPostfix]
    public static void Postfix(ItemUiContext __instance,
        InventoryController ____inventoryController, Dictionary<EItemInfoButton, string> ____contextMenuCustomNames,
        ItemContext itemContext, ContextInteractions<EItemInfoButton> ____currentContextInteractions)
    {
        if (!CanPost)
        {
            return;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (InGameStatus.InRaid)
        {
            return;
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (____currentContextInteractions == null)
        {
            return;
        }

#if DEBUG
        Logger.LogInfo("Patch running");
#endif

        if (!Input.GetKey(CSF_Plugin.Hotkey.Value.MainKey))
        {
            return;
        }

        if (__instance.Session == null)
        {
#if DEBUG
            Logger.LogWarning("Session was null");
#endif
            return;
        }

        if (____inventoryController == null)
        {
#if DEBUG
            Logger.LogWarning("InventoryController was null");
#endif
            return;
        }

        if (__instance.Session.RagFair?.Available != true)
        {
#if DEBUG
            Logger.LogWarning("Ragfair was not available");
#endif
            return;
        }

        if (!__instance.CurrentItemContext.Item.CanSell())
        {
            return;
        }

        if (____currentContextInteractions is not BaseItemContextInteractions baseItemContextInteractions)
        {
#if DEBUG
            Logger.LogWarning($"Was not ContextInteractionsAbstractClass, was {____currentContextInteractions.GetType().Name}");
#endif
            return;
        }

        if (!____currentContextInteractions.AllInteractions.Contains(EItemInfoButton.AddOffer))
        {
#if DEBUG
            Logger.LogWarning("Does not contain add to flea, skipping");
#endif
            return;
        }

        if (itemContext.ViewType != EItemViewType.Inventory)
        {
#if DEBUG
            Logger.LogWarning($"Was not EItemViewType.Inventory, was {itemContext.ViewType}");
#endif
            return;
        }

        if (itemContext.Item.Parent.Container.ParentItem.TemplateId == "55d7217a4bdc2d86028b456d") // fix for UI Fixes
        {
            return;
        }

        var ragFair = __instance.Session.RagFair;
        if (ragFair?.Disabled != false)
        {
            return;
        }

        if (!CSF_Plugin.BypassLimit.Value && ragFair.MyOffersCount == ragFair.MaxOffersCount)
        {
            return;
        }

        if (MultiSelect.Count > 1)
        {
            MultiSell.HandleMultiSelectSell(ragFair, __instance, ____inventoryController);
            return;
        }

        var cont = (InteractionButtonsContainer)InteractionButtonsContainerHelper.InteractionButtonsContainerRef.GetValue(__instance.ContextMenu);
        var button = cont.GetButton(new("QUICKOFFER", "Fetching...",
            ClickQuickOffer, ResourcesCache.Pop<Sprite>("Characteristics/Icons/AddOffer")));

        _postPriceData = new PostPriceData(ragFair, ____inventoryController, itemContext,
            __instance.Handbook, Input.GetKey(KeyCode.LeftShift), button);

        ragFair._tradingSession.RagfairGetPrices(ReceivedPrices);
    }

    private static void ReceivedPrices(Result<Dictionary<string, float>> result)
    {
        _postPriceData.RagFair.CG_RefreshItemPrices(result);
        _postPriceData.RagFair.GetMarketPrices(_postPriceData.Item.TemplateId, SetPrices);
    }

    private static void HandlePostAddOffer()
    {
        if (!CSF_Plugin.SkipMode.Value)
        {
            foreach ((var item, var address) in _postPriceData.OfferDict)
            {
                address.RaiseRemoveEvent(item,
                    address.Equals(item.CurrentAddress) ? CommandStatus.Failed : CommandStatus.Succeed,
                    _postPriceData.InventoryController);
            }
        }

        _postPriceData = default;
        CanPost = true;
        _priceReady = false;
    }

    private static void SetPrices(ItemMarketPrices prices)
    {
#if DEBUG
        Logger.LogWarning($"Average was {prices.avg} roubles");
#endif
        _postPriceData.AveragePrice = prices.avg;
        if (_postPriceData.AveragePrice <= 0f)
        {
            return;
        }

        var averagePrice = _postPriceData.AveragePrice * _postPriceData.HandbookClass.StructuredItems[ConversionUtils.RoubleTpl].Data.Price;
#if DEBUG
        Logger.LogInfo($"Searching for posting price for {_postPriceData.Item.LocalizedShortName()}, with a stack amount of {_postPriceData.Item.StackObjectsCount}" +
            $" and requirementsPrice of {averagePrice}");
#endif

        if (_postPriceData.SelectAll)
        {
#if DEBUG
            Logger.LogInfo("Posting all of similar type");
#endif
            CompoundItem[] array =
            [
                _postPriceData.InventoryController.Inventory.Stash
            ];
            using RagfairNewOfferContext helper = new(array[0].Grids[0], _postPriceData.InventoryController);
            var item = _postPriceData.Item;
            _postPriceData.Items = [.. _postPriceData.Item.Parent.Container.Items.Where(i => i.Compare(_postPriceData.Item)
                && RagFair.CanBeSelectedAtRagfair(item, helper._itemController, out var error))
                .OrderBy(i =>
                {
                    if (i != item)
                    {
                        return 2;
                    }

                    return 1;
                })];

            if (!_postPriceData.Items.Any())
            {
                _postPriceData.Items.Add(item);
            }
        }
        else
        {
            _postPriceData.Items = [_postPriceData.Item];
        }

        var postPrice = 0f;
        var count = _postPriceData.Items.Sum(i => i.StackObjectsCount);
        if (CSF_Plugin.ShowListingPrice.Value)
        {
            postPrice = Mathf.CeilToInt(
                (float)PriceCalculator.CalculateTaxPrice(_postPriceData.Item, count,
                averagePrice, false)
            );
        }
        else
        {
            postPrice = _postPriceData.AveragePrice * count;

            if (CSF_Plugin.PostingCurrency.Value is not EPostingCurrency.RUB)
            {
                postPrice = MathF.Round(postPrice / (float)CSF_Plugin.PostingCurrency.Value, 0, MidpointRounding.AwayFromZero);
            }
        }

        var symbol = string.Empty;
        switch (CSF_Plugin.PostingCurrency.Value)
        {
            case EPostingCurrency.RUB:
                symbol = "₽";
                break;
            case EPostingCurrency.USD:
                symbol = "$";
                break;
            case EPostingCurrency.EUR:
                symbol = "€";
                break;
        }

#if DEBUG
        Logger.LogInfo($"Posting price was {postPrice}{symbol}, amount was {count} with {_postPriceData.Items.Count} stacks");
#endif
        var label = count > 1 ? $"[{_postPriceData.Items.Count}s, {count}x] {postPrice.FormatSeparate()}" : $"{postPrice.FormatSeparate()}";
        var button = _postPriceData.SimpleContextMenuButton;
        if (button != null)
        {
            button.SetText($"QUICK OFFER ({label} {symbol})");
            _priceReady = true;
        }
    }

    private static void ClickQuickOffer()
    {
        if (!_priceReady)
        {
            return;
        }

        CanPost = false;
        var toPost = _postPriceData.Items.Select(i => i.Id)
            .ToArray();

        for (var i = 0; i < _postPriceData.Items.Count; i++)
        {
            var item = _postPriceData.Items[i];
            _postPriceData.OfferDict.Add(item, item.Parent);
            if (!CSF_Plugin.SkipMode.Value)
            {
                _postPriceData.Item.Parent.RaiseRemoveEvent(item, CommandStatus.Begin, _postPriceData.InventoryController);
            }
        }

        BarterTemplate postData = null;
        switch (CSF_Plugin.PostingCurrency.Value)
        {
            case EPostingCurrency.RUB:
                postData = new()
                {
                    _tpl = ConversionUtils.RoubleTpl,
                    count = _postPriceData.AveragePrice
                };
                break;
            case EPostingCurrency.USD:
                postData = new()
                {
                    _tpl = ConversionUtils.DollarTpl,
                    count = ConversionUtils.ConvertToUSD(_postPriceData.AveragePrice)
                };
                break;
            case EPostingCurrency.EUR:
                postData = new()
                {
                    _tpl = ConversionUtils.EuroTpl,
                    count = ConversionUtils.ConvertToEUR(_postPriceData.AveragePrice)
                };
                break;
        }

        Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
        _postPriceData.RagFair.AddOffer(false, toPost, [postData], HandlePostAddOffer);
    }
}