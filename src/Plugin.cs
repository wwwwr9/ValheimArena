using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;
using ValheimArena.UI;

namespace ValheimArena
{
    [BepInPlugin(Guid, ModName, Version)]
    public class ArenaPlugin : BaseUnityPlugin
    {
        public const string Guid = "valheimarena.arena";
        public const string ModName = "Valheim Arena";
        public const string Version = "1.5.0";

        internal static ArenaPlugin Instance;
        internal static ManualLogSource Log;

        internal static string DataDir;

        internal static ConfigEntry<UiLanguage> Language;
        internal static ConfigEntry<KeyCode> KeyMenu;
        internal static ConfigEntry<KeyCode> KeyEditor;
        internal static ConfigEntry<KeyCode> KeyCamera;
        internal static ConfigEntry<KeyCode> KeyFight;
        internal static ConfigEntry<KeyCode> KeyNextTarget;
        internal static ConfigEntry<KeyCode> KeyPrevTarget;
        internal static ConfigEntry<KeyCode> KeySlower;
        internal static ConfigEntry<KeyCode> KeyFaster;
        internal static ConfigEntry<KeyCode> KeyOverlay;
        internal static ConfigEntry<KeyCode> KeyTags;
        internal static ConfigEntry<KeyCode> KeyGameUi;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<float> MouseSensitivity;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            DataDir = Path.Combine(Paths.ConfigPath, "ValheimArena");
            Directory.CreateDirectory(DataDir);
            BindConfig();
            ArenaSettingsStore.Load();

            _harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(ArenaPlugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Log.LogError(L.F("Патч {0} не применён: {1}", type.Name, e.GetBaseException().Message));
                }
            }
            Log.LogInfo(L.F("{0} {1} загружен: патчей {2}, ошибок {3}. Меню: {4}, редактор: {5}.", ModName, Version, ok, failed, KeyMenu.Value, KeyEditor.Value));
        }

        private void BindConfig()
        {
            Language = Config.Bind("Interface", "Language", UiLanguage.English,
                "Interface language (also switched in the arena menu, F7). Auto — the game's language (Russian for Russian, English otherwise).\n" +
                "Язык интерфейса (переключается и в меню арены, F7). Auto — как в игре (русский при русском языке игры, иначе английский).");
            Language.SettingChanged += (s, e) => L.Invalidate();
            UiScale = Config.Bind("Interface", "Scale", 1f, new ConfigDescription(
                "Multiplier to the automatic scale (windows follow the screen height, 1080p = 1).\n" +
                "Множитель к автоматическому масштабу (окна подстраиваются под высоту экрана, 1080p = 1).",
                new AcceptableValueRange<float>(0.5f, 2.5f)));
            KeyMenu = Config.Bind("Keys", "Arena menu", KeyCode.F7, "Open/close the arena menu. / Открыть/закрыть меню арены.");
            KeyEditor = Config.Bind("Keys", "Creature editor", KeyCode.F8, "Open/close the creature editor. / Открыть/закрыть редактор существ.");
            KeyCamera = Config.Bind("Keys", "Camera mode", KeyCode.F3, "Player camera → free → follow. / Камера игрока → свободная → слежение.");
            if (KeyCamera.Value == KeyCode.F9) KeyCamera.Value = KeyCode.F3;
            KeyFight = Config.Bind("Keys", "Start or pause fight", KeyCode.F6, "Deploy the arena, start, pause or resume the fight. / Развернуть арену, начать бой, пауза, продолжить.");
            KeyNextTarget = Config.Bind("Keys", "Next target", KeyCode.RightBracket, "Follow camera: next fighter. / Слежение: следующий боец.");
            KeyPrevTarget = Config.Bind("Keys", "Previous target", KeyCode.LeftBracket, "Follow camera: previous fighter. / Слежение: предыдущий боец.");
            KeySlower = Config.Bind("Keys", "Slower time", KeyCode.Minus, "Single player / own server without other players only. / Только без других игроков.");
            KeyFaster = Config.Bind("Keys", "Faster time", KeyCode.Equals, "Single player / own server without other players only. / Только без других игроков.");
            KeyOverlay = Config.Bind("Keys", "Overlay", KeyCode.F10, "Show/hide the whole arena overlay: name tags, scores, kill feed. / Показать/скрыть весь оверлей арены: таблички, счёт, ленту убийств.");
            KeyTags = Config.Bind("Keys", "Name tags", KeyCode.F4, "Show/hide the name tags over fighters: name, stars, health bar. / Показать/скрыть таблички над бойцами: имя, звёзды, полоска здоровья.");
            KeyGameUi = Config.Bind("Keys", "Game interface", KeyCode.F1,
                "Hide/show the game's own interface: HUD, minimap, bars over creatures, messages, chat and key hints; the arena overlay and windows stay. / " +
                "Скрыть/показать интерфейс самой игры: HUD, миникарту, полоски над существами, сообщения, чат и подсказки клавиш; оверлей и окна арены остаются.");
            MouseSensitivity = Config.Bind("Camera", "Mouse sensitivity", 1f, new ConfigDescription(
                "Mouse sensitivity multiplier for the free and follow cameras. / Множитель чувствительности мыши для свободной камеры и слежения.",
                new AcceptableValueRange<float>(0.1f, 5f)));
        }

        private void Update()
        {
            UiInput.Update(ArenaUI.WantsCursor);
            Arena.Update();
            ArenaUI.Update();
        }

        private void FixedUpdate()
        {
            Arena.FixedUpdate();
        }

        private void OnGUI()
        {
            ArenaUI.OnGUI();
        }

        private void OnApplicationQuit()
        {
            ArenaSettingsStore.Save();
        }

        private void OnDestroy()
        {
            ArenaSettingsStore.Save();
            _harmony?.UnpatchSelf();
        }
    }
}
