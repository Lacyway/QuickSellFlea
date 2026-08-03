using System.Reflection;
using Comfort.Common;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace QuickSellFlea;

internal static class InteractionButtonsContainerHelper
{
    public static readonly FieldInfo InteractionButtonsContainerRef = AccessTools.Field(typeof(SimpleContextMenu), "_interactionButtonsContainer");

    private static readonly FieldInfo _buttonsTemplateRef = AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonTemplate");
    private static readonly FieldInfo _buttonsContainerRef = AccessTools.Field(typeof(InteractionButtonsContainer), "_buttonsContainer");
    private static readonly FieldInfo _tmpTextRef = AccessTools.Field(typeof(ContextMenuButton), "_text");

    public static SimpleContextMenuButton GetButton(this InteractionButtonsContainer container, DynamicContextInteraction interaction)
    {
        var simpleContextMenuButton = container.CreateContextButton(interaction.Key, interaction.Key,
            (SimpleContextMenuButton)_buttonsTemplateRef.GetValue(container), (RectTransform)_buttonsContainerRef.GetValue(container),
            interaction.Icon, new Action(interaction.Execute), container.CloseSubMenu, false, true);
        simpleContextMenuButton.SetButtonInteraction(SuccessfulResult.New);
        container.BindButton(simpleContextMenuButton);

        return simpleContextMenuButton;
    }

    public static void SetText(this SimpleContextMenuButton button, string text)
    {
        ((TextMeshProUGUI)_tmpTextRef.GetValue(button)).text = text;
    }
}
