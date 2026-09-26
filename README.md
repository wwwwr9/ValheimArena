# Valheim Arena

Valheim Arena is a BepInEx 5 mod that turns a part of the world into an arena. Any creature of the game,
bosses included, can be spawned with chosen stars, teams and factions onto a fenced battleground and watched
from a free or follow camera, and a separate editor retunes every creature down to its health, resistances,
movement, AI senses and each individual attack.

The interface is available in English, the default, and in Russian; the language is switched with the
EN / RU / Auto buttons at the top of the arena menu (F7) or with `Language` in the config. Documentation in
Russian is in [README.ru.md](README.ru.md).

## Installation

- With a mod manager (r2modman or Thunderstore Mod Manager), install ValheimArena from Thunderstore; the manager
  installs BepInExPack Valheim automatically.
- For a manual installation, install BepInExPack Valheim (https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), download `ValheimArena-<version>.zip`
  from Releases and extract it into the Valheim folder so that the result is
  `Valheim/BepInEx/plugins/ValheimArena/ValheimArena.dll`.
- Building from source is described under [Building](#building).

Everyone at the arena needs the same version of the mod.

## Features

### Spawning

Any creature of the game, including creatures added by other mods, can be spawned in groups. A group defines
the count (1 to 200), the star range (0 to 20), the team (1 to 8), an optional faction override, the placement
(team side, random on the arena, center or camera aim), the spread and the group's own behavior. The catalog
filters creatures by the traits the game itself uses: bosses, flyers, water dwellers, and factions, which are
the grouping the game consults when deciding who attacks whom. For the selected creature the catalog shows how
the game spawns it (biomes, pack size, stars, time of day, raids, nests and altar), and "As in the game: group
size and stars" copies that pack size and those stars into the group. A roster is spawned one group at a time
or all at once, and complete setups are saved as scenarios.

### Hostility

Hostility follows one of three modes. In "Teams" an 8×8 matrix decides which teams are hostile to each other.
In "Factions" a directional faction-by-faction matrix decides who attacks whom, so bosses can be set to attack
regular creatures while those regular creatures fight back only if that option is enabled. In "Free for all"
every fighter is hostile to every other. General rules add friendly fire, area attacks that hit enemies of the
same kind (vanilla AoE spares creatures with the same name, which makes a troll-versus-troll fight impossible
without this rule), isolation from wild creatures and other players, and damage that does not depend on the
number of players nearby. The last rule matters because the game makes creatures hit 4% harder and take 30%
less damage for every additional player within 100 m, even in fights between creatures; by default the arena
uses single-player numbers.

### Battleground

The battleground is a circle or a square with a radius of 5 to 150 m, marked by a visible ring of posts that
follows the terrain. Leaving it is either free, blocked by a wall, or answered by sending the fighter back to
the center. Flyers additionally get a ceiling and a cruise altitude.

### Behavior

In "Hunt" a fighter always knows where its enemies are and goes for them. In "Hold" it stays in place, turns
toward enemies and strikes whoever comes within range. In "Free" it keeps vanilla senses and wanders around its
spawn point, and bosses stay near that point as they would near their altar. Creatures summoned by fighters,
such as boss minions and spawns of attacks and projectiles, join their summoner's side. Intruders, meaning wild
creatures that wander in and summons that belong to no fighter, can be ignored, drawn into the fight or removed.

### Fight flow

A fight can start after a countdown or automatically, fighters stay frozen until the start and, optionally,
after the result, and the fight can be paused. Time can be slowed down or stopped: the menu buttons cover stop
and ×0.1 to ×2, and the `=` key goes up to ×3. Time control works only in single player or in a world you host
while nobody else is connected, and never on a dedicated server.

### Cameras

The free camera moves independently of the character. The follow camera orbits, chases or looks down on its
target, switches between fighters, moves on to another fighter when the target dies, and can hand control to a
"director" that keeps the camera on the fighters who are actually fighting and dealing the most damage.

### Your character

The character can be made invisible, can let creatures pass through, can have collision disabled entirely, can
be ignored by creatures and can be made invulnerable, in any combination; the "Observer" and "Participant"
presets set these options together. The options apply everywhere in the world rather than only at the arena,
and they are remembered between sessions. Invisibility hides the character only from players who have the mod.

### Overlay and results

The overlay shows name tags in team or faction colors, health bars, side scores, a kill feed and a card for the
followed target. F4 hides the name tags and F10 hides the entire overlay, while F1 hides the game's own interface
and leaves the arena overlay and windows on screen. The results list the sides with their damage and kills
together with who killed whom.

### Creature editor (F8)

The editor covers health, with a preview for stars and the NG+ world level, as well as size, dealt and taken
damage multipliers, stagger and regeneration. It exposes 11 resistances together with separate resistances for
each weak spot, movement including flight and swimming, AI senses and pacing, and every attack individually:
damage by type, range, cooldown, angle, knockback, status effect chance, the hit zone according to the attack
type, projectiles, and effects that carry their own damage, such as boss area attacks, explosions, puddles and
ability spawns. Badges mark heals, buffs, and attacks limited to the mist or to dungeons. Edits are saved as
named profiles and applied to a group or to fighters already on the arena. Only arena fighters are affected,
because each fighter receives its own copy of the attack data while the prefabs and the rest of the world stay
vanilla.

### Cleanup

Cleanup handles creatures and corpses, items on the ground, tombstones, petrified gammeltrolls and fallen trees,
and each category is set to "remove" or "keep" on its own.

## Controls

Keys can be changed in the config file.

| Key | Action |
|---|---|
| F7 | Arena menu |
| F8 | Creature editor |
| F3 | Camera: player, free, follow |
| F6 | Deploy the arena if none is deployed; otherwise start, pause or resume the fight |
| `[` `]` | Previous or next fighter to follow (while an arena is deployed) |
| `-` `=` | Slower or faster time, from a full stop up to ×3 (while an arena is deployed and no other player is in the world) |
| F4 | Show or hide the name tags over fighters with name, stars and health bar |
| F10 | Show or hide the whole arena overlay: name tags, side scores and kill feed |
| F1 | Show or hide the game's own interface: HUD, minimap, bars over creatures, game messages, chat and key hints; the arena overlay and windows stay |

The free camera moves with WASD, rises with Space or E and descends with Ctrl or Q; Shift triples its speed,
Alt reduces it to ×0.3, and the mouse wheel sets the base speed. The follow camera orbits with the mouse and
changes distance with the wheel. While a menu is open, looking around requires holding the right mouse button.

## Multiplayer

The arena works on dedicated servers and in worlds started with "Start server".

- Every player who takes part in a fight or stands near the arena needs the mod, and all of them need the same
  version; a mismatch is reported in the game.
- Installing the mod on the server is recommended. The server then keeps arena fighters simulated when they
  wander to the edge of the players' active zones and hands fighters whose owner has left to a player nearby.
  Without the mod on the server such a fighter freezes until a player comes closer, as it would in vanilla, and
  a fighter frozen this way is marked "(frozen)". The DLL has to be copied to the server's `BepInEx/plugins`
  manually, since mod managers do not update servers.
- The player who deploys an arena hosts it and owns its borders, rules, fight flow and results. Guests see the
  arena and can start, pause and clear the fight and spawn their own fighters; this is enabled by default, and
  the host can turn it off.
- Each player decides independently whether they are visible, whether creatures react to them, which team they
  belong to and whether they are invulnerable.
- The mod has no admin lock: on a server, anyone who has the mod can spawn creatures, bosses included, and use
  the character options, so it belongs on servers where every player accepts that.
- If a player without the mod owns a boss fighter at the moment it dies, the world may count the kill.

## Configuration

- `BepInEx/config/valheimarena.arena.cfg` holds the interface language (`Language`: English by default, Russian, or Auto,
  which follows the game), the keys, the UI scale as a multiplier to the automatic scale, and mouse sensitivity.
- `BepInEx/config/ValheimArena/settings.json` holds the arena settings and the roster. A damaged file is kept
  as `settings.json.bad`, and the mod starts with defaults.
- `BepInEx/config/ValheimArena/profiles/` holds the creature profiles created in the editor.
- `BepInEx/config/ValheimArena/scenarios/` holds saved scenarios, which contain the roster and the arena rules;
  loading a scenario leaves the character, camera and overlay settings unchanged.

## Compatibility

- While the arena camera is active, the mod replaces `GameCamera.LateUpdate`, so other camera mods may conflict.
- Hostility (`BaseAI.IsEnemy`), AI perception and movement, damage, area attacks and projectiles are patched
  for arena fighters only. Wild creatures and the world are left alone, with the exception of the character
  options described above, which apply to the player everywhere.
- On the server the mod changes object ownership handling (`ZDOMan.ReleaseNearbyZDOS`), again for arena fighters
  only.
- Bosses fighting on the arena do not advance world progress, because their defeat keys and cutscenes are
  disabled. With loot enabled, however, a dropped boss trophy can still be placed on the stones, and that does
  count.

## Known limitations

- Time control requires single player or your own hosted world with nobody else connected, which is a
  limitation of the game.
- Fighters above 2★ keep the 2★ look, since the game has no looks for higher stars, while their health and
  damage continue to scale.
- The world is loaded around the character rather than the camera, so by default the free camera stays within
  80 m of the arena border; the "Stay within 80 m of the border" option can be turned off.
- A player without the mod who becomes the owner of an arena fighter runs it as an ordinary creature.
- Before the mod is removed, the arena should be closed in every world where one is deployed: fighters are
  saved in the world and would otherwise remain as ordinary creatures, bosses included.

## Building

A build requires the .NET 8 SDK or newer and an installed Valheim with BepInExPack Valheim in the game folder,
because the game and BepInEx assemblies are referenced from there and are not part of this repository. The
plugin itself targets netstandard2.1; only the PatchCheck tool needs .NET 8.

```
.\build.ps1                                 # Release build and Harmony patch check against the installed game
.\build.ps1 -Deploy                         # the same, then copy the plugin to BepInEx\plugins\ValheimArena
.\build.ps1 -ValheimDir "D:\Games\Valheim"  # game installed elsewhere
.\build.ps1 -SkipVerify                     # build without the patch check
```

If PowerShell refuses to run the script, start it with `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

`tools/PatchCheck` verifies that every Harmony patch target and every private game member the mod accesses
exists in the installed Valheim, and after a game update it reports exactly what has changed.

The sources are organized as follows: `src/Core` contains arena sessions, rules, networking and fighters,
`src/Patches` the Harmony patches, `src/Profiles` the creature profiles, and `src/UI` the windows and the
overlay.

## License

The mod is distributed under the MIT License; see [LICENSE](LICENSE).
