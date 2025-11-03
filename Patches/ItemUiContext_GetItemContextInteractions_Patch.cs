using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace QuickSellFlea.Patches;

internal class ItemUiContext_GetItemContextInteractions_Patch : ModulePatch
{
    private static PostPriceData _postPriceData;
    private const string _roubleTpl = "5449016a4bdc2d6f028b456f";
    private static bool _canPost = true;


    protected override MethodBase GetTargetMethod()
    {
        return typeof(ItemUiContext)
            .GetMethod(nameof(ItemUiContext.ShowContextMenu));
    }

    [PatchPostfix]
    public static void Postfix(ItemUiContext __instance,
        InventoryController ___inventoryController_0, Dictionary<EItemInfoButton, string> ___dictionary_0,
        ItemContextAbstractClass itemContext, ItemInfoInteractionsAbstractClass<EItemInfoButton> ___gclass3753_0)
    {
        if (!_canPost)
        {
            return;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (GClass2340.InRaid)
        {
            return;
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (___gclass3753_0 == null)
        {
            return;
        }

#if DEBUG
        Logger.LogInfo("Patch running");
#endif

        if (!Input.GetKey(KeyCode.LeftControl))
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

        if (___inventoryController_0 == null)
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

        if (___gclass3753_0 is not ContextInteractionsAbstractClass gclass)
        {
#if DEBUG
            Logger.LogWarning($"Was not ContextInteractionsAbstractClass, was {___gclass3753_0.GetType().Name}");
#endif
            return;
        }

        if (!___gclass3753_0.AllInteractions.Contains(EItemInfoButton.AddOffer))
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

        if (!itemContext.Item.CanSellOnRagfair)
        {
            return;
        }

        var parentItems = itemContext.Item.GetAllParentItems();
        if (parentItems.Any(i => i is InventoryEquipment))
        {
            return;
        }

        if (itemContext.Item.IsNotEmpty())
        {
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

        if (ragFair.MyOffersCount == ragFair.MaxOffersCount)
        {
            return;
        }

        _postPriceData = new(ragFair, gclass,
            __instance, ___gclass3753_0,
            ___dictionary_0, ___inventoryController_0, itemContext,
            __instance.HandbookClass, Input.GetKey(KeyCode.LeftShift));

        ragFair.ISession.RagfairGetPrices(ReceivedPrices);
    }

    private static void ReceivedPrices(Result<Dictionary<string, float>> result)
    {
        _postPriceData.RagFair.method_35(result);
        _postPriceData.RagFair.GetMarketPrices(_postPriceData.Item.TemplateId, SetPrices);
    }

    private static void HandlePostAddOffer()
    {
        foreach ((var item, var address) in _postPriceData.OfferDict)
        {
            address.RaiseRemoveEvent(item,
                address.Equals(item.CurrentAddress) ? CommandStatus.Failed : CommandStatus.Succeed,
                _postPriceData.InventoryController);
        }

        _postPriceData = default;
        _canPost = true;
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

        var averagePrice = _postPriceData.AveragePrice * _postPriceData.HandbookClass.StructuredItems[_roubleTpl].Data.Price;
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
            using RagfairOfferSellHelperClass helper = new(array[0].Grids[0], _postPriceData.InventoryController);
            var item = _postPriceData.Item;
            _postPriceData.Items = [.. _postPriceData.Item.Parent.Container.Items.Where(i => i.Compare(_postPriceData.Item)
                && RagFairClass.CanBeSelectedAtRagfair(item, helper.TraderControllerClass, out var error))
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
                (float)FleaTaxCalculatorAbstractClass.CalculateTaxPrice(_postPriceData.Item, count,
                averagePrice, false)
            );
        }
        else
        {
            postPrice = _postPriceData.AveragePrice * count;
        }

#if DEBUG
        Logger.LogInfo($"Posting price was {postPrice} roubles, amount was {count} with {_postPriceData.Items.Count} stacks");
#endif
        var label = count > 1 ? $"[{_postPriceData.Items.Count}s, {count}x] {postPrice.FormatSeparate()}" : $"{postPrice.FormatSeparate()}";
        var dynamicInteractions = _postPriceData.InteractionsClass.Dictionary_0 ?? [];
        dynamicInteractions[$"QUICK OFFER ({label} ₽)"] = new("QUICKOFFER", $"QUICK OFFER ({label} ₽)",
            ClickQuickOffer, CacheResourcesPopAbstractClass.Pop<Sprite>("Characteristics/Icons/AddOffer"));

        _postPriceData.ItemUiContext.ContextMenu.Show(_postPriceData.ItemUiContext.ContextMenu.transform.position,
            _postPriceData.InteractionsClass, _postPriceData.ItemInfoDict, _postPriceData.Item);
    }

    private static void ClickQuickOffer()
    {
        _canPost = false;
        var toPost = _postPriceData.Items.Select(i => i.Id)
            .ToArray();

        for (var i = 0; i < _postPriceData.Items.Count; i++)
        {
            var item = _postPriceData.Items[i];
            _postPriceData.OfferDict.Add(item, item.Parent);
            _postPriceData.Item.Parent.RaiseRemoveEvent(item, CommandStatus.Begin, _postPriceData.InventoryController);
        }
        
        Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
        _postPriceData.RagFair.AddOffer(false, toPost, [
                new()
                {
                    _tpl = "5449016a4bdc2d6f028b456f",
                    count = _postPriceData.AveragePrice
                }
            ],
            HandlePostAddOffer);
    }
}