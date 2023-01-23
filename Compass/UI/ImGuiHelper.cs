using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;

namespace Compass.UI;

public static class ImGuiHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ImageRotated(nint texId, Vector2 center, Vector2 size, float angle, Vector2 uv, Vector2 uv1, ImDrawListPtr drawList)
    {
        var cosA = MathF.Cos(angle);
        var sinA = MathF.Sin(angle);
        drawList.AddImageQuad(
            texId,
            center + Util.Rotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
            center + Util.Rotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
            center + Util.Rotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
            center + Util.Rotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
            new Vector2(uv.X, uv.Y),
            new Vector2(uv1.X, uv.Y),
            new Vector2(uv1.X, uv1.Y),
            new Vector2(uv.X, uv1.Y)
        );
    }

    internal static bool DrawTreeCheckbox(string label, ref bool open, Func<bool> drawConfig)
    {
        var changed = ImGui.Checkbox($"###{label.GetHashCode()}", ref open);
        ImGui.SameLine();
        if (open)
        {
            if (!ImGui.TreeNodeEx(label)) return changed;
            changed |= drawConfig();
            ImGui.TreePop();
        } else
        {
            DummyTreeNode(label);
        }

        return changed;
    }

    private static void DummyTreeNode(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen)
    {
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
        ImGui.TreeNodeEx(label, flags);
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }
}