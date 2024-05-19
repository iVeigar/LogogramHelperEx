using System.Numerics;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using ImGuiNET;

namespace LogogramHelperEx.Util;

internal static class ImGuiUtils
{
    public static bool DrawIcon(uint iconId, float width)
    {
        if (ThreadLoadImageHandler.TryGetIconTextureWrap(iconId, false, out var texture))
        {
            ImGui.Image(texture.ImGuiHandle, ImGuiHelpers.ScaledVector2(width), Vector2.Zero, Vector2.One, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            return true;
        }
        return false;
    }
}
