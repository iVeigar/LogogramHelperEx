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
using ECommons.Configuration;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LogogramHelperEx.Classes;
using LogogramHelperEx.Windows;
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

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        LoadData();
        EzTaskManager = new();
        EzConfig.Migrate<Configuration>();
        Config = EzConfig.Init<Configuration>();

        MainWindow = new(this);
        LogosWindow = new(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(LogosWindow);

        Svc.PluginInterface.UiBuilder.Draw += DrawUI;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", ItemDetailOnUpdate);
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
    }

    private unsafe void DrawUI()
    {
        WindowSystem.Draw();
        if (GenericHelpers.TryGetAddonByName("EurekaMagiciteItemShardList", out AtkUnitBase* addon) && addon != null)
        {
            if (!MainWindow.IsOpen)
            {
                MainWindow.IsOpen = true;
                ClickEurekaMagiciteItemShardList.Using((nint)addon).SwitchCategory(0);
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

            var arrayData = Framework.Instance()->UIModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
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

    private unsafe void ObtainLogograms()
    {
        var arrayData = Framework.Instance()->UIModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
        for (var i = 1; i <= arrayData.NumberArrays[136]->IntArray[0]; i++)
        {
            var id = (uint)arrayData.NumberArrays[136]->IntArray[(4 * i) + 1];
            var stock = arrayData.NumberArrays[136]->IntArray[4 * i];
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
                (_, true) => 0,
                (true, _) => 1,
                _ => -1
            };
            if (which == -1) return;
            try
            {
                var clickSynthesis = new ClickEurekaMagiciteItemSynthesis();
                recipe?.Each(item =>
                {
                    for (var i = 0; i < item.quantity; i++)
                        clickSynthesis.Put(which, MagiciteItems[item.id].Index);
                });
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
                recipe2?.Each(item =>
                {
                    for (var i = 0; i < item.quantity; i++)
                        clickSynthesis.Put(0, MagiciteItems[item.id].Index);
                });
                recipe1?.Each(item =>
                {
                    for (var i = 0; i < item.quantity; i++)
                        clickSynthesis.Put(1, MagiciteItems[item.id].Index);
                });
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
        if (!arrayNode->IsVisible())
            return false;
        var nodeList = arrayNode->GetComponent()->UldManager.NodeList;
        for (var i = 12; i >= 10; i--)
        {
            if (nodeList[i]->IsVisible())
            {
                return false;
            }
        }
        return true;
    }

    private unsafe (bool, bool) IsEmptyArray()
    {
        if (GenericHelpers.TryGetAddonByName("EurekaMagiciteItemSynthesis", out AtkUnitBase* addon))
            return (IsArrayEmpty(addon, 17), IsArrayEmpty(addon, 16));
        return (false, false);
    }

    public (int, string) GetRecipeInfo(List<(uint id, int quantity)> recipe)
    {
        var total = new List<int>();
        var stockStrings = new List<string>();
        foreach (var (id, quantity) in recipe)
        {
            var stock = MagiciteItemStock.GetOrCreate(id);
            total.Add(stock / quantity);
            stockStrings.AddRange(Enumerable.Repeat($"{MagiciteItems[id].Name}({stock})", quantity));
        }
        return (total.Min(), stockStrings.Join(" + "));
    }

    public int GetActionSetQuantity(List<(uint id, int quantity)>? recipe1, List<(uint id, int quantity)>? recipe2)
    {
        var demand = new Dictionary<uint, int>();
        (recipe1 ?? []).Concat(recipe2 ?? [])?.Each(item => demand.IncrementOrSet(item.id, item.quantity));
        return demand.Count != 0 ? demand.Select(kv => MagiciteItemStock.GetOrCreate(kv.Key) / kv.Value).Min() : 0;
    }
}
