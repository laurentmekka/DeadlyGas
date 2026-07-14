# Deadly Gas — realistic end-of-raid (SPT 4.0.x / EFT 0.16.9)

Replaces the arbitrary end-of-timer MIA with a **deadly gas**: when the raid
timer expires, the zone slowly fills with gas (green haze + fog) and your
health drains — about 3 minutes to die at full health (configurable). You can
still run for the extract: extracting during the gas counts as a normal
**Survived**. Dying in the gas is a normal death. No more MIA screen at
2 meters from the extract.

**Client-side** BepInEx plugin. Nothing to install server-side.

## Install (users)

Grab the release zip and extract it into your SPT folder (the one with
`SPT.Launcher.exe`). The DLL lands in `BepInEx/plugins/MekkaDeadlyGas.dll`.

## Build (from source)

Requires the .NET SDK 9+. Then:

```
build.bat [path-to-SPT]     (default: D:\SPT4.0)
```

This compiles against the DLLs of YOUR SPT install (`BepInEx/core` and
`EscapeFromTarkov_Data/Managed`) and copies the plugin into
`BepInEx/plugins/`. No game assemblies are included in this repository.

## Configuration

In game via **F12** (Configuration Manager) or in
`BepInEx/config/com.mekka.deadlygas.cfg`:

| Setting | Default | Effect |
|---|---|---|
| Enabled | on | Master switch |
| Minutes before death | 3 | Survival time in the gas at full health (chest-based) |
| Gas rise (seconds) | 20 | Grace period before damage starts |
| Visual effects | on | Green tint + fog |
| Tint intensity | 0.45 | Green overlay opacity (0.2 subtle → 0.7 pea soup) |
| Fog density | 0.06 | Fog thickness at full ramp |
| Probe mode | off | Diagnostic dumps for troubleshooting |

## How it works

Harmony prefix on `EndByTimerScenario.Update`. While the timer runs:
vanilla. When it expires: the session is silently **extended by 24h** (so
the game never fires the MIA path and extracts stay fully vanilla) and a
`GasController` MonoBehaviour starts — visual ramp, then Poison-type damage
distributed over all body parts, 1 tick/second, rate calibrated on the
chest pool so "minutes before death" is accurate at full health.

Every game type is resolved **via reflection** with fallbacks (fields and
properties, all `Singleton`1` candidates tried, etc.). If a future EFT
update renames internals, the mod logs it and disables itself cleanly —
it never breaks the raid.

## Known quirks

- After expiry the in-raid timer display shows ~24h instead of 00:00
  (cosmetic — the gas is your real timer now).
- AI Scavs are unaffected (gameplay choice: the map stays alive).
- Works in PMC and player-Scav raids. Untested with Fika/coop.

## Troubleshooting

Check `BepInEx/LogOutput.log` for `[DeadlyGas]` lines. If the patch fails
to apply or resolution errors appear: enable **Probe mode** (F12), run one
raid to timer end, and open an issue with your `LogOutput.log` attached —
the probe dumps the real type/member names of your game version.

## License

MIT — see [LICENSE](LICENSE).

---
