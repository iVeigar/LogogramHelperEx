using System;
using System.Collections.Generic;
using System.Linq;
using ClickLib.Clicks;
using ClickLib.Exceptions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LogogramHelperEx.Classes;
using LogogramHelperEx.Util;
using LogogramHelperEx.Windows;
using Lumina.Excel;
using EzTaskManager = ECommons.Automation.LegacyTaskManager.TaskManager;
namespace LogogramHelperEx;

public sealed class Plugin : IDalamudPlugin
{
    public WindowSystem WindowSystem = new("LogogramHelperEx");

    public MainWindow MainWindow { get; init; }

    public LogosWindow LogosWindow { get; init; }

    internal EzTaskManager EzTaskManager { get; init; }

    internal Dictionary<ulong, List<uint>> Logograms = null!; // 未鉴定的文理碎晶

    internal List<LogosActionInfo> LogosActions = null!; // 文理技能

    internal Dictionary<uint, (int Index, string Name)> MagiciteItems = null!; // 文理碎晶(28种)

    internal Dictionary<uint, int> MagiciteItemStock = []; // 文理碎晶余量

    internal Configuration Config;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        LoadData();
        EzTaskManager = new();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new();
        MainWindow = new(this);
        LogosWindow = new(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(LogosWindow);

        pluginInterface.UiBuilder.Draw += DrawUI;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", ItemDetailOnUpdate);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        TextureManager.Dispose();
        ECommonsMain.Dispose();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
        var addonPtr = Svc.GameGui.GetAddonByName("EurekaMagiciteItemShardList", 1);
        if (addonPtr != IntPtr.Zero)
        {
            if (!MainWindow.IsOpen)
            {
                MainWindow.IsOpen = true;
                var click = ClickEurekaMagiciteItemShardList.Using(addonPtr);
                click.SwitchCategory(0);
            }
            ObtainLogograms();
        }
        else
        {
            if (MainWindow.IsOpen) MainWindow.IsOpen = false;
            if (LogosWindow.IsOpen) LogosWindow.IsOpen = false;
        }
    }

    private void LoadData()
    {
        TextureManager.LoadIcon(786);
        MagiciteItems = MagiciteItem.Load();
        Logograms = Logogram.Load();
        LogosActions = LogosActionInfo.Load();
    }

    public void DrawLogosDetailUI(LogosActionInfo action)
    {
        LogosWindow.SetDetails(action);
        LogosWindow.IsOpen = true;
    }

    private unsafe void ItemDetailOnUpdate(AddonEvent type, AddonArgs args)
    {
        var id = Svc.GameGui.HoveredItem;
        if (Logograms.TryGetValue(id, out var contentsId))
        {
            var contentsName = new List<string>();
            contentsId.ForEach(Id =>
            {
                contentsName.Add(MagiciteItems[Id].Name);
            });

            var arrayData = Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            var stringArrayData = arrayData.StringArrays[26];
            var seStr = GetTooltipString(stringArrayData, 13);
            if (seStr == null) return;

            var insert = $"\n\n可获得: {string.Join(", ", [.. contentsName])}";
            if (!seStr.TextValue.Contains(insert)) seStr.Payloads.Insert(1, new TextPayload(insert));

            stringArrayData->SetValue(13, seStr.Encode(), false, true, true);
        }
    }

    private static unsafe SeString? GetTooltipString(StringArrayData* stringArrayData, int field)
    {
        var stringAddress = new IntPtr(stringArrayData->StringArray[field]);
        return stringAddress != IntPtr.Zero ? MemoryHelper.ReadSeStringNullTerminated(stringAddress) : null;
    }

    public static T? GetSheetRow<T>(uint row) where T : ExcelRow
    {
        return Svc.Data.Excel.GetSheet<T>()!.GetRow(row);
    }

    private unsafe void ObtainLogograms()
    {
        var arrayData = Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
        for (var i = 1; i <= arrayData.NumberArrays[135]->IntArray[0]; i++)
        {
            var id = (uint)arrayData.NumberArrays[135]->IntArray[(4 * i) + 1];
            var stock = arrayData.NumberArrays[135]->IntArray[4 * i];
            if (MagiciteItemStock.TryGetValue(id, out var oldValue))
            {
                if (oldValue != stock)
                    MagiciteItemStock[id] = stock;
            }
            else
            {
                MagiciteItemStock.Add(id, stock);
                continue;
            }

        }
    }

    public unsafe void PutRecipe(List<(uint id, int quantity)> recipe)
    {
        EzTaskManager.Enqueue(() =>
        {
            var which = IsEmptyArray() switch
            {
                (true, _) => 0,
                (_, true) => 1,
                _ => -1
            };
            if (which == -1) return;
            try
            {
                var clickSynthesis = new ClickEurekaMagiciteItemSynthesis();
                foreach (var item in recipe)
                {
                    for (var i = 0; i < item.quantity; i++)
                        clickSynthesis.Put(which, MagiciteItems[item.id].Index);
                }
            }
            catch (InvalidClickException)
            {
            }
        });
    }
    public unsafe void PutRecipes(List<(uint id, int quantity)>? recipe1, List<(uint id, int quantity)>? recipe2)
    {
        ClearArrays();
        EzTaskManager.Enqueue(() =>
        {
            try
            {
                var clickSynthesis = new ClickEurekaMagiciteItemSynthesis();
                if (recipe2 == null)
                    (recipe1, recipe2) = (recipe2, recipe1);
                if (recipe2 != null)
                {
                    foreach (var item in recipe2)
                    {
                        for (var i = 0; i < item.quantity; i++)
                            clickSynthesis.Put(0, MagiciteItems[item.id].Index);
                    }
                }
                if (recipe1 != null)
                {
                    foreach (var item in recipe1)
                    {
                        for (var i = 0; i < item.quantity; i++)
                            clickSynthesis.Put(1, MagiciteItems[item.id].Index);
                    }
                }
            }
            catch (InvalidClickException)
            {
            }
        });
    }

    public unsafe void Synthesis()
    {
        EzTaskManager.Enqueue(() =>
        {
            if (IsEmptyArray() is (_, true))
                return;
            try
            {
                new ClickEurekaMagiciteItemSynthesis().Synthesis();
            }
            catch (InvalidClickException)
            {
            }
        });
    }

    public unsafe void ClearArrays()
    {
        EzTaskManager.Enqueue(() =>
        {
            try
            {
                var clickSynthesis = new ClickEurekaMagiciteItemSynthesis();
                for (var i = 5; i >= 0; i--)
                    clickSynthesis.Retrieve(i);
            }
            catch (InvalidClickException)
            {
            }
        });
    }

    private unsafe bool IsArrayEmpty(AtkUnitBase* addon, int arrayNodeIndex)
    {
        var arrayNode = addon->UldManager.NodeList[arrayNodeIndex];
        if (!arrayNode->IsVisible)
            return false;
        var nodeList = arrayNode->GetComponent()->UldManager.NodeList;
        for (var i = 12; i >= 10; i--)
        {
            if (nodeList[i]->IsVisible)
            {
                return false;
            }
        }
        return true;
    }

    private unsafe (bool, bool) IsEmptyArray()
    {
        var addon = Svc.GameGui.GetAddonByName("EurekaMagiciteItemSynthesis");
        if (addon == IntPtr.Zero)
            return (false, false);
        return (IsArrayEmpty((AtkUnitBase*)addon, 16), IsArrayEmpty((AtkUnitBase*)addon, 17));
    }

    public (int, string) GetRecipeInfo(List<(uint id, int quantity)> recipe)
    {
        var total = new List<int>();
        var stockStrings = new List<string>();
        foreach (var (id, quantity) in recipe)
        {
            if (!MagiciteItemStock.TryGetValue(id, out var stock))
            {
                stock = 0;
                MagiciteItemStock.Add(id, 0);
            }
            total.Add(stock / quantity);
            for (var j = 0; j < quantity; j++)
            {
                stockStrings.Add($"{MagiciteItems[id].Name}({stock})");
            }
        }
        return (total.Min(), string.Join(" + ", stockStrings));
    }

    public int GetActionSetQuantity(List<(uint id, int quantity)>? recipe1, List<(uint id, int quantity)>? recipe2)
    {
        var dict1 = recipe1?.ToDictionary();
        var dict2 = recipe2?.ToDictionary();
        var dict = dict1 ?? dict2;
        if (dict == null)
            return 0;
        if (dict1 != null && dict2 != null)
        {
            foreach (var (id, quantity) in dict2)
            {
                if (dict.ContainsKey(id))
                    dict[id] += quantity;
                else
                    dict[id] = quantity;
            }
        }
        List<int> total = [200];
        foreach (var (id, quantity) in dict)
        {
            if (!MagiciteItemStock.TryGetValue(id, out var stock))
            {
                stock = 0;
                MagiciteItemStock.Add(id, 0);
            }
            total.Add(stock / quantity);
        }
        return total.Min();
    }
}
