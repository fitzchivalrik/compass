using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;

namespace Compass
{
    public partial class Compass
    {
        
        
        #region Math related methods
        
             [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe (Vector2 pMin, Vector2 pMax, uint tintColour, bool inArea) CalculateAreaCirlceVariables(
            in Vector2 playerPos, Vector2 playerForward, AtkComponentNode* mapIconComponentNode,
            AtkImageNode* imgNode, float mapScale, float compassUnit, float halfWidth32, Vector2 compassCentre,
            float maxDistance = 180f, float distanceOffset = 40f, float lowestScaleFactor = 0.2f)
        {
            // TODO Distinguish between Circles for quests and circles for Fates (colour?) for filtering
            // NOTE (Chiv) Remember, Y needs to be flipped to transform to default coordinate system
            var (scaleArea, angleArea, distanceArea) = CalculateDrawVariables(
                playerPos,
                new Vector2(
                    mapIconComponentNode->AtkResNode.X,
                    -mapIconComponentNode->AtkResNode.Y
                ),
                playerForward,
                mapScale,
                maxDistance,
                distanceOffset,
                lowestScaleFactor);
            var radius = mapIconComponentNode->AtkResNode.ScaleX *
                         (mapIconComponentNode->AtkResNode.Width - mapIconComponentNode->AtkResNode.OriginX);
            // NOTE (Chiv) We assume part.Width == part.Height == 32
            var areaCircleOffset = compassUnit * angleArea;
            var areaHalfWidth = halfWidth32 * scaleArea;
            if (distanceArea >= radius)
                return (
                    new Vector2(compassCentre.X - areaHalfWidth + areaCircleOffset, compassCentre.Y - areaHalfWidth),
                    new Vector2(compassCentre.X + areaCircleOffset + areaHalfWidth, compassCentre.Y + areaHalfWidth),
                    0xFFFFFFFF, false);
            var tintColour = ImGui.ColorConvertFloat4ToU32(new Vector4(
                (255 + imgNode->AtkResNode.AddRed) * (imgNode->AtkResNode.MultiplyRed / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddGreen) * (imgNode->AtkResNode.MultiplyGreen / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddBlue) * (imgNode->AtkResNode.MultiplyBlue / 100f) / 255f,
                1));
            return (new Vector2(compassCentre.X - areaHalfWidth, compassCentre.Y - areaHalfWidth)
                , new Vector2(compassCentre.X + areaHalfWidth, compassCentre.Y + areaHalfWidth)
                , tintColour, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (float distanceScaleFactor, float signedAngle, float distance) CalculateDrawVariables(in Vector2 from,
            in Vector2 to, in Vector2 forward, float distanceScaling, float maxDistance = 180f, float distanceOffset = 40f, float lowestScaleFactor = 0.2f)
        {
            // TODO (Chiv) Distance Offset adjustments
            distanceOffset *= distanceScaling; //80f @Max Zoom(==2) _NaviMap, default
            maxDistance *= distanceScaling; //360f @Max Zoom(==2) _NaviMap, default
            var distance = Vector2.Distance(to, from);
            var scaleFactor = Math.Max(1f - (distance - distanceOffset) / maxDistance, lowestScaleFactor);
            return (scaleFactor,SignedAngle(to  - from, forward), distance);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedAngle(float rotation,in Vector2 forward)
        {
            var cosObject = (float) Math.Cos(rotation);
            var sinObject = (float) Math.Sin(rotation);
            // Note(Chiv) Same reasoning as player rotation,
            // but the map rotation is mirrored in comparison, which changes the sinus
            var objectForward = new Vector2(sinObject, cosObject);
            return SignedAngle(objectForward, forward);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedAngle(in Vector2 from,in Vector2 to)
        {
            var dot = Vector2.Dot(Vector2.Normalize(from), Vector2.Normalize(to));
            var sign = (from.X * to.Y - from.Y * to.X) >= 0 ? 1 : -1;
            return (float)Math.Acos(dot) * sign;
        }
        
        #endregion
    }
}