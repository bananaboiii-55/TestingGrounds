using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace WallhackandAimbotCombinedTest
{
    // ── Settings ──────────────────────────────────────────────────────────
    public class Settings
    {
        public bool EnableESP { get; set; } = true;
        public bool EnableName { get; set; } = true;
        public bool EspDrawBones { get; set; } = true;
        public float BoneThickness { get; set; } = 4f;
        public bool EnableChams { get; set; } = false;
        public float ChamsAlpha { get; set; } = 0.85f;
        public float ChamsThickness { get; set; } = 12f;

        public float[] EnemyColor { get; set; } = { 1f, 0f, 0f, 1f };
        public float[] TeamColor { get; set; } = { 0f, 1f, 0f, 1f };
        public float[] BoneColor { get; set; } = { 1f, 1f, 1f, 1f };
        public float[] NameColor { get; set; } = { 1f, 1f, 1f, 1f };
        public float[] CircleColor { get; set; } = { 1f, 1f, 1f, 1f };
        public float[] ChamsEnemyColor { get; set; } = { 0.9f, 0.15f, 0.15f, 1f };
        public float[] ChamsTeamColor { get; set; } = { 0.15f, 0.55f, 1f, 1f };

        public bool Aimbot { get; set; } = true;
        public bool AimOnTeam { get; set; } = false;
        public bool AimVisibleOnly { get; set; } = false;
        public bool AimTargetClosestDistance { get; set; } = false;
        public float FOV { get; set; } = 50f;
        public float AimSmooth { get; set; } = 0.22f;
        public float AimSwitchHysteresis { get; set; } = 42f;
        public float AimHumanisation { get; set; } = 0f;
    }

    public class Renderer : Overlay
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "y018_settings.json");

        public Vector2 screenSize = new Vector2(1920, 1080);
        private ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        // ESP
        private bool enableESP = true;
        public bool enableName = true;
        public bool espDrawBones = true;
        public float boneThickness = 4f;

        // Chams
        private bool enableChams = false;
        private float chamsAlpha = 0.85f;
        private float chamsThickness = 12f;

        // Colors
        private Vector4 enemyColor = new Vector4(1f, 0f, 0f, 1f);
        private Vector4 teamColor = new Vector4(0f, 1f, 0f, 1f);
        private Vector4 boneColor = new Vector4(1f, 1f, 1f, 1f);
        private Vector4 nameColor = new Vector4(1f, 1f, 1f, 1f);
        private Vector4 chamsEnemyColor = new Vector4(0.9f, 0.15f, 0.15f, 1f);
        private Vector4 chamsTeamColor = new Vector4(0.15f, 0.55f, 1f, 1f);

        // Aimbot
        public bool aimbot = true;
        public bool aimOnTeam = false;
        public bool aimVisibleOnly = false;
        public bool aimTargetClosestDistance = false;
        public float FOV = 50f;
        public float aimSmooth = 0.22f;
        public float aimSwitchHysteresis = 42f;
        public float aimHumanisation = 0f;
        public Vector4 circleColor = new Vector4(1f, 1f, 1f, 1f);

        // Menu / splash
        private bool _menuVisible = true;
        private bool _escWasDown = false;
        private bool _rshiftWasDown = false;
        private bool _focusNextFrame = false;
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
        ImDrawListPtr drawList;

        // Chams bone segments (boneA, boneB, radius scale)
        private static readonly (int A, int B, float R)[] ChamsBones =
        {
            (0,  5,  1.5f),
            (5,  6,  1.3f),
            (5,  8,  0.80f),
            (8,  9,  0.70f),
            (9,  11, 0.50f),
            (5,  16, 0.80f),
            (16, 14, 0.70f),
            (14, 17, 0.50f),
            (0,  23, 0.90f),
            (23, 24, 0.80f),
            (0,  26, 0.90f),
            (26, 27, 0.80f),
        };

        public Renderer() { LoadSettings(); }

        // ── Settings ──────────────────────────────────────────────────────
        private static Vector4 ToVec4(float[] a) =>
            a?.Length == 4 ? new Vector4(a[0], a[1], a[2], a[3]) : Vector4.One;
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
                enableChams = s.EnableChams;
                chamsAlpha = s.ChamsAlpha;
                chamsThickness = s.ChamsThickness;
                enemyColor = ToVec4(s.EnemyColor);
                teamColor = ToVec4(s.TeamColor);
                boneColor = ToVec4(s.BoneColor);
                nameColor = ToVec4(s.NameColor);
                circleColor = ToVec4(s.CircleColor);
                chamsEnemyColor = ToVec4(s.ChamsEnemyColor);
                chamsTeamColor = ToVec4(s.ChamsTeamColor);
                aimbot = s.Aimbot;
                aimOnTeam = s.AimOnTeam;
                aimVisibleOnly = s.AimVisibleOnly;
                aimTargetClosestDistance = s.AimTargetClosestDistance;
                FOV = s.FOV;
                aimSmooth = s.AimSmooth;
                aimSwitchHysteresis = s.AimSwitchHysteresis;
                aimHumanisation = s.AimHumanisation;
            }
            catch { }
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
                    EnableChams = enableChams,
                    ChamsAlpha = chamsAlpha,
                    ChamsThickness = chamsThickness,
                    EnemyColor = FromVec4(enemyColor),
                    TeamColor = FromVec4(teamColor),
                    BoneColor = FromVec4(boneColor),
                    NameColor = FromVec4(nameColor),
                    CircleColor = FromVec4(circleColor),
                    ChamsEnemyColor = FromVec4(chamsEnemyColor),
                    ChamsTeamColor = FromVec4(chamsTeamColor),
                    Aimbot = aimbot,
                    AimOnTeam = aimOnTeam,
                    AimVisibleOnly = aimVisibleOnly,
                    AimTargetClosestDistance = aimTargetClosestDistance,
                    FOV = FOV,
                    AimSmooth = aimSmooth,
                    AimSwitchHysteresis = aimSwitchHysteresis,
                    AimHumanisation = aimHumanisation,
                };
                File.WriteAllText(ConfigPath,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ── Focus overlay ─────────────────────────────────────────────────
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

        // ── Main render ───────────────────────────────────────────────────
        protected override void Render()
        {
            screenSize = ImGui.GetIO().DisplaySize;
            ApplyPurpleStyle();

            if (!_splashDone)
            {
                double elapsed = _splashTimer.Elapsed.TotalSeconds;
                float alpha = elapsed > SplashDuration - 0.6
                    ? (float)Math.Max(0, (SplashDuration - elapsed) / 0.6) : 1f;
                if (elapsed >= SplashDuration) _splashDone = true;
                else { DrawSplash(alpha); return; }
            }

            bool escDown = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
            bool rshiftDown = (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;

            if (escDown && !_escWasDown) { _menuVisible = false; ShowCursor(false); SaveSettings(); }
            if (rshiftDown && !_rshiftWasDown) { _menuVisible = true; _focusNextFrame = true; FocusOverlay(); }

            _escWasDown = escDown;
            _rshiftWasDown = rshiftDown;

            if (_menuVisible)
            {
                // ── ESP window ────────────────────────────────────────────
                ImGui.SetNextWindowSize(new Vector2(265, 0), ImGuiCond.FirstUseEver);
                if (_focusNextFrame) ImGui.SetNextWindowFocus();
                ImGui.Begin("  ESP Settings");

                ImGui.Spacing();
                ImGui.Text("Visibility");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.Checkbox("Enable ESP", ref enableESP);
                ImGui.Checkbox("Show names", ref enableName);
                ImGui.Checkbox("Draw skeleton", ref espDrawBones);

                ImGui.Spacing();
                ImGui.Text("Chams  —  body through walls");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.Checkbox("Enable chams", ref enableChams);
                if (enableChams)
                {
                    ImGui.SliderFloat("Limb thickness", ref chamsThickness, 4f, 28f);
                    ImGui.SetItemTooltip("Radius of each limb capsule");
                    ImGui.SliderFloat("Opacity", ref chamsAlpha, 0.1f, 1.0f);
                    ImGui.Spacing();
                    if (ImGui.CollapsingHeader("  Chams enemy colour"))
                    { ImGui.Spacing(); ImGui.ColorPicker4("##cec", ref chamsEnemyColor); }
                    if (ImGui.CollapsingHeader("  Chams team colour"))
                    { ImGui.Spacing(); ImGui.ColorPicker4("##ctc", ref chamsTeamColor); }
                }

                ImGui.Spacing();
                ImGui.Text("Appearance");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.SliderFloat("Bone thickness", ref boneThickness, 0.5f, 16f);
                ImGui.Spacing();
                if (ImGui.CollapsingHeader("  Team colour")) { ImGui.Spacing(); ImGui.ColorPicker4("##tc", ref teamColor); }
                if (ImGui.CollapsingHeader("  Enemy colour")) { ImGui.Spacing(); ImGui.ColorPicker4("##ec", ref enemyColor); }
                if (ImGui.CollapsingHeader("  Bone colour")) { ImGui.Spacing(); ImGui.ColorPicker4("##bc", ref boneColor); }
                ImGui.Spacing();
                if (ImGui.Button("Save##esp")) SaveSettings();
                ImGui.End();

                // ── Aimbot window ─────────────────────────────────────────
                ImGui.SetNextWindowSize(new Vector2(275, 0), ImGuiCond.FirstUseEver);
                ImGui.Begin("  Aimbot Settings");

                ImGui.Spacing();
                ImGui.Text("Control");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.Checkbox("Enable aimbot", ref aimbot);
                ImGui.Checkbox("Aim on teammates", ref aimOnTeam);
                ImGui.Checkbox("Visible targets only", ref aimVisibleOnly);
                ImGui.SetItemTooltip("Only aim at targets whose head is on screen");
                ImGui.Checkbox("Prioritise closest by distance", ref aimTargetClosestDistance);
                ImGui.SetItemTooltip("ON  = locks nearest player in world space\nOFF = locks nearest to crosshair");

                ImGui.Spacing();
                ImGui.Text("Tuning");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.SliderFloat("FOV radius (px)", ref FOV, 10f, 300f);
                ImGui.SliderFloat("Smoothing", ref aimSmooth, 0.05f, 1f);
                ImGui.SetItemTooltip("Lower = smoother aim movement");
                ImGui.SliderFloat("Target steal threshold", ref aimSwitchHysteresis, 5f, 120f);
                ImGui.SetItemTooltip("How many px closer a new target must be to steal lock");

                ImGui.Spacing();
                ImGui.Text("Humanisation");
                ImGui.Separator(); ImGui.Spacing();
                ImGui.SliderFloat("Humanisation", ref aimHumanisation, 0f, 1f);
                ImGui.SetItemTooltip(
                    "0 = perfectly mechanical\n" +
                    "0.3 = slight speed variation + occasional micro-miss\n" +
                    "0.7 = noticeable jitter, misses more often\n" +
                    "1.0 = very erratic, frequently undershoots");

                // Live preview label so user knows what level they're at
                string humanLabel = aimHumanisation switch
                {
                    < 0.01f => "Off",
                    < 0.25f => "Subtle",
                    < 0.50f => "Moderate",
                    < 0.75f => "Heavy",
                    _ => "Very heavy"
                };
                ImGui.SameLine();
                ImGui.TextDisabled($"({humanLabel})");

                ImGui.Spacing();
                if (ImGui.CollapsingHeader("  FOV circle colour"))
                { ImGui.Spacing(); ImGui.ColorPicker4("##fcc", ref circleColor); }
                ImGui.Spacing();
                if (ImGui.Button("Save##aim")) SaveSettings();
                ImGui.End();

                _focusNextFrame = false;
            }

            // ── Foreground draws ──────────────────────────────────────────
            drawList = ImGui.GetForegroundDrawList();

            drawList.AddCircle(
                new Vector2(screenSize.X / 2, screenSize.Y / 2),
                FOV, ImGui.ColorConvertFloat4ToU32(circleColor));

            DrawWatermark();

            if (enableESP)
            {
                foreach (var entity in entities)
                {
                    if (!EntityOnScreen(entity)) continue;
                    if (enableChams) DrawChams(entity);
                    DrawHealthBar(entity);
                    DrawBox(entity);
                    DrawLine(entity);
                    if (espDrawBones) DrawBones(entity);
                    DrawName(entity, 20);
                }
            }
        }

        // ── Chams — 3-pass depth-shaded capsules ──────────────────────────
        private void DrawChams(Entity entity)
        {
            if (entity.bones2d == null || entity.bones2d.Count < 2) return;

            bool isTeam = localPlayer.team == entity.team;
            Vector4 baseCol = isTeam ? chamsTeamColor : chamsEnemyColor;

            Vector4 shadowCol = new Vector4(
                baseCol.X * 0.15f, baseCol.Y * 0.15f, baseCol.Z * 0.15f,
                baseCol.W * chamsAlpha * 0.75f);
            Vector4 fillCol = new Vector4(
                baseCol.X, baseCol.Y, baseCol.Z, baseCol.W * chamsAlpha);
            Vector4 highlightCol = new Vector4(
                Math.Min(1f, baseCol.X * 1.6f + 0.3f),
                Math.Min(1f, baseCol.Y * 1.6f + 0.3f),
                Math.Min(1f, baseCol.Z * 1.6f + 0.3f),
                baseCol.W * chamsAlpha * 0.65f);
            Vector4 rimCol = new Vector4(
                Math.Min(1f, baseCol.X + 0.35f),
                Math.Min(1f, baseCol.Y + 0.35f),
                Math.Min(1f, baseCol.Z + 0.35f),
                Math.Min(1f, baseCol.W * (chamsAlpha + 0.2f)));

            uint uShadow = ImGui.ColorConvertFloat4ToU32(shadowCol);
            uint uFill = ImGui.ColorConvertFloat4ToU32(fillCol);
            uint uHighlight = ImGui.ColorConvertFloat4ToU32(highlightCol);
            uint uRim = ImGui.ColorConvertFloat4ToU32(rimCol);

            float distScale = Math.Clamp(1f - entity.distance / 3500f, 0.25f, 1f);
            float baseR = chamsThickness * distScale;

            foreach (var (a, b, rScale) in ChamsBones)
            {
                if (!BoneOk(entity.bones2d, a) || !BoneOk(entity.bones2d, b)) continue;
                Vector2 pA = entity.bones2d[a];
                Vector2 pB = entity.bones2d[b];
                if (!OnScreen(pA) || !OnScreen(pB)) continue;

                float r = baseR * rScale;

                DrawCapsuleFilled(pA, pB, r * 1.35f, uShadow);
                DrawCapsuleFilled(pA, pB, r, uFill);

                Vector2 lightOff = Vector2.Normalize(new Vector2(-1f, -1f)) * (r * 0.35f);
                DrawCapsuleFilled(pA + lightOff, pB + lightOff, r * 0.38f, uHighlight);

                drawList.AddCircle(pA, r, uRim, 0, 1.5f);
                drawList.AddCircle(pB, r, uRim, 0, 1.5f);
            }

            Vector2 headPos = BoneOk(entity.bones2d, 6) ? entity.bones2d[6] : entity.head2d;
            if (OnScreen(headPos))
            {
                float headR = baseR * 1.6f;
                drawList.AddCircleFilled(headPos, headR * 1.3f, uShadow);
                drawList.AddCircleFilled(headPos, headR, uFill);
                Vector2 specOff = new Vector2(-headR * 0.3f, -headR * 0.3f);
                drawList.AddCircleFilled(headPos + specOff, headR * 0.42f, uHighlight);
                drawList.AddCircle(headPos, headR, uRim, 0, 1.8f);
            }
        }

        private void DrawCapsuleFilled(Vector2 a, Vector2 b, float r, uint col)
        {
            drawList.AddCircleFilled(a, r, col);
            drawList.AddCircleFilled(b, r, col);
            Vector2 dir = b - a;
            float len = dir.Length();
            if (len < 0.001f) return;
            Vector2 perp = new Vector2(-dir.Y, dir.X) / len;
            Vector2 p0 = a + perp * r, p1 = a - perp * r;
            Vector2 p2 = b - perp * r, p3 = b + perp * r;
            drawList.AddTriangleFilled(p0, p1, p2, col);
            drawList.AddTriangleFilled(p0, p2, p3, col);
        }

        private bool OnScreen(Vector2 p) =>
            p.X >= 0 && p.Y >= 0 && p.X <= screenSize.X && p.Y <= screenSize.Y;

        // ── Splash ────────────────────────────────────────────────────────
        private void DrawSplash(float alpha)
        {
            var dl = ImGui.GetForegroundDrawList();
            Vector2 center = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);

            dl.AddRectFilled(Vector2.Zero, screenSize,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0f, 0.08f, alpha * 0.97f)));
            dl.AddCircleFilled(center, 160f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.10f, 0.85f, alpha * 0.18f)));
            dl.AddCircleFilled(center, 110f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.40f, 0.05f, 0.65f, alpha * 0.22f)));

            uint lc = ImGui.ColorConvertFloat4ToU32(new Vector4(0.60f, 0.15f, 0.90f, alpha * 0.55f));
            dl.AddLine(new Vector2(center.X - 220f, center.Y - 48f),
                       new Vector2(center.X + 220f, center.Y - 48f), lc, 1.2f);
            dl.AddLine(new Vector2(center.X - 220f, center.Y + 52f),
                       new Vector2(center.X + 220f, center.Y + 52f), lc, 1.2f);

            const float ts = 3.8f; const string title = "y018client";
            Vector2 tsz = ImGui.CalcTextSize(title) * ts;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * ts,
                center - tsz / 2f + new Vector2(3f, 3f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0f, 0.40f, alpha * 0.80f)), title);
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * ts,
                center - tsz / 2f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.88f, 0.60f, 1f, alpha)), title);

            const float ss = 1.4f; const string sub = "version 1.0";
            Vector2 ssz = ImGui.CalcTextSize(sub) * ss;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * ss,
                new Vector2(center.X - ssz.X / 2f, center.Y + tsz.Y / 2f + 12f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.70f, 0.40f, 0.95f, alpha * 0.85f)), sub);

            string dots = new string('.', (int)(_splashTimer.Elapsed.TotalSeconds * 2.5) % 4);
            Vector2 dsz = ImGui.CalcTextSize(dots);
            dl.AddText(new Vector2(center.X - dsz.X / 2f, center.Y + tsz.Y / 2f + 46f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.60f, 0.30f, 0.80f, alpha * 0.70f)), dots);
        }

        // ── Watermark ─────────────────────────────────────────────────────
        private void DrawWatermark()
        {
            const string text = "y018 client";
            uint tCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.78f, 0.30f, 1.00f, 1.00f));
            uint bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.02f, 0.14f, 0.80f));
            uint bCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.15f, 0.80f, 0.90f));
            Vector2 pad = new Vector2(8f, 4f);
            Vector2 pos = new Vector2(10f, 10f);
            Vector2 sz = ImGui.CalcTextSize(text);
            drawList.AddRectFilled(pos - pad, pos + sz + pad, bgCol, 5f);
            drawList.AddRect(pos - pad, pos + sz + pad, bCol, 5f, ImDrawFlags.None, 1.5f);
            drawList.AddText(pos, tCol, text);
        }

        // ── Purple theme ──────────────────────────────────────────────────
        private static void ApplyPurpleStyle()
        {
            var style = ImGui.GetStyle();
            var c = style.Colors;
            c[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.02f, 0.14f, 0.94f);
            c[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.03f, 0.16f, 0.80f);
            c[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.03f, 0.16f, 0.95f);
            c[(int)ImGuiCol.Border] = new Vector4(0.55f, 0.15f, 0.80f, 0.55f);
            c[(int)ImGuiCol.BorderShadow] = new Vector4(0f, 0f, 0f, 0f);
            c[(int)ImGuiCol.FrameBg] = new Vector4(0.18f, 0.05f, 0.28f, 0.85f);
            c[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.30f, 0.10f, 0.45f, 0.90f);
            c[(int)ImGuiCol.FrameBgActive] = new Vector4(0.40f, 0.15f, 0.60f, 1.00f);
            c[(int)ImGuiCol.TitleBg] = new Vector4(0.12f, 0.03f, 0.20f, 1.00f);
            c[(int)ImGuiCol.TitleBgActive] = new Vector4(0.38f, 0.08f, 0.60f, 1.00f);
            c[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.12f, 0.03f, 0.20f, 0.75f);
            c[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.03f, 0.18f, 1.00f);
            c[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.06f, 0.01f, 0.10f, 0.85f);
            c[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.42f, 0.10f, 0.65f, 0.80f);
            c[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.55f, 0.18f, 0.80f, 1.00f);
            c[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.70f, 0.28f, 0.95f, 1.00f);
            c[(int)ImGuiCol.CheckMark] = new Vector4(0.85f, 0.40f, 1.00f, 1.00f);
            c[(int)ImGuiCol.SliderGrab] = new Vector4(0.60f, 0.18f, 0.88f, 0.90f);
            c[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.78f, 0.32f, 1.00f, 1.00f);
            c[(int)ImGuiCol.Button] = new Vector4(0.28f, 0.07f, 0.46f, 0.85f);
            c[(int)ImGuiCol.ButtonHovered] = new Vector4(0.46f, 0.13f, 0.70f, 1.00f);
            c[(int)ImGuiCol.ButtonActive] = new Vector4(0.62f, 0.22f, 0.88f, 1.00f);
            c[(int)ImGuiCol.Header] = new Vector4(0.32f, 0.07f, 0.52f, 0.80f);
            c[(int)ImGuiCol.HeaderHovered] = new Vector4(0.46f, 0.13f, 0.70f, 0.90f);
            c[(int)ImGuiCol.HeaderActive] = new Vector4(0.62f, 0.22f, 0.88f, 1.00f);
            c[(int)ImGuiCol.Separator] = new Vector4(0.45f, 0.12f, 0.68f, 0.60f);
            c[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.60f, 0.20f, 0.85f, 0.80f);
            c[(int)ImGuiCol.SeparatorActive] = new Vector4(0.75f, 0.30f, 1.00f, 1.00f);
            c[(int)ImGuiCol.ResizeGrip] = new Vector4(0.48f, 0.12f, 0.72f, 0.40f);
            c[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.62f, 0.22f, 0.88f, 0.70f);
            c[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.78f, 0.32f, 1.00f, 1.00f);
            c[(int)ImGuiCol.Tab] = new Vector4(0.18f, 0.05f, 0.30f, 0.85f);
            c[(int)ImGuiCol.TabHovered] = new Vector4(0.50f, 0.15f, 0.75f, 0.90f);
            c[(int)ImGuiCol.TabActive] = new Vector4(0.38f, 0.10f, 0.62f, 1.00f);
            c[(int)ImGuiCol.TabUnfocused] = new Vector4(0.12f, 0.03f, 0.20f, 0.85f);
            c[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.25f, 0.07f, 0.40f, 1.00f);
            c[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.88f, 1.00f, 1.00f);
            c[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.38f, 0.60f, 1.00f);
            c[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.45f, 0.12f, 0.68f, 0.45f);
            c[(int)ImGuiCol.NavHighlight] = new Vector4(0.75f, 0.30f, 1.00f, 1.00f);
            style.WindowRounding = 7f; style.FrameRounding = 4f;
            style.GrabRounding = 4f; style.ScrollbarRounding = 5f;
            style.TabRounding = 4f;
            style.FramePadding = new Vector2(8f, 4f);
            style.ItemSpacing = new Vector2(8f, 6f);
            style.WindowPadding = new Vector2(12f, 10f);
            style.WindowBorderSize = 1f; style.FrameBorderSize = 0f;
        }

        // ── ESP helpers ───────────────────────────────────────────────────

        bool EntityOnScreen(Entity entity)
        {
            Vector2 p = entity.position2D, h = entity.head2d;
            if (p.X < 0 || p.Y < 0 || h.X < 0 || h.Y < 0) return false;
            if (p.X > screenSize.X || p.Y > screenSize.Y ||
                h.X > screenSize.X || h.Y > screenSize.Y) return false;
            return true;
        }

        private static readonly (int A, int B)[] BoneSegments =
        {
            (0,5),(5,6),(5,8),(8,9),(9,11),
            (5,16),(16,14),(14,17),
            (0,23),(23,24),(0,26),(26,27),
        };

        private void DrawBones(Entity entity)
        {
            uint col = ImGui.ColorConvertFloat4ToU32(boneColor);
            float t = boneThickness / MathF.Max(entity.distance, 1f);
            if (entity.bones2d?.Count > 6)
            {
                foreach (var (a, b) in BoneSegments) BoneLine(entity.bones2d, a, b, col, t);
                if (BoneOk(entity.bones2d, 6)) drawList.AddCircle(entity.bones2d[6], 4f + t, col);
                else drawList.AddCircle(entity.head2d, 4f + t, col);
                return;
            }
            drawList.AddLine(entity.position2D, entity.viewPosition2D, col, t * 0.85f);
            drawList.AddLine(entity.viewPosition2D, entity.head2d, col, t);
            drawList.AddCircle(entity.head2d, 4f + t, col);
        }

        private static bool BoneOk(IReadOnlyList<Vector2> b, int i)
        {
            if (i < 0 || i >= b.Count) return false;
            return b[i].X >= 0f && b[i].Y >= 0f;
        }

        private void BoneLine(IReadOnlyList<Vector2> bones, int a, int b, uint col, float t)
        {
            if (!BoneOk(bones, a) || !BoneOk(bones, b)) return;
            Vector2 pa = bones[a], pb = bones[b];
            if (pa.X > screenSize.X || pa.Y > screenSize.Y ||
                pb.X > screenSize.X || pb.Y > screenSize.Y) return;
            drawList.AddLine(pa, pb, col, t);
        }

        private void DrawHealthBar(Entity entity)
        {
            float h = entity.position2D.Y - entity.viewPosition2D.Y;
            float l = entity.viewPosition2D.X - h / 3;
            float bw = 0.05f * (entity.position2D.X + h / 3 - l);
            drawList.AddRectFilled(
                new Vector2(l - bw, entity.position2D.Y - h * (entity.health / 100f)),
                new Vector2(l, entity.position2D.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)));
        }

        private void DrawName(Entity entity, int yOff)
        {
            if (!enableName) return;
            drawList.AddText(
                new Vector2(entity.viewPosition2D.X, entity.viewPosition2D.Y - yOff),
                ImGui.ColorConvertFloat4ToU32(nameColor), entity.name);
        }

        private void DrawBox(Entity entity)
        {
            float h = entity.position2D.Y - entity.viewPosition2D.Y;
            Vector4 col = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddRect(
                new Vector2(entity.viewPosition2D.X - h / 3, entity.viewPosition2D.Y),
                new Vector2(entity.position2D.X + h / 3, entity.position2D.Y),
                ImGui.ColorConvertFloat4ToU32(col));
        }

        private void DrawLine(Entity entity)
        {
            Vector4 col = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddLine(
                new Vector2(screenSize.X / 2, screenSize.Y),
                entity.position2D,
                ImGui.ColorConvertFloat4ToU32(col));
        }

        public void UpdateEntities(IEnumerable<Entity> e)
            => entities = new ConcurrentQueue<Entity>(e);

        public void UpdateLocalPlayer(Entity e)
        { lock (entityLock) { localPlayer = e; } }
    }
}