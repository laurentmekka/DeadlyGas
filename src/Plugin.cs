using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DeadlyGas
{
    [BepInPlugin("com.mekka.deadlygas", "Deadly Gas", "0.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        // ----- Config -----
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MinutesToDie;
        public static ConfigEntry<float> GraceSeconds;
        public static ConfigEntry<bool> VisualEffects;
        public static ConfigEntry<float> TintIntensity;
        public static ConfigEntry<float> FogDensity;
        public static ConfigEntry<bool> DebugProbe;
        public static ConfigEntry<bool> MaskProtection;
        public static ConfigEntry<float> GasMaskFactor;
        public static ConfigEntry<float> RespiratorFactor;
        public static ConfigEntry<string> GasMaskIds;
        public static ConfigEntry<string> RespiratorIds;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1. Général", "Activer", true,
                "Remplace le MIA de fin de raid par le gaz mortel.");
            MinutesToDie = Config.Bind("1. Général", "Minutes avant la mort", 3f,
                new ConfigDescription("Temps de survie dans le gaz avec la vie pleine (calé sur la tête, première partie vitale à tomber).",
                    new AcceptableValueRange<float>(0.5f, 15f)));
            GraceSeconds = Config.Bind("1. Général", "Montée du gaz (secondes)", 20f,
                new ConfigDescription("Délai entre la fin du timer et les premiers dégâts (le gaz 'monte').",
                    new AcceptableValueRange<float>(0f, 120f)));
            MaskProtection = Config.Bind("2. Masque à gaz", "Protection par masque", true,
                "Un masque à gaz porté (slot visage) ralentit les dégâts du gaz.");
            GasMaskFactor = Config.Bind("2. Masque à gaz", "Facteur masque complet", 3f,
                new ConfigDescription("Division des dégâts avec un vrai masque à gaz (3 = temps de survie triplé).",
                    new AcceptableValueRange<float>(1f, 10f)));
            RespiratorFactor = Config.Bind("2. Masque à gaz", "Facteur respirateur", 1.5f,
                new ConfigDescription("Division des dégâts avec un demi-masque/respirateur.",
                    new AcceptableValueRange<float>(1f, 10f)));
            GasMaskIds = Config.Bind("2. Masque à gaz", "IDs masques complets",
                "5b432c305acfc40019478128,60363c0c92ec1c31037959f5",
                "IDs de template des masques à gaz complets, séparés par des virgules. Défaut : GP-5, GP-7.");
            RespiratorIds = Config.Bind("2. Masque à gaz", "IDs respirateurs",
                "59e7715586f7742ee5789605,5b4329f05acfc47a86086aa1",
                "IDs de template des demi-masques, séparés par des virgules. Défaut : Respirator, DevTac Ronin.");

            VisualEffects = Config.Bind("3. Visuel", "Effets visuels", true,
                "Teinte verte + brouillard quand le gaz est actif.");
            TintIntensity = Config.Bind("3. Visuel", "Intensité du filtre", 0.45f,
                new ConfigDescription("Opacité de la teinte verte (0.2 = subtil, 0.45 = marqué, 0.7 = purée de pois).",
                    new AcceptableValueRange<float>(0.1f, 0.8f)));
            FogDensity = Config.Bind("3. Visuel", "Densité du brouillard", 0.06f,
                new ConfigDescription("Densité du brouillard au maximum de la montée.",
                    new AcceptableValueRange<float>(0.01f, 0.15f)));
            DebugProbe = Config.Bind("4. Debug", "Mode sonde", false,
                "Logge les types/membres détectés (à activer si le mod ne trouve pas ses cibles, puis envoyer le log).");

            var harmony = new Harmony("com.mekka.deadlygas");
            var ok = TimerPatch.TryApply(harmony);
            Log.LogInfo(ok
                ? "[DeadlyGas] Patch fin-de-timer appliqué. Le gaz attend son heure."
                : "[DeadlyGas] Patch NON appliqué (cible introuvable) — comportement vanilla conservé. Activer le Mode sonde et envoyer le log.");
        }
    }
}
