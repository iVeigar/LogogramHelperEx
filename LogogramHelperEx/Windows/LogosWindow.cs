using System;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using LogogramHelperEx.Util;

namespace LogogramHelperEx.Windows;

public sealed class LogosWindow(Plugin plugin) : Window(
"技能详情", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize), IDisposable
{
    private Plugin Plugin { get; } = plugin;
    private LogosActionInfo Action { get; set; } = plugin.LogosActions[1];
    private IDalamudTextureWrap Texture { get; set; } = null!;
    public void Dispose()
    {
    }
    public void SetDetails(LogosActionInfo action)
    {
        Action = action;
        Texture = TextureManager.GetTex(action.IconID);
    }
    public override void Draw()
    {
        if (Texture == null)
            return;
        var fontScaling = ImGui.GetFontSize() / 17;
        ImGui.PushTextWrapPos(540.0f * fontScaling);
        ImGui.BeginGroup();
        ImGui.Image(Texture.ImGuiHandle, new Vector2(40, 40) * fontScaling, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.Text(Action.Name);
        ImGui.SameLine();
        ImGui.BeginGroup();
        Action.Roles.ForEach(role =>
        {
            var roleTexture = TextureManager.GetTex(role);
            ImGui.Image(roleTexture.ImGuiHandle, new Vector2(19, 19) * fontScaling, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f));
            ImGui.SameLine();
        });
        ImGui.EndGroup();
        var details = Action.Type.ToUpper();
        if (!Action.Cast.IsNullOrEmpty())
            details += $"    咏唱时间: {Action.Cast}";
        if (!Action.Recast.IsNullOrEmpty())
            details += $"    复唱时间: {Action.Recast}";
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), details);
        ImGui.EndGroup();
        ImGui.EndGroup();
        ImGui.Spacing();
        ImGui.TextUnformatted($"{Action.Description}");
        ImGui.Spacing();
        ImGui.Text("合成: (点击可一键放置)");
        ImGui.BeginChild($"combinations{Action.Name}", new Vector2(540.0f * fontScaling, (ImGui.GetFontSize() + 5) * Action.Recipes.Count), false, ImGuiWindowFlags.NoScrollbar);
        ImGui.Columns(2, "combinations", false);
        ImGui.SetColumnWidth(0, 40f);
        ImGui.SetColumnWidth(1, 300f * fontScaling);
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
        ImGui.EndChild();
        ImGui.PopTextWrapPos();
    }
}
