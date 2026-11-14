# Long Distance Play 🌐

A mod that bridges "Couch Co-op" from LAN to WAN, making it possible to play anywhere, on-the-go.

Seamlessly bridges [CouchCoop](https://steamcommunity.com/sharedfiles/filedetails/?id=3799476240) with **Cloudflare Tunnels (`cloudflared`)** to provide instant, zero-configuration global WAN access without port forwarding or VPNs.

---

## Features

- **Worldwide Access via Cloudflare Quick Tunnels:** Outbound HTTPS/WSS tunnel (`*.trycloudflare.com`) generated instantly. No account, no credit card, no port-forwarding required.
- **In-Game Lobby HUD:** Sleek overlay on character select and multiplayer load screens displaying tunnel status, live URL, and one-click actions.
- **Clipboard Sync:** Copies the public join link to your clipboard automatically on launch.
- **Interactive QR Code Modal:** Displays high-contrast in-game QR code for remote friends or streamers on mobile devices.
- **Auto Binary Downloader:** Ships with or automatically fetches the official `cloudflared.exe` binary.
- **Clean Process Lifecycle:** Spawns and kills `cloudflared.exe` with the game/lobby to eliminate orphan background tasks.
- **Custom Vanity Domain Support:** Optionally enter a Cloudflare Tunnel Token in `config.cfg` for persistent custom domains (e.g., `sts2.yourdomain.com`).

---

## How It Works

```
Host PC (Slay the Spire 2)
  ├── CouchCoop Mod (Embedded WebSocket Server: localhost:13337)
  ├── STS2-LongDistancePlay (Harmony + Godot UI Engine)
  └── cloudflared.exe (Child Process)
         │
         ▼ (Encrypted Outbound Tunnel)
  Cloudflare Anycast Global Edge (https://<random>.trycloudflare.com)
         │
         ▼ (HTTPS / WSS)
  Remote Players (Safari / Chrome / Firefox on iOS, Android, Mac, PC)
```

---

## Installation & Usage

1. Ensure **CouchCoop** is installed in `Slay the Spire 2/mods/couchcoop`.
2. Extract **STS2-LongDistancePlay** into `Slay the Spire 2/mods/STS2-LongDistancePlay`.
3. Launch Slay the Spire 2.
4. Host a multiplayer run -> On the Character Select screen, look at the top-right overlay.
5. Click **Copy Link** or open the **QR Code** and share it with your remote players.
6. Friends open the URL in their browser, select their character, and play!

---

## Configuration (`config.cfg`)

Created automatically next to `STS2-LongDistancePlay.dll`:

```json
{
  "AutoStart": true,
  "AutoCopyUrl": true,
  "TargetPort": 13337,
  "TunnelToken": "",
  "EnableInGameOverlay": true
}
```

- `AutoStart`: Automatically start the Cloudflare tunnel upon entering the multiplayer lobby.
- `AutoCopyUrl`: Automatically copy the public link to the host clipboard when ready.
- `TargetPort`: Local port served by CouchCoop (default `13337`).
- `TunnelToken`: Optional named Cloudflare tunnel token for persistent domains.
- `EnableInGameOverlay`: Toggles the in-game HUD panel.

---

## Building from Source

Requirements:
- .NET 9.0 SDK
- Windows x64

```powershell
dotnet build -c Release
```
Mod files will compile and auto-deploy to your local Steam STS2 installation.
