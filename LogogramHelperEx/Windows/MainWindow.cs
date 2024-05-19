using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.ImGuiMethods;
using ImGuiNET;
using LogogramHelperEx.Util;

namespace LogogramHelperEx.Windows;

public sealed class MainWindow : Window
{
    private Plugin Plugin { get; }
    private List<LogosActionInfo> LogosActions { get; }
    public MainWindow(Plugin plugin) : base(
        "文理技能", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {

        Plugin = plugin;
        LogosActions = plugin.LogosActions;

        ShowCloseButton = false;
        SizeConstraints = new() { MinimumSize = new(440, 350) };
    }

    private string filter = "";

    public override void Draw()
    {
        using (ImRaii.TabBar("LogogramHelperEx"))
        {
            if (ImGui.BeginTabItem("文理技能"))
            {
                DrawMainTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("技能组"))
            {
                DrawPresetsTab();
                ImGui.EndTabItem();
            }
        }
    }

    private unsafe void DrawMainTab()
    {
        ImGui.SetNextItemWidth(400 * ImGuiHelpers.GlobalScaleSafe);
        ImGui.InputTextWithHint("", "搜索", ref filter, 50, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.SameLine();

        if (ImGuiComponents.IconButton("KoFi", FontAwesomeIcon.Coffee, new Vector4(1.0f, 0.35f, 0.37f, 1.0f)))
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/apetih", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Support me on Ko-Fi");

        for (var i = 1; i <= 56; i++)
        {
            var action = LogosActions[i];
            if (!ThreadLoadImageHandler.TryGetIconTextureWrap(action.IconID, false, out var texture))
                continue;
            var tint = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            if (!action.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                tint.W = 0.15f;
            if (ImGui.ImageButton(texture.ImGuiHandle, ImGuiHelpers.ScaledVector2(40f), Vector2.Zero, Vector2.One, 2, new Vector4(0.0f, 0.0f, 0.0f, 1.0f), tint))
                Plugin.DrawLogosDetailUI(action);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{action.Name}");
            if (i % 10 != 0)
                ImGui.SameLine();
        }
    }

    private int currentActionSetGroupTab = -1;
    private string newTabName = "";
    private void DrawPresetsTab()
    {
        var groups = Plugin.Config.Groups;
        var autoSynthesis = Plugin.Config.AutoSynthesis;
        var save = false;
        ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScaleSafe);
        ImGui.InputTextWithHint("", "输入新分类名称..", ref newTabName, 20);
        ImGui.SameLine();
        using (ImRaii.Disabled(newTabName.IsNullOrEmpty()))
        {
            if (ImGui.Button("创建新分类"))
            {
                groups.Add(new(newTabName));
                newTabName = "";
                save = true;
            }
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(groups.Count == 0 || currentActionSetGroupTab == -1 || !ImGui.GetIO().KeyCtrl))
        {
            if (ImGui.Button("删除当前分类"))
            {
                groups.RemoveAt(currentActionSetGroupTab);
                currentActionSetGroupTab = -1;
                save = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住CTRL");

        ImGui.SameLine();
        if (ImGui.Checkbox("一键放入后自动提取记忆（慎用！）", ref autoSynthesis))
        {
            save = true;
            Plugin.Config.AutoSynthesis = autoSynthesis;
        }
        ImGui.Spacing();
        using (ImRaii.TabBar("LogogramHelperExPresets"))
        {
            for (var i = 0; i < groups.Count; i++)
            {
                if (ImGui.BeginTabItem($"{groups[i].Name}"))
                {
                    currentActionSetGroupTab = i;
                    save |= DrawActionSetGroupTab(groups[i]);
                    ImGui.EndTabItem();
                }
            }
        }
        if (save)
            Plugin.Config.Save();
    }


    private bool DrawActionSetGroupTab(ActionSetGroup group)
    {
        var save = false;
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScaleSafe);
        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ImGuiHelpers.ScaledVector2(4f, 10f)))
        {
            using (ImRaii.Table($"{group.Name}", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, 114f * ImGuiHelpers.GlobalScaleSafe);
                ImGui.TableSetupColumn("编辑");
                ImGui.TableSetupColumn("操作");
                ImGui.TableNextColumn();
                for (var i = 0; i < group.Sets.Count; i++)
                {
                    using (ImRaii.PushId(i))
                    {
                        save |= DrawActionSetRow(group.Sets, i);
                    }
                    if (i < group.Sets.Count - 1)
                    {
                        ImGui.TableNextColumn();
                    }
                }
            }
        }
        ImGui.Spacing();
        if (ImGui.Button("+", ImGuiHelpers.ScaledVector2(150.0f, 0.0f)))
        {
            group.Sets.Add(new());
            save = true;
        }
        ImGui.Spacing();
        return save;
    }

    private bool DrawActionSetRow(List<ActionSet> actionSets, int index)
    {
        var save = false;
        var actionSet = actionSets[index];

        DrawActionSetIcons(actionSet);
        ImGui.TableNextColumn();

        using (ImRaii.PushId(1))
        {
            save |= DrawRecipeSelector(actionSet.ActionRecipe1);
        }
        using (ImRaii.PushId(2))
        {
            save |= DrawRecipeSelector(actionSet.ActionRecipe2);
        }
        ImGui.TableNextColumn();
        save |= DrawActionSetOperator(actionSets, index);
        return save;
    }

    private const float IconWidth = 45f;
    private void DrawActionSetIcons(ActionSet actionSet)
    {
        var action1 = actionSet.ActionRecipe1.ActionIndex;
        var action2 = actionSet.ActionRecipe2.ActionIndex;
        if (action1 <= 0 && action2 <= 0)
            return;
        if (action1 <= 0 || action2 <= 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - (IconWidth + 2) * ImGuiHelpers.GlobalScaleSafe) / 2);
            ImGuiUtils.DrawIcon(LogosActions[action1 <= 0 ? action2 : action1].IconID, IconWidth);
        }
        else
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(cursor.X + 4 * ImGuiHelpers.GlobalScaleSafe);
            ImGuiUtils.DrawIcon(LogosActions[action1].IconID, IconWidth);
            ImGui.SetCursorPosX(cursor.X + (IconWidth + 16) * ImGuiHelpers.GlobalScaleSafe);
            ImGui.SetCursorPosY(cursor.Y);
            ImGuiUtils.DrawIcon(LogosActions[action2].IconID, IconWidth);
        }
    }

    private string actionNameFilter = "";
    private bool DrawRecipeSelector(ActionRecipe currentActionRecipe)
    {
        var changed = false;
        var action = LogosActions[currentActionRecipe.ActionIndex];
        ImGui.SetNextItemWidth(80.0f * ImGuiHelpers.GlobalScaleSafe);
        if (ImGui.BeginCombo("##SelectAction", action.Name, ImGuiComboFlags.HeightLargest))
        {
            ImGui.SetNextItemWidth(70.0f * ImGuiHelpers.GlobalScaleSafe);
            ImGui.InputTextWithHint("", "搜索", ref actionNameFilter, 50, ImGuiInputTextFlags.AutoSelectAll);
            using (ImRaii.Child("##SelectActionChild", ImGuiHelpers.ScaledVector2(0f, 105f), true))
            {
                var closePopup = false;
                for (var i = 0; i < LogosActions.Count; i++)
                {
                    if (!LogosActions[i].Name.Contains(actionNameFilter, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                    if (ImGui.Selectable(LogosActions[i].Name, currentActionRecipe.ActionIndex == i))
                    {
                        currentActionRecipe.ActionIndex = i;
                        changed = true;
                        closePopup = true;
                    }
                }
                if (closePopup)
                {
                    ImGui.CloseCurrentPopup();
                    actionNameFilter = "";
                }
            }
            ImGui.EndCombo();
        }
        if (currentActionRecipe.ActionIndex == 0)
            return changed;

        ImGui.SameLine();
        if (changed)
        {
            action = LogosActions[currentActionRecipe.ActionIndex];
            currentActionRecipe.RecipeIndex = 0;
        }
        var (_, previewInfo) = Plugin.GetRecipeInfo(action.Recipes[currentActionRecipe.RecipeIndex]);
        ImGui.SetNextItemWidth(ImGui.CalcTextSize(previewInfo).X + 28f * ImGuiHelpers.GlobalScaleSafe);
        if (ImGui.BeginCombo("##SelectRecipe", previewInfo))
        {
            for (var i = 0; i < action.Recipes.Count; i++)
            {
                var (_, recipeInfo) = Plugin.GetRecipeInfo(action.Recipes[i]);

                if (ImGui.Selectable(recipeInfo, currentActionRecipe.RecipeIndex == i))
                {
                    currentActionRecipe.RecipeIndex = i;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    private bool DrawActionSetOperator(List<ActionSet> actionSets, int index)
    {
        var changed = false;
        var autoSynthesis = Plugin.Config.AutoSynthesis;
        var actionSet = actionSets[index];
        var actionRecipe1 = actionSet.ActionRecipe1;
        var actionRecipe2 = actionSet.ActionRecipe2;
        var action1 = LogosActions[actionRecipe1.ActionIndex];
        var action2 = LogosActions[actionRecipe2.ActionIndex];
        var recipe1 = action1.Recipes?[actionRecipe1.RecipeIndex];
        var recipe2 = action2.Recipes?[actionRecipe2.RecipeIndex];
        var amount = Plugin.GetActionSetQuantity(recipe1, recipe2);
        var w = ImGui.CalcTextSize("(剩余200)    ").X;
        using (ImRaii.Disabled(recipe1 == null && recipe2 == null || amount == 0))
        {
            if (ImGui.Button($" 一键放入\n(剩余{amount})", new Vector2(w, 45f) * ImGuiHelpers.GlobalScaleSafe))
            {
                Plugin.PutRecipes(recipe1, recipe2);
                if (autoSynthesis)
                    Plugin.Synthesis();
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("融合器里已有的碎晶会被自动取回");

        ImGui.SameLine();
        var canDelete = ImGui.GetIO().KeyCtrl;
        using (ImRaii.Disabled(!canDelete))
        {
            if (ImGui.Button("删除", new Vector2(45f, 45f) * ImGuiHelpers.GlobalScaleSafe))
            {
                actionSets.RemoveAt(index);
                changed = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住CTRL");

        return changed;
    }
}
