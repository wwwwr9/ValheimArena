# Valheim Arena

An arena sandbox for Valheim (BepInEx 5). Spawn any creatures — bosses included — with the stars, teams and
factions you want, fence off a battleground and watch the fight with a free or follow camera. A separate editor
lets you retune every creature: health, resistances, movement, AI senses and each of its attacks on its own.

> **Language.** The interface is in English (default) and Russian: switch it with the EN / RU buttons in the
> arena menu (F7) or with `Language` in the config. Documentation in Russian: [README.ru.md](README.ru.md).

## Installation

- **Mod manager:** ValheimArena on Thunderstore (r2modman, Thunderstore Mod Manager) — BepInExPack Valheim is
  installed automatically.
- **Manually:** install BepInExPack Valheim (https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), download `ValheimArena-<version>.zip` from Releases
  and extract it into the Valheim folder so that you get `Valheim/BepInEx/plugins/ValheimArena/ValheimArena.dll`.
- **From source:** see [Building](#building).

Everyone at the arena needs the same version of the mod.

## Features

- **Spawning.** Any creature of the game (and of other mods) in groups: count 1–200, stars 0–20, team 1–8,
  faction override, placement (team side / random / center / camera aim), spread and per-group behavior.
  The catalog filters by the game's own traits — bosses, flyers, water dwellers — and by faction, the grouping
  the game uses to decide who attacks whom. For the selected creature it shows how the game itself spawns it
  (biomes, pack size, stars, time of day, raids, nests, altar), and "As in the game" applies that pack size and
  those stars to the group. Rosters are spawned group by group or all at once; whole setups are saved as scenarios.
- **Who fights whom.** *Teams* (8×8 hostility matrix), *Factions* (faction × faction matrix, directional:
  bosses can attack regular creatures, and those can optionally fight back) or *Free for all*. Friendly fire,
  area attacks that hit creatures of the same kind (vanilla AoE spares them — troll vs troll needs this),
  isolation from wild creatures and other players, and damage that doesn't depend on players nearby (in the game
  each additional player within 100 m makes creatures 4% stronger and 30% tougher, in their fights with each other
  too; by default the arena uses single-player numbers).
- **Battleground.** Radius 5–150 m, circle or square, a visible ring with posts that follows the terrain.
  Leaving is free, blocked by a wall or sends the fighter back to the center; a ceiling and a cruise altitude
  for flyers.
- **Behavior.** *Hunt* — always knows where the enemies are and goes for them; *Hold* — stands still, turns
  and strikes whoever comes into range; *Free* — vanilla senses, wanders around its spawn point, bosses keep
  to it like to their altar. Creatures summoned by fighters (boss minions, spawns of attacks and projectiles)
  join their summoner's side; intruders can be ignored, drawn into the fight or removed.
- **Fight flow.** Countdown, auto start, fighters frozen until the start and optionally after the result,
  pause, slow motion ×0.1–×2 and time stop (single player).
- **Cameras.** Free camera; follow camera (orbit / chase / top-down) with target switching, auto-switch when
  the target dies and a "director" that keeps the camera on the hottest fight.
- **Your character.** Invisible, pass-through for creatures, collision off, ignored by creatures,
  invulnerable — in any combination; "Observer" and "Participant" presets.
- **Overlay and results.** Name tags in team colors, health bars, side scores, kill feed and a target card
  (the bars over fighters hide with F4, the whole overlay with F10);
  results with sides, damage, kills and who killed whom.
- **Creature editor (F8).** Health (with the per-star and NG+ preview), size, dealt/taken damage multipliers,
  stagger, regeneration; 11 resistances plus each weak spot's own; movement, flight and swimming; AI senses and
  pacing; every attack on its own — damage by type, range, cooldown, angle, knockback, status effect chance,
  hit zone by attack type, projectiles and effects with their own damage (boss AoE, explosions, puddles,
  ability spawns); badges show heals, buffs and mist- or dungeon-only attacks. Saved as named profiles and applied to a group or to live fighters. Only arena fighters are
  affected: each gets its own copy of the attack data, prefabs and the rest of the world stay vanilla.
- **Cleanup.** Creatures and corpses, items on the ground, tombstones, petrified gammeltrolls and fallen trees —
  each one "remove" or "keep".

## Controls

Keys can be changed in the config file.

| Key | Action |
|---|---|
| F7 | Arena menu |
| F8 | Creature editor |
| F9 | Camera: player → free → follow |
| F6 | Deploy the arena / start / pause / resume the fight |
| `[` `]` | Previous / next fighter to follow |
| `-` `=` | Slower / faster time (single player only) |
| F4 | Show/hide the bars over fighters (name, stars, health bar) |
| F10 | Show/hide the whole arena overlay (name tags, side scores, kill feed) |

Free camera: WASD to move, Space/E up, Ctrl/Q down, Shift ×3, Alt ×0.3, mouse wheel sets the speed.
Follow camera: mouse to orbit, wheel for distance. With a menu open, hold the right mouse button to look around.

## Multiplayer

Works on dedicated servers and on worlds started with "Start server".

- Every player who takes part in a fight or stands near the arena needs the mod, **the same version**
  (a mismatch is reported in the game).
- Installing the mod **on the server** is recommended: the server then keeps arena fighters simulated when they
  wander to the edge of the players' active zones. Without it such a fighter freezes, invisible, until
  a player comes closer (vanilla behavior); a fighter frozen this way is marked "(frozen)".
- The player who deploys an arena hosts it: borders, rules, fight flow and results. Guests see the arena
  and, if the host allows it, start, pause and clear the fight and spawn their own fighters.
- Each player decides for themselves whether they are visible, whether creatures react to them, which team
  they are on and whether they are invulnerable.

## Configuration

- `BepInEx/config/valheimarena.arena.cfg` — interface language (`Language`: English by default, Russian, Auto follows the game), keys,
  UI scale (a multiplier to the automatic scale), mouse sensitivity.
- `BepInEx/config/ValheimArena/settings.json` — arena settings and roster.
- `BepInEx/config/ValheimArena/profiles/` — creature profiles from the editor.
- `BepInEx/config/ValheimArena/scenarios/` — saved scenarios.

## Compatibility

- While the arena camera is on, the mod replaces `GameCamera.LateUpdate`: other camera mods may conflict.
- Hostility (`BaseAI.IsEnemy`), AI perception and movement, damage, area attacks and projectiles are patched
  for arena fighters only; wild creatures and the world are left alone.
- On the server the mod changes object ownership handling (`ZDOMan.ReleaseNearbyZDOS`) for arena fighters only.
- Bosses fighting on the arena don't advance world progress: their defeat keys and cutscenes are disabled.

## Known limitations

- Time control works only while no other players are connected (a limitation of the game).
- Stars above 2 don't change a creature's look — the game has no looks for them; health and damage keep scaling.
- The free camera stays within about 80 m of the arena border: the world is loaded around your character.
- A player without the mod who becomes the owner of an arena fighter runs it as an ordinary creature.

## Building

Requires the .NET SDK 6 or newer and an installed Valheim with BepInExPack Valheim: the game and BepInEx
assemblies are referenced from the game folder and are not part of this repository.

```
.\build.ps1                                 # Release build + Harmony patch check against your game
.\build.ps1 -Deploy                         # ... and copy the plugin to BepInEx\plugins\ValheimArena
.\build.ps1 -ValheimDir "D:\Games\Valheim"  # game installed elsewhere
```

`tools/PatchCheck` verifies that every Harmony patch target and every private game member the mod accesses exists
in the installed Valheim; after a game update it shows exactly what has changed.

Source layout: `src/Core` — arena sessions, rules, networking, fighters; `src/Patches` — Harmony patches;
`src/Profiles` — creature profiles; `src/UI` — windows and overlay.

## License

MIT License — see [LICENSE](LICENSE).
