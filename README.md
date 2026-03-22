# CS2 Aimbot — y018 client

A basic, non-bypassing cheat for Counter-Strike 2 (CS2) featuring ESP and a smoothing aimbot, built for educational and personal use.

## ⚠️ Disclaimer

This project is intended for **educational purposes only**. Use of cheating software in online multiplayer games violates Valve's [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) and CS2's terms of service. This tool is **not designed to bypass VAC (Valve Anti-Cheat)** or any other anti-cheat system, and using it on official servers will likely result in a permanent ban.

**Use only in offline mode, private servers, or LAN environments.**

---

## Features

### ESP
- Player boxes and lines
- Skeleton / bone rendering with adjustable thickness
- Player name tags
- Health bars
- Configurable team and enemy colors

### Smoothing Aimbot
- Smooth, natural-feeling aim assistance — no snapping
- Configurable FOV radius (pixel-based)
- Adjustable smoothing factor (lower = smoother)
- Target lock with hysteresis to prevent constant target switching
- Optional teammate aim
- Visual FOV circle overlay

### UI
- Purple-themed ImGui overlay
- **Right Shift** — show menu and tab out of the game
- **Escape** — hide menu and return to game
- Settings saved automatically to `y018_settings.json`
- Splash screen on launch

---

## Installation & Usage

1. Head to the [**Releases**](../../releases) tab
2. Download the latest `.exe` file
3. Run the `.exe` as Administrator
4. Launch CS2
5. Press **Right Shift** in-game to open the menu

---

## Configuration

All settings are adjustable from the in-game overlay and saved automatically. Key binds:

| Key | Action |
|---|---|
| Right Shift | Show menu / tab out of game |
| Escape | Hide menu / return to game |
| Mouse Button 5 | Activate aimbot |

---

## How It Works

The tool reads CS2's process memory to get enemy player positions and projects them onto the screen for ESP. The aimbot calculates the angle to the target's head and smoothly interpolates the view angle over time — no raw snapping — using CS2's view angle write offset.

---

## Legal & Ethical Notice

- Do **not** use this on Valve official servers, FACEIT, ESEA, or any other competitive platform.
- The developer(s) are **not responsible** for any bans, account suspensions, or other consequences from misuse.
- This project exists purely for learning about game memory reading, input simulation, and software development.

---

## License

[MIT License](LICENSE) — see the LICENSE file for details.
