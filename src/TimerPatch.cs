using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace DeadlyGas
{
    /// <summary>
    /// Stratégie v0.2 : quand le timer expire, on RALLONGE la session de 24 h
    /// au lieu de bloquer la fin de raid. Le jeu croit que le raid continue :
    /// pas de MIA, extraction 100 % vanilla (comptée Survived), et le gaz
    /// s'occupe du reste. Fallback : si la rallonge est impossible, on bloque
    /// EndByTimerScenario.Update (comportement v0.1).
    /// </summary>
    public static class TimerPatch
    {
        private static Type _abstractGameType;
        private static object _game;               // AbstractGame du raid courant
        private static object _gameTimer;          // GameTimerClass
        private static PropertyInfo _pastTime;     // TimeSpan
        private static PropertyInfo _sessionTime;  // TimeSpan?
        private static bool _extendedThisRaid;
        private static bool _resolveFailed;

        public static bool TryApply(Harmony harmony)
        {
            try
            {
                var type = FindType("EndByTimerScenario");
                if (type == null)
                {
                    Probe.DumpTypesContaining("Scenario");
                    return false;
                }

                var update = type.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (update == null)
                {
                    Probe.DumpMembers(type);
                    return false;
                }

                harmony.Patch(update,
                    prefix: new HarmonyMethod(typeof(TimerPatch), nameof(UpdatePrefix)));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[DeadlyGas] TryApply: {e}");
                return false;
            }
        }

        /// <returns>true = laisser tourner l'Update vanilla, false = le bloquer.</returns>
        public static bool UpdatePrefix()
        {
            try
            {
                if (!Plugin.Enabled.Value || _resolveFailed) return true;

                var remaining = GetRemainingSeconds();
                if (remaining == null || remaining > 0) return true;

                // Temps écoulé : le gaz démarre, la session est rallongée.
                GasController.EnsureStarted();
                if (!_extendedThisRaid)
                {
                    _extendedThisRaid = TryExtendSession();
                    if (!_extendedThisRaid)
                        Plugin.Log.LogWarning("[DeadlyGas] Rallonge de session impossible — mode blocage (extraction possiblement cassée, envoyer le log).");
                }
                // Rallonge OK : vanilla peut tourner (le timer n'est plus expiré).
                // Rallonge KO : on bloque l'Update pour empêcher le MIA.
                return _extendedThisRaid;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[DeadlyGas] UpdatePrefix: {e} — retour au vanilla.");
                _resolveFailed = true;
                return true;
            }
        }

        private static double? GetRemainingSeconds()
        {
            if (_abstractGameType == null)
            {
                _abstractGameType = FindType("AbstractGame");
                if (_abstractGameType == null) { Fail("AbstractGame"); return null; }
            }

            var game = SingletonInstance(_abstractGameType);
            if (game == null) return null;            // pas en raid

            // Nouveau raid : nouvelle instance de jeu -> tout re-résoudre.
            if (!ReferenceEquals(game, _game))
            {
                _game = game;
                _gameTimer = null;
                _extendedThisRaid = false;
                GasController.ResetIfNeeded();
                if (!ResolveTimer(game)) return null;
            }
            if (_gameTimer == null && !ResolveTimer(game)) return null;

            var past = (TimeSpan)_pastTime.GetValue(_gameTimer);
            var sessionRaw = _sessionTime.GetValue(_gameTimer);
            if (sessionRaw == null) return null;
            return ((TimeSpan)sessionRaw - past).TotalSeconds;
        }

        private static bool ResolveTimer(object game)
        {
            var timerMember = game.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType.Name.Contains("GameTimer"));
            _gameTimer = timerMember?.GetValue(game);
            if (_gameTimer == null)
            {
                if (Plugin.DebugProbe.Value) Probe.DumpMembers(game.GetType());
                Fail("GameTimer"); return false;
            }

            var props = _gameTimer.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _pastTime = PickTimeSpan(props, false, "Past", "Elapsed");
            _sessionTime = PickTimeSpan(props, true, "Session", "Escape", "Total");
            if (_pastTime == null || _sessionTime == null)
            {
                if (Plugin.DebugProbe.Value) Probe.DumpMembers(_gameTimer.GetType());
                Fail("PastTime/SessionTime"); return false;
            }

            Plugin.Log.LogInfo($"[DeadlyGas] Timer résolu : {_gameTimer.GetType().Name}.{_pastTime.Name} / {_sessionTime.Name}");
            return true;
        }

        /// <summary>SessionTime += 24 h, via setter ou champ de backing.</summary>
        private static bool TryExtendSession()
        {
            try
            {
                var past = (TimeSpan)_pastTime.GetValue(_gameTimer);
                object extended = (TimeSpan?)(past + TimeSpan.FromHours(24));

                if (_sessionTime.CanWrite || _sessionTime.GetSetMethod(true) != null)
                {
                    _sessionTime.SetValue(_gameTimer, extended);
                    Plugin.Log.LogInfo("[DeadlyGas] Session rallongée de 24 h (setter). Extraction vanilla préservée.");
                    return true;
                }

                var backing = _gameTimer.GetType().GetField(
                    $"<{_sessionTime.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? _gameTimer.GetType()
                        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(f => Nullable.GetUnderlyingType(f.FieldType) == typeof(TimeSpan));
                if (backing != null)
                {
                    backing.SetValue(_gameTimer, extended);
                    Plugin.Log.LogInfo($"[DeadlyGas] Session rallongée de 24 h (champ {backing.Name}). Extraction vanilla préservée.");
                    return true;
                }

                if (Plugin.DebugProbe.Value) Probe.DumpMembers(_gameTimer.GetType());
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[DeadlyGas] TryExtendSession: {e}");
                return false;
            }
        }

        private static PropertyInfo PickTimeSpan(PropertyInfo[] props, bool nullableOk, params string[] hints)
        {
            bool IsTs(Type t) => t == typeof(TimeSpan) ||
                (nullableOk && Nullable.GetUnderlyingType(t) == typeof(TimeSpan));
            foreach (var h in hints)
            {
                var hit = props.FirstOrDefault(p => IsTs(p.PropertyType) &&
                    p.Name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null) return hit;
            }
            return props.FirstOrDefault(p => IsTs(p.PropertyType));
        }

        private static void Fail(string what)
        {
            Plugin.Log.LogWarning($"[DeadlyGas] Résolution échouée : {what}. Vanilla conservé.");
            _resolveFailed = true;
        }

        internal static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == name);
        }

        /// <summary>
        /// Singleton&lt;T&gt;.Instance en essayant TOUS les types "Singleton`1"
        /// (il y en a plusieurs dans le jeu, avec des contraintes différentes).
        /// </summary>
        internal static object SingletonInstance(Type target)
        {
            var candidates = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => t.Name == "Singleton`1" && t.IsGenericTypeDefinition)
                .OrderByDescending(t => t.FullName == "Comfort.Common.Singleton`1");

            foreach (var def in candidates)
            {
                try
                {
                    var prop = def.MakeGenericType(target)
                        .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var value = prop?.GetValue(null);
                    if (value != null) return value;
                }
                catch { /* contraintes incompatibles : suivant */ }
            }
            return null;
        }
    }
}
