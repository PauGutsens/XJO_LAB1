# Unity Multiplayer Lobby & Chat (Deliverable 2)

A custom C# networking solution built for Unity using standard system sockets over both **TCP** and **UDP** protocols.

## Features

### Core Requirements (Mandatory)
* **Create & Join Game:** Host servers and connect clients using IP address and player username.
* **Waiting Room:** Real-time synchronized player list updated on join and leave events.
* **Global Chat:** Text messaging system broadcasted to all connected clients.
* **Dual Protocol Support:** Full functionality available in both TCP and UDP modes.
* **UDP Timeout Detection:** Automatic disconnect handling via periodic heartbeat/pings.

### Bonus Features
* **Host-Player Integration:** Server host participates in the lobby as an active player.
* **Synchronized Scene Switch:** Host can trigger a scene transition (`S_Deliverable2` -> `S_Game`) for all connected clients.
* **Room Capacity Limits:** Prevents connections beyond the specified maximum player count (`FULL:` response).
* **Player Kick System:** Host can remove specific players from the session (`KICK:` command).
* **RTT / Ping Display:** Calculates and displays latency per player in milliseconds.

## Project Structure & Setup

### Scenes Configuration (Build Settings)
1. **Index 0:** `Assets/Scenes/S_Deliverable2.unity` (Lobby UI)
2. **Index 1:** `Assets/Scenes/S_Game.unity` (In-Game Scene)

### Network Protocol Summary

| Message | Direction | Description |
| :--- | :--- | :--- |
| `JOIN:<Name>` | Client -> Server | Connection request |
| `PLAYERS:<List>` | Server -> All | Player list broadcast |
| `CHAT:<Text>` | Client -> Server | Outgoing chat line |
| `CHAT:<Name>: <Text>` | Server -> All | Relayed chat line |
| `LEAVE:` | Client -> Server | Graceful disconnect |
| `FULL:` | Server -> Client | Room at max capacity |
| `START:` | Host -> All | Scene transition trigger |
| `KICK:<Name>` | Host -> Server | Player kick request |
| `KICKED:` | Server -> Client | Kick notification to client |
| `LAT:` / `LATR:` | Client <-> Server | Latency measurement (RTT) |