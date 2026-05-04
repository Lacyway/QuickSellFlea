using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using QuickSellFlea.Patches;
using UIFixesInterop;
using UnityEngine;

namespace QuickSellFlea;

internal static class MultiSell
{
    public static bool CanFetch = true;

    private static RagFairClass _ragFair;
    private static InventoryController _inventoryController;
    private static InteractionButtonsContainer _interactionButtonsContainer;
    private static SimpleContextMenuButton _simpleContextMenuButton;

    private static readonly List<Item> _itemsToSell = [];
    private static readonly Dictionary<string, PriceData> _prices = [];
    private static int _receivedPrices;
    private static int _posted;
    private static bool _priceReady;
    private static readonly Dictionary<Item, ItemAddress> _offerDict = [];

    private static void Reset()
    {
        _ragFair = null;
        _inventoryController = null;
        _interactionButtonsContainer = null;
        _simpleContextMenuButton = null;

        _itemsToSell.Clear();
        _prices.Clear();
        _receivedPrices = 0;
        _posted = 0;
        _priceReady = false;

        ItemUiContext_GetItemContextInteractions_Patch.CanPost = true;
    }

    internal static void HandleMultiSelectSell(RagFairClass ragFair, ItemUiContext instance, InventoryController inventoryController_0)
    {
        Reset();

        CanFetch = false;

        _ragFair = ragFair;
        _inventoryController = inventoryController_0;

        _interactionButtonsContainer = (InteractionButtonsContainer)InteractionButtonsContainerHelper.InteractionButtonsContainerRef.GetValue(instance.ContextMenu);
        _simpleContextMenuButton = _interactionButtonsContainer.GetButton(new("QUICKOFFER", "Fetching...",
            ClickQuickOffer, CacheResourcesPopAbstractClass.Pop<Sprite>("Characteristics/Icons/AddOffer")));

        _itemsToSell.AddRange(MultiSelect.Items.Where(ItemUiContext_GetItemContextInteractions_Patch.CanSell));

        var availablePosts = _ragFair.MaxOffersCount - _ragFair.MyOffersCount;
        if (_itemsToSell.Count > availablePosts)
        {
#if DEBUG
            CSF_Plugin.CSF_Logger.LogWarning($"Tried to post {_itemsToSell.Count} items but only {availablePosts} were available to be posted");
#endif
            var startIdx = Math.Max(0, availablePosts);
            var countToRemove = _itemsToSell.Count - startIdx;

            _itemsToSell.RemoveRange(startIdx, countToRemove);
        }

        ragFair.ISession.RagfairGetPrices(ReceivedPrices);
    }

    private static void ReceivedPrices(Result<Dictionary<string, float>> result)
    {
        if (_ragFair == null)
        {
            throw new NullReferenceException("RagFair was null?");
        }

        _ragFair.method_35(result);

        for (var i = 0; i < _itemsToSell.Count; i++)
        {
            var item = _itemsToSell[i];
            var priceData = new PriceData();
            _prices.Add(item.Id, priceData);
            _ragFair.GetMarketPrices(item.TemplateId, priceData.SetPrice);
        }
    }

    private static void SetPrices()
    {
        CanFetch = true;

        var total = 0f;
        for (var i = 0; i < _itemsToSell.Count; i++)
        {
            var item = _itemsToSell[i];
            var count = item.StackObjectsCount;

            if (_prices.TryGetValue(item.Id, out var priceData))
            {
                var price = count * priceData.Average;
                if (CSF_Plugin.PostingCurrency.Value is not EPostingCurrency.RUB)
                {
                    price = MathF.Round(price / (float)CSF_Plugin.PostingCurrency.Value, 0, MidpointRounding.AwayFromZero);
                }
                total += price;
            }
            else
            {
                throw new Exception("Missing price data for an item");
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
        CSF_Plugin.CSF_Logger.LogInfo($"Posting price was {total}{symbol}");
#endif
        var label = total.FormatSeparate();

        if (_simpleContextMenuButton != null)
        {
            _simpleContextMenuButton.SetText($"QUICK OFFER ({label} {symbol})");
            _priceReady = true;
        }
    }

    private static void ClickQuickOffer()
    {
        if (!_priceReady)
        {
            return;
        }

        ItemUiContext_GetItemContextInteractions_Patch.CanPost = false;
        foreach (var item in _itemsToSell)
        {
            if (!_prices.TryGetValue(item.Id, out var data))
            {
                throw new Exception("Price was missing");
            }

            GClass2335 postData = null;
            switch (CSF_Plugin.PostingCurrency.Value)
            {
                case EPostingCurrency.RUB:
                    postData = new()
                    {
                        _tpl = ConversionUtils.RoubleTpl,
                        count = data.Average
                    };
                    break;
                case EPostingCurrency.USD:
                    postData = new()
                    {
                        _tpl = ConversionUtils.DollarTpl,
                        count = ConversionUtils.ConvertToUSD(data.Average)
                    };
                    break;
                case EPostingCurrency.EUR:
                    postData = new()
                    {
                        _tpl = ConversionUtils.EuroTpl,
                        count = ConversionUtils.ConvertToEUR(data.Average)
                    };
                    break;
            }

            _offerDict.Add(item, item.Parent);
            item.Parent.RaiseRemoveEvent(item, CommandStatus.Begin, _inventoryController);
            _ragFair.AddOffer(false, [item.Id], [postData], HandlePostAddOffer);
        }

        Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
    }

    private static void HandlePostAddOffer()
    {
        _posted++;
        if (_posted >= _itemsToSell.Count)
        {
#if DEBUG
            CSF_Plugin.CSF_Logger.LogInfo("All items posted");
#endif
            foreach ((var item, var address) in _offerDict)
            {
                address.RaiseRemoveEvent(item,
                    address.Equals(item.CurrentAddress) ? CommandStatus.Failed : CommandStatus.Succeed,
                    _inventoryController);
            }

            Reset();
            return;
        }

#if DEBUG
        CSF_Plugin.CSF_Logger.LogInfo($"Still waiting, posted {_posted}/{_itemsToSell.Count}");
#endif
    }

    private sealed class PriceData
    {
        public float Average;

        public void SetPrice(ItemMarketPrices prices)
        {
            _receivedPrices++;
            Average = prices.avg;
#if DEBUG
            CSF_Plugin.CSF_Logger.LogInfo($"Received prices {_receivedPrices}/{_itemsToSell.Count}");
#endif
            if (_receivedPrices >= _itemsToSell.Count)
            {
                SetPrices();
            }
        }
    }
}

