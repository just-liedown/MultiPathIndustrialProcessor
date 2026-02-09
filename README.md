# Multi-Path Industrial Processor (SMAPI mod)

A WIP “industrial-grade” farm building that merges multiple vanilla processors into one station using **spatial multi-port input** and a **unified output**.

## Features (current)

- Buildable **farm building**: `Multi-Path Industrial Processor` (in Robin's build menu).
- **6 input ports** (bottom), each mapped to a processing module:
  - Animal products (mayo/cheese)
  - Brewing (keg)
  - Preserves (jar)
  - Smelting (ore/quartz -> bars/refined quartz)
  - Wool (loom)
  - Oil (oil maker)
- **Unified output** (top): opens **vanilla chest UI** for collecting products.
- **Fuel system**: left-side coal port adds to a global fuel pool; **smelting consumes fuel** and pauses if fuel is insufficient.
- **Capacity = parallelism**: each module starts with capacity **8** (no queue; full => reject input).

## Controls / ports

All interactions use **player standing tile** (not mouse cursor).

- Bottom (6 ports): start a job if the held item is accepted by that module.
- Top (1 port): open output chest (vanilla chest UI).
- Left side: coal port (accepts coal only).
- Right side: management terminal (not implemented yet; logs only).

## Status / known limitations

- The management terminal UI and upgrades (8→16→32→64) are not wired up yet.
- Building cost/time are currently **test values** (0g, 1 wood, 1 day).
- Recipes/rules aren’t fully configurable yet (most modules reuse vanilla machine logic; smelting uses a small fixed table).

## Development

### Requirements

- Stardew Valley 1.6+
- SMAPI 4.0+

### Local .NET SDK (repo-only)

```bash
./scripts/install-dotnet.sh
```

### Configure game path (if autodetect fails)

```bash
cp stardewvalley.targets.example stardewvalley.targets
```

Edit `stardewvalley.targets` and set `<GamePath>` to your Stardew Valley install folder.

### Build & deploy

```bash
./scripts/doctor.sh
./scripts/restore.sh
./scripts/build.sh
```

If the game is running, Windows may lock `*.dll` and deployment can fail. In that case:

- close the game and rerun `./scripts/build.sh`, or
- build only (no deploy): `./scripts/build-nodeploy.sh`
