using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace WallhackandAimbotCombinedTest
{
    public class Renderer : Overlay
    {
        public Vector2 screenSize = new Vector2(1920, 1080);

        private ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        private bool enableESP = true;
        public bool enableName = true;
        public bool espDrawBones = true;

        public float boneThickness = 4f;

        private Vector4 enemyColor = new Vector4(1, 0, 0, 1);
        private Vector4 teamColor = new Vector4(0, 1, 0, 1);
        private Vector4 boneColor = new Vector4(1, 1, 1, 1);
        private Vector4 nameColor = new Vector4(1, 1, 1, 1);

        public bool aimbot = true;
        public bool aimOnTeam = false;
        public float FOV = 50;
        public float aimSmooth = 0.22f;
        public float aimSwitchHysteresis = 42f;
        public Vector4 circleColor = new Vector4(1, 1, 1, 1);

        ImDrawListPtr drawList;

        protected override void Render()
        {
            screenSize = ImGui.GetIO().DisplaySize;

            ImGui.Begin("Basic ESP");
            ImGui.Checkbox("Enable ESP", ref enableESP);
            ImGui.Checkbox("Enable name", ref enableName);
            ImGui.Checkbox("Draw skeleton (bones)", ref espDrawBones);
            ImGui.SliderFloat("bone thickness", ref boneThickness, 0.5f, 16f);

            if (ImGui.CollapsingHeader("Team color"))
                ImGui.ColorPicker4("##teamcolor", ref teamColor);
            if (ImGui.CollapsingHeader("Enemy color"))
                ImGui.ColorPicker4("##enemycolor", ref enemyColor);
            if (ImGui.CollapsingHeader("Bone color"))
                ImGui.ColorPicker4("##bonecolor", ref boneColor);
            ImGui.End();

            ImGui.Begin("Aimbot");
            ImGui.Checkbox("aimbot", ref aimbot);
            ImGui.Checkbox("aim on teamates, aswell", ref aimOnTeam);
            ImGui.SliderFloat("pixel FOV", ref FOV, 10, 300);
            ImGui.SliderFloat("aim smooth (lower = smoother)", ref aimSmooth, 0.05f, 1f);
            ImGui.SliderFloat("target switch (px closer to steal)", ref aimSwitchHysteresis, 5f, 120f);
            if (ImGui.CollapsingHeader("FOV circle color"))
                ImGui.ColorPicker4("##ciclecolor", ref circleColor);
            ImGui.End();

            drawList = ImGui.GetForegroundDrawList();
            drawList.AddCircle(new Vector2(screenSize.X / 2, screenSize.Y / 2), FOV, ImGui.ColorConvertFloat4ToU32(circleColor));

            if (enableESP)
            {
                foreach (var entity in entities)
                {
                    if (EntityOnScreen(entity))
                    {
                        DrawHealthBar(entity);
                        DrawBox(entity);
                        DrawLine(entity);
                        if (espDrawBones)
                            DrawBones(entity);
                        DrawName(entity, 20);
                    }
                }
            }
        }

        bool EntityOnScreen(Entity entity)
        {
            Vector2 p = entity.position2D;
            Vector2 h = entity.head2d;
            if (p.X < 0 || p.Y < 0 || h.X < 0 || h.Y < 0)
                return false;
            if (p.X > screenSize.X || p.Y > screenSize.Y || h.X > screenSize.X || h.Y > screenSize.Y)
                return false;
            return true;
        }

        // CS2 player bone indices (see Entity.BoneIds). Pelvis(0)→neck(5) gives torso; arms/legs branch from that pose.
        private static readonly (int A, int B)[] BoneSegments =
        {
            (0, 5),
            (5, 6),
            (5, 8), (8, 9), (9, 11),
            (5, 16), (16, 14), (14, 17),
            (0, 23), (23, 24),
            (0, 26), (26, 27),
        };

        private void DrawBones(Entity entity)
        {
            uint uintColor = ImGui.ColorConvertFloat4ToU32(boneColor);
            float t = boneThickness / MathF.Max(entity.distance, 1f);

            if (entity.bones2d != null && entity.bones2d.Count > 6)
            {
                foreach (var (a, b) in BoneSegments)
                    BoneLine(entity.bones2d, a, b, uintColor, t);

                if (BoneOk(entity.bones2d, 6))
                    drawList.AddCircle(entity.bones2d[6], 4f + t, uintColor);
                else
                    drawList.AddCircle(entity.head2d, 4f + t, uintColor);
                return;
            }

            drawList.AddLine(entity.position2D, entity.viewPosition2D, uintColor, t * 0.85f);
            drawList.AddLine(entity.viewPosition2D, entity.head2d, uintColor, t);
            drawList.AddCircle(entity.head2d, 4f + t, uintColor);
        }

        private static bool BoneOk(IReadOnlyList<Vector2> bones, int i)
        {
            if (i < 0 || i >= bones.Count) return false;
            Vector2 v = bones[i];
            return v.X >= 0f && v.Y >= 0f;
        }

        private void BoneLine(IReadOnlyList<Vector2> bones, int a, int b, uint color, float thickness)
        {
            if (!BoneOk(bones, a) || !BoneOk(bones, b))
                return;
            Vector2 pa = bones[a];
            Vector2 pb = bones[b];
            if (pa.X > screenSize.X || pa.Y > screenSize.Y || pb.X > screenSize.X || pb.Y > screenSize.Y)
                return;
            drawList.AddLine(pa, pb, color, thickness);
        }

        private void DrawHealthBar(Entity entity)
        {
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;

            float boxLeft = entity.viewPosition2D.X - entityHeight / 3;
            float boxRight = entity.position2D.X + entityHeight / 3;

            float barPercentWidth = 0.05f;
            float barPixelWidth = barPercentWidth * (boxRight - boxLeft);

            float barHeight = entityHeight * (entity.health / 100f);

            Vector2 barTop = new Vector2(boxLeft - barPixelWidth, entity.position2D.Y - barHeight);
            Vector2 barBottom = new Vector2(boxLeft, entity.position2D.Y);

            Vector4 barColor = new Vector4(0, 1, 0, 1);

            drawList.AddRectFilled(barTop, barBottom, ImGui.ColorConvertFloat4ToU32(barColor));
        }

        private void DrawName(Entity entity, int yOffset)
        {
            if (enableName)
            {
                Vector2 textLocation = new Vector2(entity.viewPosition2D.X, entity.viewPosition2D.Y - yOffset);
                drawList.AddText(textLocation, ImGui.ColorConvertFloat4ToU32(nameColor), $"{entity.name}");
            }
        }

        private void DrawBox(Entity entity)
        {
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;

            Vector2 rectTop = new Vector2(entity.viewPosition2D.X - entityHeight / 3, entity.viewPosition2D.Y);

            Vector2 rectBottom = new Vector2(entity.position2D.X + entityHeight / 3, entity.position2D.Y);

            Vector4 boxColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            drawList.AddRect(rectTop, rectBottom, ImGui.ColorConvertFloat4ToU32(boxColor));
        }

        private void DrawLine(Entity entity)
        {
            Vector4 lineColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            drawList.AddLine(new Vector2(screenSize.X / 2, screenSize.Y), entity.position2D, ImGui.ColorConvertFloat4ToU32(lineColor));
        }

        public void UpdateEntities(IEnumerable<Entity> newEntites)
        {
            entities = new ConcurrentQueue<Entity>(newEntites);
        }

        public void UpdateLocalPlayer(Entity newEntity)
        {
            lock (entityLock)
            {
                localPlayer = newEntity;
            }
        }
    }
}
