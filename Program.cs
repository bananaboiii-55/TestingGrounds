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
    const int HOTKEY = 0x06; // Mouse button 5
    static Swed swed = new Swed("cs2");
    static IntPtr lockedAimPawn = IntPtr.Zero;

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

        int dwEntityList = 0x24AF268;
        int dwViewMatrix = 0x230FF20;
        int dwLocalPlayerPawn = 0x2069B50;
        int dwViewAngles = 0x231A648;

        int m_vOldOrigin = 0x1588;
        int m_iTeamNum = 0x3F3;
        int m_lifeState = 0x35C;
        int m_hPlayerPawn = 0x90C;
        int m_vecViewOffset = 0xD58;
        int m_modelState = 0x160;
        int m_pGameSceneNode = 0x338;
        int m_iHealth = 0x354;
        int m_iszPlayerName = 0x6F8;

        while (true)
        {
            Vector2 screenSize = renderer.screenSize;
            entities.Clear();

            IntPtr entityList = swed.ReadPointer(client, dwEntityList);
            if (entityList == IntPtr.Zero)
            {
                Thread.Sleep(50);
                continue;
            }

            IntPtr listEntry = swed.ReadPointer(entityList, 0x10);
            if (listEntry == IntPtr.Zero)
            {
                Thread.Sleep(50);
                continue;
            }
            IntPtr localPawn = swed.ReadPointer(client, dwLocalPlayerPawn);
            if (localPawn == IntPtr.Zero)
            {
                Thread.Sleep(50);
                continue;
            }

            localPlayer.team = swed.ReadInt(localPawn, m_iTeamNum);
            localPlayer.position = swed.ReadVec(localPawn, m_vOldOrigin);
            localPlayer.pawnAdress = localPawn;
            localPlayer.view = swed.ReadVec(localPawn, m_vecViewOffset);

            float[] viewMatrix = swed.ReadMatrix(client + dwViewMatrix);

            const float maxEspDistance = 6000f;

            for (int i = 0; i < 64; i++)
            {
                IntPtr controller = swed.ReadPointer(listEntry, i * 0x70);
                if (controller == IntPtr.Zero) continue;

                int pawnHandle = swed.ReadInt(controller, m_hPlayerPawn);
                if (pawnHandle == 0) continue;

                IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
                if (listEntry2 == IntPtr.Zero) continue;

                IntPtr pawn = swed.ReadPointer(listEntry2, 0x70 * (pawnHandle & 0x1FF));
                if (pawn == IntPtr.Zero || pawn == localPlayer.pawnAdress) continue;

                int lifeState = swed.ReadInt(pawn, m_lifeState);
                if (lifeState != 256)
                    continue;

                int team = swed.ReadInt(pawn, m_iTeamNum);
                int health = swed.ReadInt(pawn, m_iHealth);
                if (health < 1 || health > 100)
                    continue;

                IntPtr sceneNode = swed.ReadPointer(pawn, m_pGameSceneNode);
                if (sceneNode == IntPtr.Zero)
                    continue;

                IntPtr boneMatrix = Calculate.ResolveBoneArray(sceneNode, swed, m_modelState);

                Vector3 origin = swed.ReadVec(pawn, m_vOldOrigin);
                if (origin.LengthSquared() < 1f)
                    continue;
                Vector3 viewOff = swed.ReadVec(pawn, m_vecViewOffset);
                Vector3 eye = origin + viewOff;

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
                    bones = Calculate.ReadBones(boneMatrix, swed)
                };

                if (entity.distance > maxEspDistance)
                    continue;

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

                entity.pixelDistance = Vector2.Distance(entity.head2d, new Vector2(screenSize.X / 2, screenSize.Y / 2));

                entities.Add(entity);
            }

            entities = entities.OrderBy(e => e.pixelDistance).ToList();

            bool aimKey = GetAsyncKeyState(HOTKEY) < 0 && renderer.aimbot;
            if (!aimKey)
                lockedAimPawn = IntPtr.Zero;
            else if (entities.Count > 0)
            {
                var inFov = entities
                    .Where(e => ((e.team & 0xFF) != (localPlayer.team & 0xFF) || renderer.aimOnTeam) && e.pixelDistance < renderer.FOV)
                    .OrderBy(e => e.pixelDistance)
                    .ToList();
                if (inFov.Count == 0)
                    lockedAimPawn = IntPtr.Zero;
                else
                {
                    Entity closest = inFov[0];
                    Entity? lockedEnt = inFov.FirstOrDefault(e => e.pawnAdress == lockedAimPawn);

                    if (lockedAimPawn == IntPtr.Zero || lockedEnt == null)
                        lockedAimPawn = closest.pawnAdress;
                    else if (closest.pawnAdress != lockedAimPawn
                            && closest.pixelDistance < lockedEnt.pixelDistance - renderer.aimSwitchHysteresis)
                        lockedAimPawn = closest.pawnAdress;

                    Entity target = inFov.FirstOrDefault(e => e.pawnAdress == lockedAimPawn) ?? closest;
                    if (target.pawnAdress != lockedAimPawn)
                        lockedAimPawn = target.pawnAdress;

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
}
