using HarmonyLib;
using UnityEngine;

namespace ValheimArena.UI
{
    /// <summary>
    /// Полное скрытие интерфейса игры: HUD, полоски над существами, всплывающий урон, сообщения и чат прячет
    /// сам флаг игры Hud.m_userHidden (как по Ctrl+F3), миникарту и подсказки клавиш — мод. Оверлей и окна арены
    /// остаются. Состояние не сохраняется: в новом мире интерфейс снова виден.
    /// </summary>
    public static class GameUi
    {
        public static bool Hidden { get; private set; }
        private static bool _minimapHidden;

        public static void Toggle()
        {
            SetHidden(!Hidden);
            if (MessageHud.instance)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,
                    Hidden ? L.T("Интерфейс игры скрыт") : L.T("Интерфейс игры показан"), 0, null, showDespiteHiddenHUD: true);
        }

        public static void SetHidden(bool hidden)
        {
            if (!Hud.instance) hidden = false;
            Hidden = hidden;
            if (Hud.instance) Hud.instance.m_userHidden = hidden;
            if (!hidden) RestoreMinimap();
        }

        /// <summary>Каждый кадр: держать скрытыми части, которые игра по своему флагу не прячет.</summary>
        public static void Update()
        {
            if (!Hidden) return;
            // Игра вернула интерфейс сама (Ctrl+F3, удержание кнопки геймпада, новый мир) — значит, его вернули.
            if (!Hud.instance || !Hud.instance.m_userHidden)
            {
                Hidden = false;
                RestoreMinimap();
                return;
            }
            var map = Minimap.instance;
            if (map && map.m_mode == Minimap.MapMode.Small && map.m_smallRoot && map.m_smallRoot.activeSelf)
            {
                map.m_smallRoot.SetActive(false);
                _minimapHidden = true;
            }
        }

        private static void RestoreMinimap()
        {
            if (!_minimapHidden) return;
            _minimapHidden = false;
            var map = Minimap.instance;
            if (map && map.m_mode == Minimap.MapMode.Small && map.m_smallRoot) map.m_smallRoot.SetActive(true);
        }
    }

    /// <summary>Подсказки клавиш игра включает заново каждый кадр — при скрытом интерфейсе гасим их сразу после неё.</summary>
    [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
    internal static class KeyHintsHidePatch
    {
        private static void Postfix(KeyHints __instance)
        {
            if (!GameUi.Hidden) return;
            Off(__instance.m_buildHints);
            Off(__instance.m_combatHints);
            Off(__instance.m_inventoryHints);
            Off(__instance.m_inventoryWithContainerHints);
            Off(__instance.m_fishingHints);
            Off(__instance.m_barberHints);
            Off(__instance.m_radialHints);
        }

        private static void Off(GameObject go)
        {
            if (go && go.activeSelf) go.SetActive(false);
        }
    }
}
