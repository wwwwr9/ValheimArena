using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimArena
{
    /// <summary>Язык интерфейса мода (по умолчанию — английский).</summary>
    public enum UiLanguage
    {
        /// <summary>Как в игре: русский при русском языке Valheim, иначе английский.</summary>
        Auto,
        Russian,
        English
    }

    /// <summary>
    /// Перевод интерфейса. Ключ — русский текст из исходников, перевод — из <see cref="En"/>;
    /// нет перевода — остаётся русский. Язык — настройка «Language» (меню арены или cfg); в режиме Auto —
    /// язык игры (до её загрузки — язык системы), он перепроверяется раз в секунду.
    /// </summary>
    public static class L
    {
        private static readonly FieldInfo GameLocalization = AccessTools.Field(typeof(Localization), "m_instance");

        private static bool _english;
        private static float _nextCheck = float.MinValue;

        public static bool English
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (now >= _nextCheck)
                {
                    _nextCheck = now + 1f;
                    _english = Resolve();
                }
                return _english;
            }
        }

        /// <summary>Язык сменили — определить заново при следующем обращении.</summary>
        public static void Invalidate() => _nextCheck = float.MinValue;

        /// <summary>Перевести строку (ключ — русский текст).</summary>
        public static string T(string ru)
        {
            if (ru == null || !English) return ru;
            return En.Strings.TryGetValue(ru, out var en) ? en : ru;
        }

        /// <summary>Перевести формат и подставить значения (как string.Format).</summary>
        public static string F(string ru, params object[] args) => string.Format(T(ru), args);

        private static bool Resolve()
        {
            var setting = ArenaPlugin.Language != null ? ArenaPlugin.Language.Value : UiLanguage.Auto;
            if (setting == UiLanguage.English) return true;
            if (setting == UiLanguage.Russian) return false;
            // Экземпляр локализации игры создаётся при первом обращении — до игры его не создаём сами.
            if (GameLocalization?.GetValue(null) is Localization game) return game.GetSelectedLanguage() != "Russian";
            return Application.systemLanguage != SystemLanguage.Russian;
        }
    }
}
