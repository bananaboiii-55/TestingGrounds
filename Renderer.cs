using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace WallhackandAimbotCombinedTest
{
    // ── Serialisable settings bag ─────────────────────────────────────────
    public class Settings
    {
        public bool EnableESP { get; set; } = true;
        public bool EnableName { get; set; } = true;
        public bool EspDrawBones { get; set; } = true;
        public float BoneThickness { get; set; } = 4f;

        public float[] EnemyColor { get; set; } = { 1, 0, 0, 1 };
        public float[] TeamColor { get; set; } = { 0, 1, 0, 1 };
        public float[] BoneColor { get; set; } = { 1, 1, 1, 1 };
        public float[] NameColor { get; set; } = { 1, 1, 1, 1 };
        public float[] CircleColor { get; set; } = { 1, 1, 1, 1 };

        public bool Aimbot { get; set; } = true;
        public bool AimOnTeam { get; set; } = false;
        public float FOV { get; set; } = 50f;
        public float AimSmooth { get; set; } = 0.22f;
        public float AimSwitchHysteresis { get; set; } = 42f;
    }

    public class Renderer : Overlay
    {
        // ── Config path ───────────────────────────────────────────────────
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "y018_settings.json");

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

        // Menu state
        private bool _menuVisible = true;
        private bool _escWasDown = false;
        private bool _rshiftWasDown = false;
        private bool _focusNextFrame = false;

        // Splash
        private bool _splashDone = false;
        private readonly Stopwatch _splashTimer = Stopwatch.StartNew();
        private const double SplashDuration = 3.0;

        // Win32
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern int ShowCursor(bool bShow);
        [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_RSHIFT = 0xA1;

        private IntPtr _overlayHwnd = IntPtr.Zero;

        // ── Constructor — load settings ───────────────────────────────────
        public Renderer()
        {
            LoadSettings();
        }

        // ── Settings helpers ──────────────────────────────────────────────
        private static Vector4 ToVec4(float[] a) =>
            a != null && a.Length == 4 ? new Vector4(a[0], a[1], a[2], a[3]) : Vector4.One;

        private static float[] FromVec4(Vector4 v) => new[] { v.X, v.Y, v.Z, v.W };

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath));
                if (s == null) return;

                enableESP = s.EnableESP;
                enableName = s.EnableName;
                espDrawBones = s.EspDrawBones;
                boneThickness = s.BoneThickness;
                enemyColor = ToVec4(s.EnemyColor);
                teamColor = ToVec4(s.TeamColor);
                boneColor = ToVec4(s.BoneColor);
                nameColor = ToVec4(s.NameColor);
                circleColor = ToVec4(s.CircleColor);
                aimbot = s.Aimbot;
                aimOnTeam = s.AimOnTeam;
                FOV = s.FOV;
                aimSmooth = s.AimSmooth;
                aimSwitchHysteresis = s.AimSwitchHysteresis;
            }
            catch { /* ignore corrupt config */ }
        }

        private void SaveSettings()
        {
            try
            {
                var s = new Settings
                {
                    EnableESP = enableESP,
                    EnableName = enableName,
                    EspDrawBones = espDrawBones,
                    BoneThickness = boneThickness,
                    EnemyColor = FromVec4(enemyColor),
                    TeamColor = FromVec4(teamColor),
                    BoneColor = FromVec4(boneColor),
                    NameColor = FromVec4(nameColor),
                    CircleColor = FromVec4(circleColor),
                    Aimbot = aimbot,
                    AimOnTeam = aimOnTeam,
                    FOV = FOV,
                    AimSmooth = aimSmooth,
                    AimSwitchHysteresis = aimSwitchHysteresis,
                };
                File.WriteAllText(ConfigPath,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore write errors */ }
        }

        // ── Focus helper ──────────────────────────────────────────────────
        private IntPtr GetOverlayHwnd()
        {
            if (_overlayHwnd != IntPtr.Zero) return _overlayHwnd;
            uint myPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == myPid) { found = hWnd; return false; }
                return true;
            }, IntPtr.Zero);
            _overlayHwnd = found;
            return _overlayHwnd;
        }

        private void FocusOverlay()
        {
            IntPtr hwnd = GetOverlayHwnd();
            if (hwnd == IntPtr.Zero) return;
            IntPtr fgHwnd = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fgHwnd, out _);
            uint myThread = GetCurrentThreadId();
            AttachThreadInput(myThread, fgThread, true);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            AttachThreadInput(myThread, fgThread, false);
            ClipCursor(IntPtr.Zero);
            ShowCursor(true);
        }

        ImDrawListPtr drawList;

        protected override void Render()
        {
            screenSize = ImGui.GetIO().DisplaySize;
            ApplyPurpleStyle();

            // ── Splash ────────────────────────────────────────────────────
            if (!_splashDone)
            {
                double elapsed = _splashTimer.Elapsed.TotalSeconds;
                float alpha = elapsed > SplashDuration - 0.6
                    ? (float)Math.Max(0, (SplashDuration - elapsed) / 0.6) : 1f;
                if (elapsed >= SplashDuration) _splashDone = true;
                else { DrawSplash(alpha); return; }
            }

            // ── Keys ──────────────────────────────────────────────────────
            bool escDown = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
            bool rshiftDown = (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;

            if (escDown && !_escWasDown)
            {
                _menuVisible = false;
                ShowCursor(false);
                SaveSettings(); // save on hide
            }

            if (rshiftDown && !_rshiftWasDown)
            {
                _menuVisible = true;
                _focusNextFrame = true;
                FocusOverlay();
            }

            _escWasDown = escDown;
            _rshiftWasDown = rshiftDown;

            // ── ImGui windows ─────────────────────────────────────────────
            if (_menuVisible)
            {
                ImGui.SetNextWindowSize(new Vector2(260, 0), ImGuiCond.FirstUseEver);
                if (_focusNextFrame) ImGui.SetNextWindowFocus();
                ImGui.Begin("  ESP Settings");

                ImGui.Spacing();
                ImGui.Text("Visibility");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Checkbox("Enable ESP", ref enableESP);
                ImGui.Checkbox("Show player names", ref enableName);
                ImGui.Checkbox("Draw skeleton", ref espDrawBones);

                ImGui.Spacing();
                ImGui.Text("Appearance");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.SliderFloat("Bone thickness", ref boneThickness, 0.5f, 16f);

                ImGui.Spacing();
                if (ImGui.CollapsingHeader("  Team color")) { ImGui.Spacing(); ImGui.ColorPicker4("##teamcolor", ref teamColor); }
                if (ImGui.CollapsingHeader("  Enemy color")) { ImGui.Spacing(); ImGui.ColorPicker4("##enemycolor", ref enemyColor); }
                if (ImGui.CollapsingHeader("  Bone color")) { ImGui.Spacing(); ImGui.ColorPicker4("##bonecolor", ref boneColor); }

                ImGui.Spacing();

                // Save button
                if (ImGui.Button("Save Settings"))
                    SaveSettings();

                ImGui.End();

                ImGui.SetNextWindowSize(new Vector2(270, 0), ImGuiCond.FirstUseEver);
                ImGui.Begin("  Aimbot Settings");

                ImGui.Spacing();
                ImGui.Text("Control");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Checkbox("Enable aimbot", ref aimbot);
                ImGui.Checkbox("Aim on teammates", ref aimOnTeam);

                ImGui.Spacing();
                ImGui.Text("Tuning");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.SliderFloat("FOV radius (px)", ref FOV, 10, 300);
                ImGui.SliderFloat("Smoothing", ref aimSmooth, 0.05f, 1f);
                ImGui.SetItemTooltip("Lower = smoother aim");
                ImGui.SliderFloat("Target steal threshold", ref aimSwitchHysteresis, 5f, 120f);
                ImGui.SetItemTooltip("How many px closer a target must be to steal lock");

                ImGui.Spacing();
                if (ImGui.CollapsingHeader("  FOV circle color")) { ImGui.Spacing(); ImGui.ColorPicker4("##circlecolor", ref circleColor); }

                ImGui.Spacing();

                // Save button
                if (ImGui.Button("Save Settings"))
                    SaveSettings();

                ImGui.End();

                _focusNextFrame = false;
            }

            // ── Foreground draws ──────────────────────────────────────────
            drawList = ImGui.GetForegroundDrawList();
            drawList.AddCircle(new Vector2(screenSize.X / 2, screenSize.Y / 2), FOV,
                ImGui.ColorConvertFloat4ToU32(circleColor));
            DrawWatermark();

            if (enableESP)
            {
                foreach (var entity in entities)
                {
                    if (EntityOnScreen(entity))
                    {
                        DrawHealthBar(entity);
                        DrawBox(entity);
                        DrawLine(entity);
                        if (espDrawBones) DrawBones(entity);
                        DrawName(entity, 20);
                    }
                }
            }
        }

        // ── Splash ────────────────────────────────────────────────────────
        private void DrawSplash(float alpha)
        {
            var dl = ImGui.GetForegroundDrawList();
            Vector2 center = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);

            dl.AddRectFilled(Vector2.Zero, screenSize,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.00f, 0.08f, alpha * 0.97f)));
            dl.AddCircleFilled(center, 160f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.10f, 0.85f, alpha * 0.18f)));
            dl.AddCircleFilled(center, 110f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.40f, 0.05f, 0.65f, alpha * 0.22f)));

            uint lineCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.60f, 0.15f, 0.90f, alpha * 0.55f));
            dl.AddLine(new Vector2(center.X - 220f, center.Y - 48f), new Vector2(center.X + 220f, center.Y - 48f), lineCol, 1.2f);
            dl.AddLine(new Vector2(center.X - 220f, center.Y + 52f), new Vector2(center.X + 220f, center.Y + 52f), lineCol, 1.2f);

            float titleScale = 3.8f;
            string title = "y018client";
            Vector2 titleSize = ImGui.CalcTextSize(title) * titleScale;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * titleScale,
                center - titleSize / 2f + new Vector2(3f, 3f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.00f, 0.40f, alpha * 0.80f)), title);
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * titleScale,
                center - titleSize / 2f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.88f, 0.60f, 1.00f, alpha)), title);

            float subScale = 1.4f;
            string sub = "version 1.0";
            Vector2 subSize = ImGui.CalcTextSize(sub) * subScale;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * subScale,
                new Vector2(center.X - subSize.X / 2f, center.Y + titleSize.Y / 2f + 12f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.70f, 0.40f, 0.95f, alpha * 0.85f)), sub);

            string dotsStr = new string('.', (int)(_splashTimer.Elapsed.TotalSeconds * 2.5) % 4);
            Vector2 dotSize = ImGui.CalcTextSize(dotsStr);
            dl.AddText(new Vector2(center.X - dotSize.X / 2f, center.Y + titleSize.Y / 2f + 46f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.60f, 0.30f, 0.80f, alpha * 0.70f)), dotsStr);
        }

        // ── Watermark ─────────────────────────────────────────────────────
        private void DrawWatermark()
        {
            const string text = "y018 client";
            uint textCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.78f, 0.30f, 1.00f, 1.00f));
            uint bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.02f, 0.14f, 0.80f));
            uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.15f, 0.80f, 0.90f));
            Vector2 padding = new Vector2(8f, 4f);
            Vector2 textPos = new Vector2(10f, 10f);
            Vector2 textSize = ImGui.CalcTextSize(text);
            Vector2 boxMin = textPos - padding;
            Vector2 boxMax = textPos + textSize + padding;
            drawList.AddRectFilled(boxMin, boxMax, bgCol, 5f);
            drawList.AddRect(boxMin, boxMax, borderCol, 5f, ImDrawFlags.None, 1.5f);
            drawList.AddText(textPos, textCol, text);
        }

        // ── Purple theme ──────────────────────────────────────────────────
        private static void ApplyPurpleStyle()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.02f, 0.14f, 0.94f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.03f, 0.16f, 0.80f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.03f, 0.16f, 0.95f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.55f, 0.15f, 0.80f, 0.55f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0f, 0f, 0f, 0f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.18f, 0.05f, 0.28f, 0.85f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.30f, 0.10f, 0.45f, 0.90f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.40f, 0.15f, 0.60f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.12f, 0.03f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.38f, 0.08f, 0.60f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.12f, 0.03f, 0.20f, 0.75f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.03f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.06f, 0.01f, 0.10f, 0.85f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.42f, 0.10f, 0.65f, 0.80f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.55f, 0.18f, 0.80f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.70f, 0.28f, 0.95f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.85f, 0.40f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.60f, 0.18f, 0.88f, 0.90f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.78f, 0.32f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.28f, 0.07f, 0.46f, 0.85f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.46f, 0.13f, 0.70f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.62f, 0.22f, 0.88f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.32f, 0.07f, 0.52f, 0.80f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.46f, 0.13f, 0.70f, 0.90f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.62f, 0.22f, 0.88f, 1.00f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.45f, 0.12f, 0.68f, 0.60f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.60f, 0.20f, 0.85f, 0.80f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.75f, 0.30f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.48f, 0.12f, 0.72f, 0.40f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.62f, 0.22f, 0.88f, 0.70f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.78f, 0.32f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.18f, 0.05f, 0.30f, 0.85f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.50f, 0.15f, 0.75f, 0.90f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.38f, 0.10f, 0.62f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.12f, 0.03f, 0.20f, 0.85f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.25f, 0.07f, 0.40f, 1.00f);
            colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.88f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.38f, 0.60f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.45f, 0.12f, 0.68f, 0.45f);
            colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.75f, 0.30f, 1.00f, 1.00f);
            style.WindowRounding = 7f;
            style.FrameRounding = 4f;
            style.GrabRounding = 4f;
            style.ScrollbarRounding = 5f;
            style.TabRounding = 4f;
            style.FramePadding = new Vector2(8f, 4f);
            style.ItemSpacing = new Vector2(8f, 6f);
            style.WindowPadding = new Vector2(12f, 10f);
            style.WindowBorderSize = 1f;
            style.FrameBorderSize = 0f;
        }

        // ── Unchanged helpers ─────────────────────────────────────────────

        bool EntityOnScreen(Entity entity)
        {
            Vector2 p = entity.position2D; Vector2 h = entity.head2d;
            if (p.X < 0 || p.Y < 0 || h.X < 0 || h.Y < 0) return false;
            if (p.X > screenSize.X || p.Y > screenSize.Y || h.X > screenSize.X || h.Y > screenSize.Y) return false;
            return true;
        }

        private static readonly (int A, int B)[] BoneSegments =
        {
            (0, 5), (5, 6), (5, 8), (8, 9), (9, 11),
            (5, 16), (16, 14), (14, 17),
            (0, 23), (23, 24), (0, 26), (26, 27),
        };

        private void DrawBones(Entity entity)
        {
            uint uintColor = ImGui.ColorConvertFloat4ToU32(boneColor);
            float t = boneThickness / MathF.Max(entity.distance, 1f);
            if (entity.bones2d != null && entity.bones2d.Count > 6)
            {
                foreach (var (a, b) in BoneSegments) BoneLine(entity.bones2d, a, b, uintColor, t);
                if (BoneOk(entity.bones2d, 6)) drawList.AddCircle(entity.bones2d[6], 4f + t, uintColor);
                else drawList.AddCircle(entity.head2d, 4f + t, uintColor);
                return;
            }
            drawList.AddLine(entity.position2D, entity.viewPosition2D, uintColor, t * 0.85f);
            drawList.AddLine(entity.viewPosition2D, entity.head2d, uintColor, t);
            drawList.AddCircle(entity.head2d, 4f + t, uintColor);
        }

        private static bool BoneOk(IReadOnlyList<Vector2> bones, int i)
        {
            if (i < 0 || i >= bones.Count) return false;
            Vector2 v = bones[i]; return v.X >= 0f && v.Y >= 0f;
        }

        private void BoneLine(IReadOnlyList<Vector2> bones, int a, int b, uint color, float thickness)
        {
            if (!BoneOk(bones, a) || !BoneOk(bones, b)) return;
            Vector2 pa = bones[a]; Vector2 pb = bones[b];
            if (pa.X > screenSize.X || pa.Y > screenSize.Y || pb.X > screenSize.X || pb.Y > screenSize.Y) return;
            drawList.AddLine(pa, pb, color, thickness);
        }

        private void DrawHealthBar(Entity entity)
        {
            float h = entity.position2D.Y - entity.viewPosition2D.Y;
            float l = entity.viewPosition2D.X - h / 3;
            float r = entity.position2D.X + h / 3;
            float bw = 0.05f * (r - l);
            drawList.AddRectFilled(
                new Vector2(l - bw, entity.position2D.Y - h * (entity.health / 100f)),
                new Vector2(l, entity.position2D.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)));
        }

        private void DrawName(Entity entity, int yOffset)
        {
            if (enableName)
                drawList.AddText(new Vector2(entity.viewPosition2D.X, entity.viewPosition2D.Y - yOffset),
                    ImGui.ColorConvertFloat4ToU32(nameColor), $"{entity.name}");
        }

        private void DrawBox(Entity entity)
        {
            float h = entity.position2D.Y - entity.viewPosition2D.Y;
            Vector4 boxColor = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddRect(
                new Vector2(entity.viewPosition2D.X - h / 3, entity.viewPosition2D.Y),
                new Vector2(entity.position2D.X + h / 3, entity.position2D.Y),
                ImGui.ColorConvertFloat4ToU32(boxColor));
        }

        private void DrawLine(Entity entity)
        {
            Vector4 lineColor = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddLine(new Vector2(screenSize.X / 2, screenSize.Y), entity.position2D,
                ImGui.ColorConvertFloat4ToU32(lineColor));
        }

        public void UpdateEntities(IEnumerable<Entity> newEntites)
            => entities = new ConcurrentQueue<Entity>(newEntites);

        public void UpdateLocalPlayer(Entity newEntity)
        { lock (entityLock) { localPlayer = newEntity; } }
    }
}