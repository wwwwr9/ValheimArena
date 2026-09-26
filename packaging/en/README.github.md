# Valheim Arena

{INTRO}

The interface is available in English, the default, and in Russian; the language is switched with the
EN / RU / Auto buttons at the top of the arena menu (F7) or with `Language` in the config. Documentation in
Russian is in [README.ru.md](README.ru.md).

## Installation

- With a mod manager (r2modman or Thunderstore Mod Manager), install ValheimArena from Thunderstore; the manager
  installs BepInExPack Valheim automatically.
- For a manual installation, install BepInExPack Valheim ({BEPINEX_URL}), download `ValheimArena-<version>.zip`
  from Releases and extract it into the Valheim folder so that the result is
  `Valheim/BepInEx/plugins/ValheimArena/ValheimArena.dll`.
- Building from source is described under [Building](#building).

Everyone at the arena needs the same version of the mod.

{OVERVIEW}

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

The mod is distributed under the {LICENSE_NAME}; see [LICENSE](LICENSE).
