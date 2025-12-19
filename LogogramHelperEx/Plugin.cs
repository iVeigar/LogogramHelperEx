using System;
using System.Collections.Generic;
using System.Linq;
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
using static ECommons.GenericHelpers;
using static LogogramHelperEx.UIHelpers.AddonMasterImplementations.AddonMaster;
using EzTaskManager = ECommons.Automation.LegacyTaskManager.TaskManager;
namespace LogogramHelperEx;

public sealed class Plugin : IDalamudPlugin
{
    public readonly Configuration Config;
    private readonly WindowSystem windowSystem = new("LogogramHelperEx");
    private readonly MainWindow mainWindow;
    private readonly LogosWindow logosWindow;
    private readonly EzTaskManager ezTaskManager;

    public readonly Dictionary<uint, List<uint>> Logograms; // 未鉴定的文理碎晶
    public readonly List<LogosActionInfo> LogosActions; // 文理技能
    public readonly Dictionary<uint, (int Index, string Name)> MagiciteItems; // 文理碎晶(28种)
    public readonly Dictionary<uint, int> MagiciteItemStock = []; // 文理碎晶余量
    public readonly Dictionary<uint, string> LogogramDescriptions; // 未鉴定的文理碎晶描述

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        EzConfig.Migrate<Configuration>();
        Config = EzConfig.Init<Configuration>();

        MagiciteItems = MagiciteItem.Load();
        Logograms = Logogram.Load();
        LogosActions = LogosActionInfo.Load();
        LogogramDescriptions = Logograms.ToDictionary(kvp => kvp.Key, kvp => $"\n\n可获得: {string.Join(", ", kvp.Value.Select(cid => MagiciteItems[cid].Name))}");

        ezTaskManager = new();
        mainWindow = new(this);
        logosWindow = new(this);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(logosWindow);

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", ItemDetailOnUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "EurekaMagiciteItemShardList", LogogramsStockOnUpdate);

        Svc.PluginInterface.UiBuilder.Draw += DrawUI;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", ItemDetailOnUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "EurekaMagiciteItemShardList", LogogramsStockOnUpdate);
        Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
        ECommonsMain.Dispose();
    }

    private unsafe void DrawUI()
    {
        if (TryGetAddonMaster<EurekaMagiciteItemShardList>(out var addon) && addon.IsAddonReady)
        {
            if (!mainWindow.IsOpen)
            {
                mainWindow.IsOpen = true;
                addon.All();
            }
        }
        else
        {
            if (mainWindow.IsOpen) mainWindow.IsOpen = false;
            if (logosWindow.IsOpen) logosWindow.IsOpen = false;
        }
        windowSystem.Draw();
    }

    public void DrawLogosDetailUI(LogosActionInfo action)
    {
        logosWindow.SetDetails(action);
        logosWindow.IsOpen = true;
    }

    private unsafe void ItemDetailOnUpdate(AddonEvent type, AddonArgs args)
    {
        var id = (uint)Svc.GameGui.HoveredItem;
        if (!LogogramDescriptions.TryGetValue(id, out var description))
            return;
        var arrayData = Framework.Instance()->UIModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
        var stringArrayData = arrayData.StringArrays[26];
        var seStr = GetTooltipString(stringArrayData, 13);
        if (seStr == null) return;

        if (!seStr.TextValue.Contains(description))
            seStr.Payloads.Insert(1, new TextPayload(description));

        stringArrayData->SetValue(13, seStr.Encode(), false, true, true);
    }

    private static unsafe SeString GetTooltipString(StringArrayData* stringArrayData, int field)
    {
        var stringAddress = new IntPtr(stringArrayData->StringArray[field]);
        return stringAddress != IntPtr.Zero ? MemoryHelper.ReadSeStringNullTerminated(stringAddress) : null;
    }

    private unsafe void LogogramsStockOnUpdate(AddonEvent type, AddonArgs args)
    {
        Svc.Log.Debug("AddonEvent.PreRequestedUpdate: LogogramsStockOnUpdate");
        MagiciteItemStock.Clear();
        var arrayData = Framework.Instance()->UIModule->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
        for (var i = 1; i <= arrayData.NumberArrays[137]->IntArray[0]; i++)
        {
            var stock = arrayData.NumberArrays[137]->IntArray[4 * i];
            var id = (uint)arrayData.NumberArrays[137]->IntArray[(4 * i) + 1];
            MagiciteItemStock[id] = stock;
        }
    }

    // 放入一个配方到一个空融合器中，优先会放入右边的星极融合器
    public unsafe void PutRecipe(List<(uint id, int quantity)> recipe)
    {
        ezTaskManager.Enqueue(() =>
        {
            if (recipe == null || recipe.Count == 0)
                return;
            if (!TryGetAddonMaster<EurekaMagiciteItemSynthesis>(out var addon) || !addon.IsAddonReady)
                return;
            var array = addon.AreArraysEmpty() switch
            {
                (_, true) => 0,
                (true, _) => 1,
                _ => -1
            };
            if (array == -1) return;
            foreach(var item in recipe)
                for (var i = 0; i < item.quantity; i++)
                    addon.PutMneme(array, MagiciteItems[item.id].Index);
        });
    }

    // 放入两个配方，会清空两个融合器后再放入。recipe1会放入左边的灵极融合器，recipe2会放入右边的星极融合器
    public unsafe void PutRecipes(List<(uint id, int quantity)> recipe1, List<(uint id, int quantity)> recipe2)
    {
        ClearArrays();
        ezTaskManager.Enqueue(() =>
        {
            if (!TryGetAddonMaster<EurekaMagiciteItemSynthesis>(out var addon) || !addon.IsAddonReady)
                return;

            if (recipe2 == null)
            {
                (recipe1, recipe2) = (recipe2, recipe1);
                if (recipe2 == null)
                    return;
            }

            if (recipe1 != null)
                foreach (var item in recipe1)
                    for (var i = 0; i < item.quantity; i++)
                        addon.PutMnemeIntoUmbralArray(MagiciteItems[item.id].Index);

            foreach (var item in recipe2)
                for (var i = 0; i < item.quantity; i++)
                    addon.PutMnemeIntoAstralArray(MagiciteItems[item.id].Index);
        });
    }

    public unsafe void Synthesis()
    {
        ezTaskManager.Enqueue(() =>
        {
            if (!TryGetAddonMaster<EurekaMagiciteItemSynthesis>(out var addon) || !addon.IsAddonReady)
                return;
            addon.Extract();
        });
    }

    public unsafe void ClearArrays()
    {
        ezTaskManager.Enqueue(() =>
        {
            if (!TryGetAddonMaster<EurekaMagiciteItemSynthesis>(out var addon) || !addon.IsAddonReady)
                return;
            addon.ClearArrays();
        });
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
