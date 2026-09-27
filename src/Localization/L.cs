using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimArena
{
    public enum UiLanguage
    {
        Auto,
        Russian,
        English
    }

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

        public static void Invalidate() => _nextCheck = float.MinValue;

        public static string T(string ru)
        {
            if (ru == null || !English) return ru;
            return En.Strings.TryGetValue(ru, out var en) ? en : ru;
        }

        public static string F(string ru, params object[] args) => string.Format(T(ru), args);

        private static bool Resolve()
        {
            var setting = ArenaPlugin.Language != null ? ArenaPlugin.Language.Value : UiLanguage.Auto;
            if (setting == UiLanguage.English) return true;
            if (setting == UiLanguage.Russian) return false;
            if (GameLocalization?.GetValue(null) is Localization game) return game.GetSelectedLanguage() != "Russian";
            return Application.systemLanguage != SystemLanguage.Russian;
        }
    }
}
