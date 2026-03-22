# CS2 Aimbot

A basic, non-bypassing aimbot for Counter-Strike 2 (CS2) built for educational and personal use.

## ⚠️ Disclaimer

This project is intended for **educational purposes only**. Use of cheating software in online multiplayer games violates Valve's [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) and CS2's terms of service. This aimbot is **not designed to bypass VAC (Valve Anti-Cheat)** or any other anti-cheat system, and using it on official servers will likely result in a permanent ban.

**Use only in offline mode, private servers, or LAN environments.**

---

## What Is This?

This is a simple aimbot for CS2 that automatically assists with aiming at enemy players. It is non-bypassing, meaning:

- It makes **no attempt** to evade or circumvent VAC or any other anti-cheat software.
- It is **not designed for use on official matchmaking servers**.
- It operates by reading game memory to locate enemy positions and adjusting mouse input accordingly.

---

## Features

- Basic aim assistance toward the nearest visible enemy
- Configurable field of view (FOV) radius
- Configurable smoothing to control how quickly the aim snaps
- Toggle on/off keybind support
- Head or body targeting options

---

## Requirements

- Windows 10/11
- Counter-Strike 2 (Steam)
- [List any runtime dependencies here, e.g. Python 3.x, .NET, etc.]

---

## Installation

1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/cs2-aimbot.git
   cd cs2-aimbot
   ```

2. Install dependencies:
   ```bash
   [insert install command here]
   ```

3. Run the program:
   ```bash
   [insert run command here]
   ```

---

## Configuration

Edit the `config.json` (or equivalent config file) to adjust settings:

| Setting | Description | Default |
|---|---|---|
| `fov` | Aim assist radius in degrees | `10` |
| `smoothing` | Aim movement smoothing factor | `5` |
| `target_bone` | Target bone (`head` or `body`) | `head` |
| `toggle_key` | Key to enable/disable aimbot | `Insert` |

---

## How It Works

The aimbot reads CS2's process memory to retrieve the positions of enemy players in the game world. It then projects those 3D world coordinates onto the 2D screen and moves the mouse toward the target using simulated input — no game file modification is involved.

---

## Legal & Ethical Notice

- Do **not** use this on Valve official servers, FACEIT, ESEA, or any other competitive platform.
- The developer(s) of this project are **not responsible** for any bans, account suspensions, or other consequences resulting from misuse.
- This project exists purely for learning about game memory reading, input simulation, and software development.

---

## Contributing

Pull requests are welcome for bug fixes or improvements. Please open an issue first to discuss any major changes.

---

## License

[MIT License](LICENSE)
Readme written by Claude
