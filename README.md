# SeekerBeGone

BepInEx plugin for Valheim that hides Mistlands seekers (and ticks, seeker eggs,
seeker trophies) behind another creature's model and sounds. Gameplay is
untouched: colliders, AI, attacks and drops all stay the same; only rendering
and audio change.

## How it works

- `Character.Awake` postfix: for every prefab listed in `Targets`, disable the
  original renderers and audio, clone the `ReplacementCreature`'s `Visual`
  subtree onto the character, and mirror the original Animator's parameters
  onto the clone every frame (`AnimatorMirror`; attacks are detected by state
  tag or clip name and forwarded as the clone's first attack trigger).
- Death effects (which carry the ragdoll) are always swapped for the
  replacement creature's. With `ReplaceSounds`, hit/idle/footstep effects and
  the attack items' effect lists (both item-level and attack-level) are swapped
  too.
- `ZSFX.Awake` (or `Play`) postfix mutes any sound object whose name contains
  one of `MuteSoundKeywords`, as a catch-all.
- `ZNetView.Awake` postfix disables renderers on `HideObjects` (seeker eggs).
- `ObjectDB.Awake`/`CopyOtherDB` postfix rewrites the item prefabs listed in
  `ItemReplacements` in place: inventory icons and the mesh/materials under the
  item's `attach` child are taken from the replacement item, so ground drops,
  inventory and item stands all show it. Prefabs are persistent assets, so
  children cannot be added to them; fields are swapped instead.

## Build

Requires the .NET SDK and a Valheim install with BepInEx 5.

```
dotnet build -c Release
```

`ValheimDir` defaults to `E:\SteamLibrary\steamapps\common\Valheim`; override
with `dotnet build -c Release -p:ValheimDir=<path>`.

## Install

Copy `bin/Release/net472/SeekerBeGone.dll` to `<Valheim>/BepInEx/plugins/`.
The game must not be running (the DLL is locked while loaded).

Config is generated on first launch at
`<Valheim>/BepInEx/config/yutljp.seekerbegone.cfg`.

## Testing

`tests/run-harness.ps1` builds the mod and `tests/Harness` (a second BepInEx
plugin, only active when the game is started with `-sbgtest <outdir>`), drops
both DLLs into the plugins folder, and launches the game once:

- the window is kept off-screen and all audio is muted;
- saves are redirected to `tests/out/<timestamp>/savedir/` (throwaway world
  `SBGTest`, character `sbgtest`), so real saves are never touched;
- the main menu is skipped, every `Targets` prefab, `HideObjects` prefab and
  `ItemReplacements` item is spawned in front of the player, screenshots are
  taken alive and dead, diagnostics are logged (renderer/audio state, Animator
  parameters, attack item effects, item hierarchy, remaining `ZSFX`), and the
  game quits itself.

Results land in `tests/out/<timestamp>/` (`*.png`, `LogOutput.log`); the
harness DLL is removed from the plugins folder afterwards. Steam must be
running. Item stands are not covered by the harness.

## History

- 1.1.0 (2026-06-11): original build; the source was lost and reconstructed on
  2026-09-17 by decompiling the installed DLL.
- 1.2.0 (2026-09-17): verified in-game with the harness. Sound keyword matching
  (prefix matching never hit `sfx_*` names), attack effects on item-level
  lists and all item sources, attack animation forwarding, ragdoll swap
  independent of `ReplaceSounds`, `Destroy` instead of `DestroyImmediate`
  (egg hatching runs `Awake` inside a physics callback), trophy replacement.
  `Tick_stared` is not a prefab in Valheim 1.0.12 and was dropped from the
  defaults.
