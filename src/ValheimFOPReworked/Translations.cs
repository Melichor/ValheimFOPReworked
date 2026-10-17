using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;

namespace ValheimFOPReworked
{
    /// <summary>
    /// The mod's text goes through the game's localization: Translations/&lt;Language&gt;.json, built into the plugin,
    /// with the game's language names (English, Czech, German, ...). English is loaded first, so a language that lacks
    /// a word shows it in English. In the text, $1 and $2 are filled in by the game, as in its own messages.
    /// </summary>
    internal static class Translations
    {
        /// <summary>For when the game set up its language before the mod loaded.</summary>
        public static void AddToCurrentLanguage()
        {
            if (Localization.m_instance != null)
            {
                AddTo(Localization.m_instance, Localization.m_instance.GetSelectedLanguage());
            }
        }

        private static void AddTo(Localization localization, string language)
        {
            foreach (string name in language == "English" ? new[] { "English" } : new[] { "English", language })
            {
                foreach (KeyValuePair<string, string> word in Load(name))
                {
                    localization.AddWord(word.Key, word.Value);
                }
            }
            // Text looked up before the words were there is cached untranslated
            localization.m_cache.EvictAll();
        }

        private static Dictionary<string, string> Load(string language)
        {
            using (Stream stream = typeof(Translations).Assembly.GetManifestResourceStream(
                       $"ValheimFOPReworked.Translations.{language}.json"))
            {
                if (stream == null)
                {
                    return new Dictionary<string, string>();
                }
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd()) ??
                           new Dictionary<string, string>();
                }
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        private static class OnSetupLanguage
        {
            private static void Postfix(Localization __instance, string language) => AddTo(__instance, language);
        }
    }
}
