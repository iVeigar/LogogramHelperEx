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

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin { get; }
    private List<LogosAction> LogosActions { get; }

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
    private static float fontScaling => ImGuiHelpers.GlobalScaleSafe;

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
            if (ImGui.ImageButton(TextureManager.GetTex(action.IconID).ImGuiHandle, new Vector2(40, 40) * fontScaling, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f), padding, bg, tint))
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
        using (ImRaii.Disabled(!ImGui.GetIO().KeyCtrl))
        {
            if (ImGui.Button("删除当前展示的分类"))
            {
                groups.RemoveAt(currentActionSetGroupTab);
                save = true;
            }
        }
        ImGui.SameLine();
        if(ImGui.Checkbox("一键放入后自动提取记忆（慎用！）", ref autoSynthesis))
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
            ImGui.TableSetupColumn("图标", ImGuiTableColumnFlags.WidthFixed, 90 * fontScaling + 24);
            ImGui.TableSetupColumn("编辑");
            ImGui.TableSetupColumn("操作");
            ImGui.TableNextColumn();
            for (var i = 0; i < group.Sets.Count; i++)
            {
                save |= DrawActionSetEditer(group.Sets, i);
                if(i < group.Sets.Count - 1)
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

    private bool DrawActionSetEditer(List<ActionSet> actionSets, int actionSetIndex)
    {
        ImGui.PushID(actionSetIndex);
        var save = false;
        var (selectedActionIndex1, selectedRecipeIndex1) = actionSets[actionSetIndex].MagiaAction1;
        var (selectedActionIndex2, selectedRecipeIndex2) = actionSets[actionSetIndex].MagiaAction2;
        DrawActionSetIcons(selectedActionIndex1, selectedActionIndex2);
        ImGui.TableNextColumn();
        ImGui.PushID(1);
        if (DrawRecipeSelector(ref selectedActionIndex1, ref selectedRecipeIndex1))
        {
            actionSets[actionSetIndex].MagiaAction1 = (selectedActionIndex1, selectedRecipeIndex1);
            save = true;
        }
        ImGui.PopID();
        ImGui.PushID(2);
        if (DrawRecipeSelector(ref selectedActionIndex2, ref selectedRecipeIndex2))
        {
            actionSets[actionSetIndex].MagiaAction2 = (selectedActionIndex2, selectedRecipeIndex2);
            save = true;
        }
        ImGui.PopID();
        ImGui.TableNextColumn();
        save |= DrawActionSetOperators(actionSets, actionSetIndex);
        ImGui.PopID();
        return save;
    }

    private void DrawActionSetIcons(int action1, int action2)
    {
        if (action1 <= 0 && action2 <= 0)
            return;
        var bg = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        var tint = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        if (action1 <= 0 || action2 <= 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 1 + ImGui.GetContentRegionAvail().X / 2 - 22.5f * fontScaling);
            DrawIcon(action1 <= 0 ? action2 : action1, fontScaling, tint, bg);
        }
        else
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(cursor.X + 4);
            DrawIcon(action1, fontScaling, tint, bg);
            ImGui.SetCursorPosX(cursor.X + 45f * fontScaling + 16);
            ImGui.SetCursorPosY(cursor.Y);
            DrawIcon(action2, fontScaling, tint, bg);
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

    private string actionSetEditerFilter = "";
    private bool DrawRecipeSelector(ref int prevActionIndex, ref int prevRecipeIndex)
    {
        var action = LogosActions[prevActionIndex];
        var changed = false;
        
        ImGui.SetNextItemWidth(80.0f * fontScaling);
        if (ImGui.BeginCombo("###action", action.Name, ImGuiComboFlags.HeightLargest))
        {
            ImGui.SetNextItemWidth(70.0f * fontScaling);
            ImGui.InputTextWithHint("", "搜索", ref actionSetEditerFilter, 50, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.BeginChild("###LogogramHelperExComboChild", new Vector2(0, 105 * ImGuiHelpers.GlobalScale), true);
            var closePopup = false;
            for (var i = 0; i < LogosActions.Count; i++)
            {
                if (!LogosActions[i].Name.Contains(actionSetEditerFilter, StringComparison.CurrentCultureIgnoreCase))
                    continue;
                if (ImGui.Selectable(LogosActions[i].Name, prevActionIndex == i))
                {
                    if (prevActionIndex != i)
                    {
                        prevActionIndex = i;
                        changed = true;
                    }
                    closePopup = true;
                }
            }
            if (closePopup)
            {
                ImGui.CloseCurrentPopup();
                actionSetEditerFilter = "";
            }
            ImGui.EndChild();
            ImGui.EndCombo();
        }
        if (prevActionIndex == 0)
        {
            return changed;
        }

        ImGui.SameLine();
        if (changed)
        {
            action = LogosActions[prevActionIndex];
            prevRecipeIndex = 0;
        }
        (var preview_min, var preview_info) = Plugin.GetRecipeInfo(action.Recipes[prevRecipeIndex]);
        var preview = $"[{preview_min}] {preview_info}";
        ImGui.SetNextItemWidth(ImGui.CalcTextSize(preview).X + 28 * fontScaling);
        if (ImGui.BeginCombo("###recipe", preview))
        {
            for (var i = 0; i < action.Recipes.Count; i++)
            {
                (var min, var recipeString) = Plugin.GetRecipeInfo(action.Recipes[i]);

                if (ImGui.Selectable($"[{min}] {recipeString}", prevRecipeIndex == i))
                {
                    prevRecipeIndex = i;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }
    private bool DrawActionSetOperators(List<ActionSet> actionSets, int actionSetIndex)
    {
        var autoSynthesis = Plugin.Config.AutoSynthesis;

        var changed = false;
        var (selectedActionIndex1, selectedRecipeIndex1) = actionSets[actionSetIndex].MagiaAction1;
        var (selectedActionIndex2, selectedRecipeIndex2) = actionSets[actionSetIndex].MagiaAction2;
        using (ImRaii.Disabled(selectedActionIndex1 == 0 && selectedActionIndex2 == 0))
        {
            if (ImGui.Button("一键放入"))
            {
                if (selectedActionIndex2 != 0)
                    Plugin.PutRecipe(LogosActions[selectedActionIndex2].Recipes[selectedRecipeIndex2]);
                if (selectedActionIndex1 != 0)
                    Plugin.PutRecipe(LogosActions[selectedActionIndex1].Recipes[selectedRecipeIndex1]);
                if (autoSynthesis)
                    Plugin.Synthesis();
            }
        }
        var canDelete = ImGui.GetIO().KeyCtrl;
        using (ImRaii.Disabled(!canDelete))
        {
            if (ImGui.Button("删除"))
            {
                actionSets.RemoveAt(actionSetIndex);
                changed = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住CTRL");

        return changed;
    }
}
