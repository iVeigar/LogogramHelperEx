using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using LogogramHelperEx.Util;

namespace LogogramHelperEx.Windows;

public sealed class MainWindow : Window, IDisposable
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

    public void Dispose()
    {
    }

    private string filter = "";
    private static float fontScale => ImGuiHelpers.GlobalScaleSafe;

    public override void Draw()
    {
        if (ImGui.BeginTabBar("LogogramHelperEx"))
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
            ImGui.EndTabBar();
        }
    }

    private unsafe void DrawMainTab()
    {
        ImGui.PushItemWidth(400);
        ImGui.InputTextWithHint("", "搜索", ref filter, 50, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.PopItemWidth();

        ImGui.SameLine();

        if (ImGuiComponents.IconButton("KoFi", FontAwesomeIcon.Coffee, new Vector4(1.0f, 0.35f, 0.37f, 1.0f)))
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/apetih", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Support me on Ko-Fi");


        for (var i = 1; i <= 56; i++)
        {
            var action = LogosActions[i];
            var padding = 2;
            var bg = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            var tint = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            if (!action.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                tint.W = 0.15f;
            if (ImGui.ImageButton(TextureManager.GetTex(action.IconID).ImGuiHandle, new Vector2(40, 40) * fontScale, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f), padding, bg, tint))
            {
                Plugin.DrawLogosDetailUI(action);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{action.Name}");
            if (i % 10 != 0) ImGui.SameLine();
        }
    }
    private int currentActionSetGroupTab = -1;
    private string newTabName = "";
    private void DrawPresetsTab()
    {
        var groups = Plugin.Config.Groups;
        var autoSynthesis = Plugin.Config.AutoSynthesis;
        var save = false;
        ImGui.SetNextItemWidth(120f);
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
        ImGui.SameLine();
        if (ImGui.Checkbox("一键放入后自动提取记忆（慎用！）", ref autoSynthesis))
        {
            save = true;
            Plugin.Config.AutoSynthesis = autoSynthesis;
        }
        ImGui.Spacing();
        if (ImGui.BeginTabBar("LogogramHelperExPresets"))
        {
            for (var i = 0; i < groups.Count; i++)
            {
                ImGui.PushID(i);
                if (ImGui.BeginTabItem($"{groups[i].Name}"))
                {
                    currentActionSetGroupTab = i;
                    save |= DrawActionSetGroupTab(groups[i]);
                    ImGui.EndTabItem();
                }
                ImGui.PopID();
            }
            ImGui.EndTabBar();
        }
        if (save)
        {
            Plugin.Config.Save();
        }
    }


    private bool DrawActionSetGroupTab(ActionSetGroup group)
    {
        ImGui.PushID(group.Name);
        var save = false;
        ImGui.SetNextItemWidth(150f);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(4, 10));
        if (ImGui.BeginTable($"{group.Name}", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, 90 * fontScale + 24);
            ImGui.TableSetupColumn("编辑");
            ImGui.TableSetupColumn("操作");
            ImGui.TableNextColumn();
            for (var i = 0; i < group.Sets.Count; i++)
            {
                ImGui.PushID(i);
                save |= DrawActionSetRow(group.Sets, i);
                ImGui.PopID();
                if (i < group.Sets.Count - 1)
                {
                    ImGui.TableNextColumn();
                }
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
        ImGui.Spacing();
        if (ImGui.Button("+", new(150.0f, 0.0f)))
        {
            group.Sets.Add(new());
            save = true;
        }
        ImGui.PopID();
        ImGui.Spacing();
        return save;
    }

    private bool DrawActionSetRow(List<ActionSet> actionSets, int index)
    {
        var save = false;
        var actionSet = actionSets[index];

        DrawActionSetIcons(actionSet);
        ImGui.TableNextColumn();

        ImGui.PushID(1);
        save |= DrawRecipeSelector(actionSet.ActionRecipe1);
        ImGui.PopID();
        ImGui.PushID(2);
        save |= DrawRecipeSelector(actionSet.ActionRecipe2);
        ImGui.PopID();
        ImGui.TableNextColumn();

        save |= DrawActionSetOperator(actionSets, index);
        return save;
    }

    private void DrawActionSetIcons(ActionSet actionSet)
    {
        var action1 = actionSet.ActionRecipe1.ActionIndex;
        var action2 = actionSet.ActionRecipe2.ActionIndex;
        if (action1 <= 0 && action2 <= 0)
            return;
        var bg = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        var tint = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        if (action1 <= 0 || action2 <= 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 1 + ImGui.GetContentRegionAvail().X / 2 - 22.5f * fontScale);
            DrawIcon(action1 <= 0 ? action2 : action1, fontScale, tint, bg);
        }
        else
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(cursor.X + 4);
            DrawIcon(action1, fontScale, tint, bg);
            ImGui.SetCursorPosX(cursor.X + 45f * fontScale + 16);
            ImGui.SetCursorPosY(cursor.Y);
            DrawIcon(action2, fontScale, tint, bg);
        }
    }

    private void DrawIcon(int actionIndex, float fontScaling, Vector4 tint, Vector4 bg)
    {
        if (actionIndex > 0)
        {
            var action = LogosActions[actionIndex];
            ImGui.Image(TextureManager.GetTex(action.IconID).ImGuiHandle, new Vector2(45, 45) * fontScaling, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f), tint, bg);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{action.Name}");
        }
    }

    private string actionNameFilter = "";
    private bool DrawRecipeSelector(ActionRecipe currentActionRecipe)
    {
        var changed = false;
        var action = LogosActions[currentActionRecipe.ActionIndex];
        ImGui.SetNextItemWidth(80.0f * fontScale);
        if (ImGui.BeginCombo("##SelectAction", action.Name, ImGuiComboFlags.HeightLargest))
        {
            ImGui.SetNextItemWidth(70.0f * fontScale);
            ImGui.InputTextWithHint("", "搜索", ref actionNameFilter, 50, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.BeginChild("##SelectActionChild", new Vector2(0, 105 * ImGuiHelpers.GlobalScale), true);
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
            ImGui.EndChild();
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
        ImGui.SetNextItemWidth(ImGui.CalcTextSize(previewInfo).X + 28 * fontScale);
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
            if (ImGui.Button($" 一键放入\n(剩余{amount})", new Vector2(w, 45f) * fontScale))
            {
                if (recipe2 != null)
                    Plugin.PutRecipe(recipe2);
                if (recipe1 != null)
                    Plugin.PutRecipe(recipe1);
                if (autoSynthesis)
                    Plugin.Synthesis();
            }
        }
        ImGui.SameLine();
        var canDelete = ImGui.GetIO().KeyCtrl;
        using (ImRaii.Disabled(!canDelete))
        {
            if (ImGui.Button("删除", new Vector2(45f, 45f) * fontScale))
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
