using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DeadlyGas
{
    [BepInPlugin("com.mekka.deadlygas", "Deadly Gas", "0.2.1")]
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

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1. Général", "Activer", true,
                "Remplace le MIA de fin de raid par le gaz mortel.");
            MinutesToDie = Config.Bind("1. Général", "Minutes avant la mort", 3f,
                new ConfigDescription("Temps de survie dans le gaz avec la vie pleine (thorax).",
                    new AcceptableValueRange<float>(0.5f, 15f)));
            GraceSeconds = Config.Bind("1. Général", "Montée du gaz (secondes)", 20f,
                new ConfigDescription("Délai entre la fin du timer et les premiers dégâts (le gaz 'monte').",
                    new AcceptableValueRange<float>(0f, 120f)));
            VisualEffects = Config.Bind("2. Visuel", "Effets visuels", true,
                "Teinte verte + brouillard quand le gaz est actif.");
            TintIntensity = Config.Bind("2. Visuel", "Intensité du filtre", 0.45f,
                new ConfigDescription("Opacité de la teinte verte (0.2 = subtil, 0.45 = marqué, 0.7 = purée de pois).",
                    new AcceptableValueRange<float>(0.1f, 0.8f)));
            FogDensity = Config.Bind("2. Visuel", "Densité du brouillard", 0.06f,
                new ConfigDescription("Densité du brouillard au maximum de la montée.",
                    new AcceptableValueRange<float>(0.01f, 0.15f)));
            DebugProbe = Config.Bind("3. Debug", "Mode sonde", false,
                "Logge les types/membres détectés (à activer si le mod ne trouve pas ses cibles, puis envoyer le log).");

            var harmony = new Harmony("com.mekka.deadlygas");
            var ok = TimerPatch.TryApply(harmony);
            Log.LogInfo(ok
                ? "[DeadlyGas] Patch fin-de-timer appliqué. Le gaz attend son heure."
                : "[DeadlyGas] Patch NON appliqué (cible introuvable) — comportement vanilla conservé. Activer le Mode sonde et envoyer le log.");
        }
    }
}
