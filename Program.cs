using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Swed64;
using WallhackandAimbotCombinedTest;

class Program
{
    const int HOTKEY = 0x06;  // Mouse button 5
    const int VK_SPACE = 0x20;
    const int VK_A = 0x41;
    const int VK_D = 0x44;

    static Swed swed = new Swed("cs2");
    static IntPtr lockedAimPawn = IntPtr.Zero;
    static Random rng = new Random();

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    static void Main()
    {
        IntPtr client = swed.GetModuleBase("client.dll");

        Renderer renderer = new Renderer();
        Thread renderThread = new Thread(() => renderer.Start().Wait());
        renderThread.Start();

        List<Entity> entities = new List<Entity>();
        Entity localPlayer = new Entity();

        // ── Offsets ───────────────────────────────────────────────────────
        int dwEntityList = 0x24AF268;
        int dwViewMatrix = 0x230FF20;
        int dwLocalPlayerPawn = 0x2069B50;
        int dwViewAngles = 0x231A648;

        // dwForceJump — writing 65537 here triggers a jump server-side.
        // This is the correct way to bhop externally in CS2.
        int dwForceJump = 0x1850DF0;

        int m_vOldOrigin = 0x1588;
        int m_iTeamNum = 0x3F3;
        int m_lifeState = 0x35C;
        int m_hPlayerPawn = 0x90C;
        int m_vecViewOffset = 0xD58;
        int m_modelState = 0x160;
        int m_pGameSceneNode = 0x338;
        int m_iHealth = 0x354;
        int m_iszPlayerName = 0x6F8;

        // m_fFlags: bit 0 = FL_ONGROUND
        int m_fFlags = 0x3F8;

        // m_entitySpottedState + m_bSpottedByMask
        // spotted mask is a uint — if any bit is set the local player has
        // spotted this entity (bit 0 = player slot 0, etc.)
        int m_entitySpottedState = 0x1638;
        int m_bSpottedByMask = 0xC;   // offset inside EntitySpottedState_t

        // m_vecVelocity — needed for air strafe magnitude check
        int m_vecVelocity = 0x118;

        bool bhopWasOnGround = false;

        while (true)
        {
            Vector2 screenSize = renderer.screenSize;
            entities.Clear();

            IntPtr entityList = swed.ReadPointer(client, dwEntityList);
            if (entityList == IntPtr.Zero) { Thread.Sleep(50); continue; }

            IntPtr listEntry = swed.ReadPointer(entityList, 0x10);
            if (listEntry == IntPtr.Zero) { Thread.Sleep(50); continue; }

            IntPtr localPawn = swed.ReadPointer(client, dwLocalPlayerPawn);
            if (localPawn == IntPtr.Zero) { Thread.Sleep(50); continue; }

            localPlayer.team = swed.ReadInt(localPawn, m_iTeamNum);
            localPlayer.position = swed.ReadVec(localPawn, m_vOldOrigin);
            localPlayer.pawnAdress = localPawn;
            localPlayer.view = swed.ReadVec(localPawn, m_vecViewOffset);

            float[] viewMatrix = swed.ReadMatrix(client + dwViewMatrix);
            const float maxEspDistance = 6000f;

            // ── Bunnyhop ──────────────────────────────────────────────────
            // Write dwForceJump = 65537 the moment we land while space held.
            // 65537 is the value CS2 expects to register a jump via the
            // force-jump ConVar — much more reliable than SendInput since
            // CS2 has its own input capture when fullscreened.
            if (renderer.bhopEnabled && (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0)
            {
                int flags = swed.ReadInt(localPawn, m_fFlags);
                bool onGround = (flags & 1) != 0;

                if (onGround && !bhopWasOnGround)
                {
                    // Trigger a jump
                    swed.WriteInt(client, dwForceJump, 65537);
                }
                else if (!onGround)
                {
                    // Reset the force jump value while airborne
                    swed.WriteInt(client, dwForceJump, 256);
                }

                bhopWasOnGround = onGround;
            }
            else
            {
                bhopWasOnGround = false;
            }

            // ── Air strafe ────────────────────────────────────────────────
            // Auto-strafes left/right while airborne to gain speed.
            // Reads current velocity, determines which direction adds speed
            // toward the movement direction, then holds that key.
            // Only fires when not grounded and strafe is enabled.
            if (renderer.airStrafeEnabled)
            {
                int flags = swed.ReadInt(localPawn, m_fFlags);
                bool onGround = (flags & 1) != 0;

                if (!onGround)
                {
                    Vector3 vel = swed.ReadVec(localPawn, m_vecVelocity);
                    Vector3 angles = swed.ReadVec(client, dwViewAngles);

                    float yawRad = angles.Y * MathF.PI / 180f;

                    // Forward/right unit vectors from current yaw
                    float fwdX = MathF.Cos(yawRad);
                    float fwdY = MathF.Sin(yawRad);
                    float rightX = fwdY;
                    float rightY = -fwdX;

                    // Dot velocity against right vector to decide strafe direction
                    float rightDot = vel.X * rightX + vel.Y * rightY;

                    // Strafe toward whichever side has less velocity build-up
                    // (opposite of current rightward velocity = adding speed)
                    bool strafeRight = rightDot < 0;

                    // Write a yaw nudge in the strafe direction to help
                    // the strafe gain speed. Very small to not be jarring.
                    float nudge = renderer.airStrafeStrength * (strafeRight ? 1f : -1f);
                    Vector3 newAngles = new Vector3(angles.X, angles.Y + nudge, angles.Z);
                    swed.WriteVec(client, dwViewAngles, newAngles);
                }
            }

            // ── Entity scan ───────────────────────────────────────────────
            for (int i = 0; i < 64; i++)
            {
                IntPtr controller = swed.ReadPointer(listEntry, i * 0x70);
                if (controller == IntPtr.Zero) continue;

                int pawnHandle = swed.ReadInt(controller, m_hPlayerPawn);
                if (pawnHandle == 0) continue;

                IntPtr listEntry2 = swed.ReadPointer(entityList,
                    0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
                if (listEntry2 == IntPtr.Zero) continue;

                IntPtr pawn = swed.ReadPointer(listEntry2, 0x70 * (pawnHandle & 0x1FF));
                if (pawn == IntPtr.Zero || pawn == localPlayer.pawnAdress) continue;

                if (swed.ReadInt(pawn, m_lifeState) != 256) continue;

                int team = swed.ReadInt(pawn, m_iTeamNum);
                int health = swed.ReadInt(pawn, m_iHealth);
                if (health < 1 || health > 100) continue;

                IntPtr sceneNode = swed.ReadPointer(pawn, m_pGameSceneNode);
                if (sceneNode == IntPtr.Zero) continue;

                IntPtr boneMatrix = Calculate.ResolveBoneArray(sceneNode, swed, m_modelState);
                Vector3 origin = swed.ReadVec(pawn, m_vOldOrigin);
                if (origin.LengthSquared() < 1f) continue;

                Vector3 viewOff = swed.ReadVec(pawn, m_vecViewOffset);
                Vector3 eye = origin + viewOff;

                // ── Spotted check using entity's own spotted state ────────
                // m_bSpottedByMask is a bitmask — any non-zero value means
                // at least one player has spotted this entity. Since we only
                // care about local player visibility we check bit 0 and bit 1
                // (slot-based), but any non-zero is a good enough signal.
                uint spottedMask = (uint)swed.ReadInt(pawn, m_entitySpottedState + m_bSpottedByMask);
                bool spotted = spottedMask != 0;

                Entity entity = new Entity
                {
                    pawnAdress = pawn,
                    controllerAdress = controller,
                    team = team,
                    health = health,
                    origin = origin,
                    view = viewOff,
                    distance = Vector3.Distance(origin, localPlayer.position),
                    name = swed.ReadString(controller, m_iszPlayerName, 32).Split("\0")[0],
                    bones = Calculate.ReadBones(boneMatrix, swed),
                    spotted = spotted,
                    // visible falls back to spotted — uses the game's own logic
                    visible = spotted,
                };

                if (entity.distance > maxEspDistance) continue;

                entity.position2D = Calculate.WorldToScreen(viewMatrix, origin, screenSize);
                entity.viewPosition2D = Calculate.WorldToScreen(viewMatrix, eye, screenSize);

                if (entity.bones.Count > 6)
                {
                    entity.head = entity.bones[6];
                    entity.head2d = Calculate.WorldToScreen(viewMatrix, entity.head, screenSize);
                }
                else
                {
                    entity.head = eye;
                    entity.head2d = entity.viewPosition2D;
                }

                entity.bones2d = new List<Vector2>();
                foreach (Vector3 b in entity.bones)
                    entity.bones2d.Add(Calculate.WorldToScreen(viewMatrix, b, screenSize));

                entity.pixelDistance = Vector2.Distance(
                    entity.head2d,
                    new Vector2(screenSize.X / 2, screenSize.Y / 2));

                entities.Add(entity);
            }

            entities = renderer.aimTargetClosestDistance
                ? entities.OrderBy(e => e.distance).ToList()
                : entities.OrderBy(e => e.pixelDistance).ToList();

            // ── Aimbot ────────────────────────────────────────────────────
            bool aimKey = GetAsyncKeyState(HOTKEY) < 0 && renderer.aimbot;

            if (!aimKey)
            {
                lockedAimPawn = IntPtr.Zero;
            }
            else if (entities.Count > 0)
            {
                var inFov = entities
                    .Where(e =>
                        ((e.team & 0xFF) != (localPlayer.team & 0xFF) || renderer.aimOnTeam)
                        && e.pixelDistance < renderer.FOV
                        && (!renderer.aimVisibleOnly || e.visible))
                    .ToList();

                if (inFov.Count == 0)
                {
                    lockedAimPawn = IntPtr.Zero;
                }
                else
                {
                    Entity closest = inFov[0];
                    Entity? lockedEnt = inFov.FirstOrDefault(e => e.pawnAdress == lockedAimPawn);

                    if (lockedAimPawn == IntPtr.Zero || lockedEnt == null)
                        lockedAimPawn = closest.pawnAdress;
                    else if (renderer.aimTargetClosestDistance)
                    {
                        if (closest.pawnAdress != lockedAimPawn &&
                            closest.distance < lockedEnt.distance - 50f)
                            lockedAimPawn = closest.pawnAdress;
                    }
                    else
                    {
                        if (closest.pawnAdress != lockedAimPawn &&
                            closest.pixelDistance < lockedEnt.pixelDistance - renderer.aimSwitchHysteresis)
                            lockedAimPawn = closest.pawnAdress;
                    }

                    Entity target = inFov.FirstOrDefault(e => e.pawnAdress == lockedAimPawn) ?? closest;
                    if (target.pawnAdress != lockedAimPawn) lockedAimPawn = target.pawnAdress;

                    if (target.pixelDistance < renderer.FOV)
                    {
                        Vector3 playerView = localPlayer.position + localPlayer.view;
                        Vector2 angles = Calculate.CalculateAngles(playerView, target.head);
                        Vector3 targetView = new Vector3(angles.Y, angles.X, 0);
                        Vector3 current = swed.ReadVec(client, dwViewAngles);

                        if (float.IsNaN(current.X) || float.IsNaN(current.Y)
                         || float.IsInfinity(current.X) || float.IsInfinity(current.Y))
                            current = targetView;

                        float smooth = Math.Clamp(renderer.aimSmooth, 0.02f, 1f);
                        float humanAmount = renderer.aimHumanisation;

                        if (humanAmount > 0f)
                        {
                            float dPitch = Math.Abs(NormalisePitch(targetView.X - current.X));
                            float dYaw = Math.Abs(NormaliseYaw(targetView.Y - current.Y));
                            float angularDist = MathF.Sqrt(dPitch * dPitch + dYaw * dYaw);

                            if (angularDist > 0.15f)
                            {
                                float jitterRange = smooth * 0.6f * humanAmount;
                                smooth = Math.Clamp(smooth + (float)(rng.NextDouble() * 2 - 1) * jitterRange, 0.02f, 1f);

                                if (rng.NextDouble() < humanAmount * 0.18f)
                                    smooth = Math.Clamp(smooth * 0.25f, 0.02f, 1f);

                                if (rng.NextDouble() < humanAmount * 0.35f)
                                {
                                    float maxDeflect = humanAmount * 2.5f * Math.Clamp(angularDist / 10f, 0.1f, 1f);
                                    targetView = new Vector3(
                                        targetView.X + (float)(rng.NextDouble() * 2 - 1) * maxDeflect,
                                        targetView.Y + (float)(rng.NextDouble() * 2 - 1) * maxDeflect, 0);
                                }

                                if (rng.NextDouble() < humanAmount * 0.12f)
                                {
                                    renderer.UpdateLocalPlayer(localPlayer);
                                    renderer.UpdateEntities(entities);
                                    Thread.Sleep(8);
                                    continue;
                                }
                            }
                        }

                        float pitch = Calculate.LerpPitch(current.X, targetView.X, smooth);
                        float yaw = Calculate.LerpAngleDegrees(current.Y, targetView.Y, smooth);
                        swed.WriteVec(client, dwViewAngles, new Vector3(pitch, yaw, 0));
                    }
                }
            }

            renderer.UpdateLocalPlayer(localPlayer);
            renderer.UpdateEntities(entities);
            Thread.Sleep(8);
        }
    }

    static float NormalisePitch(float d)
    {
        while (d > 90f) d -= 180f;
        while (d < -90f) d += 180f;
        return d;
    }

    static float NormaliseYaw(float d)
    {
        while (d > 180f) d -= 360f;
        while (d < -180f) d += 360f;
        return d;
    }
}