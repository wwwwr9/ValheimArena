## Сборка

Для сборки нужны .NET SDK 8 или новее и установленная Valheim с BepInExPack Valheim в папке игры: сборки игры
и BepInEx берутся оттуда и в репозиторий не входят. Сам мод нацелен на netstandard2.1, а .NET 8 требуется
только проверке PatchCheck.

```
.\build.ps1                                 # Release и сверка Harmony-патчей с установленной игрой
.\build.ps1 -Deploy                         # то же и установка в BepInEx\plugins\ValheimArena
.\build.ps1 -ValheimDir "D:\Games\Valheim"  # игра в другой папке
.\build.ps1 -SkipVerify                     # сборка без сверки патчей
```

Если PowerShell отказывается запускать скрипт, его запускают командой
`powershell -ExecutionPolicy Bypass -File .\build.ps1`.

`tools\PatchCheck` сверяет с установленной версией Valheim цели всех Harmony-патчей, а также закрытые методы
и поля игры, к которым обращается мод, и после обновления игры показывает, что именно разошлось.
