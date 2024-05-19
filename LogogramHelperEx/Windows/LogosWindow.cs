using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ImGuiNET;
using LogogramHelperEx.Util;

namespace LogogramHelperEx.Windows;

public sealed class LogosWindow(Plugin plugin) : Window(
    "技能详情", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
{
    private Plugin Plugin { get; } = plugin;
    private LogosActionInfo Action { get; set; } = plugin.LogosActions[1];
    public void SetDetails(LogosActionInfo action)
    {
        Action = action;
    }
    public override void Draw()
    {
        using (ImRaii.Group())
        {
            if (ImGuiUtils.DrawIcon(Action.IconID, 40f))
                ImGui.SameLine();
            using (ImRaii.Group())
            {
                using (ImRaii.Group())
                {
                    ImGui.Text(Action.Name);
                    ImGui.SameLine();

                    Action.Roles.ForEach(role =>
                    {
                        if (ImGuiUtils.DrawIcon(role, 19f))
                            ImGui.SameLine();
                    });
                }

                var details = Action.Type.ToUpper();
                if (!Action.Cast.IsNullOrEmpty())
                    details += $"    咏唱时间: {Action.Cast}";
                if (!Action.Recast.IsNullOrEmpty())
                    details += $"    复唱时间: {Action.Recast}";
                ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), details);
            }
        }
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"{Action.Description}");
        ImGui.Spacing();
        ImGui.Text("合成方式: (点击可一键放置)");
        using (ImRaii.Child($"combinations{Action.Name}", new Vector2(0f, (ImGui.GetFontSize() + 5) * Action.Recipes.Count), false, ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.Columns(2, null, false);
            ImGui.SetColumnWidth(0, 40f * ImGuiHelpers.GlobalScaleSafe);
            ImGui.SetColumnWidth(1, 300f * ImGuiHelpers.GlobalScaleSafe);
            foreach (var recipe in Action.Recipes)
            {
                (var min, var recipeString) = Plugin.GetRecipeInfo(recipe);
                if (min > 0)
                    ImGui.Text($"{min}");
                else
                    ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "0");
                ImGui.NextColumn();
                using (ImRaii.Disabled(min == 0))
                {
                    if (ImGui.SmallButton(recipeString))
                    {
                        Plugin.PutRecipe(recipe);
                    }
                }
                ImGui.NextColumn();
            }
        }
    }
}
