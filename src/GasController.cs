using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DeadlyGas
{
    /// <summary>
    /// Le gaz : montée (grace period), teinte verte + brouillard, puis dégâts
    /// périodiques répartis sur tout le corps via ActiveHealthController.
    /// Le rythme est calé sur le thorax (85 PV) pour que "Minutes avant la
    /// mort" soit vrai à vie pleine. La mort résultante est une mort normale.
    /// </summary>
    public class GasController : MonoBehaviour
    {
        private static GasController _instance;

        private float _elapsed;
        private float _nextTick;
        private bool _damageBroken;
        private int _resolveAttempts;

        // Réflexion santé
        private object _healthController;
        private MethodInfo _applyDamage;
        private Type _bodyPartEnum;
        private object[] _bodyParts;
        private object _damageInfo;

        // Visuel
        private Texture2D _tint;
        private bool _fogSaved;
        private bool _prevFog;
        private Color _prevFogColor;
        private float _prevFogDensity;
        private FogMode _prevFogMode;

        private const float ChestHp = 85f;

        public static void EnsureStarted()
        {
            if (_instance != null) return;
            var go = new GameObject("DeadlyGasController");
            _instance = go.AddComponent<GasController>();
            DontDestroyOnLoad(go);
            Plugin.Log.LogInfo("[DeadlyGas] Fin du timer : le gaz envahit la zone.");
        }

        /// <summary>Nouveau raid (timer repassé positif) : purge l'ancien gaz.</summary>
        public static void ResetIfNeeded()
        {
            if (_instance == null) return;
            _instance.RestoreFog();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Update()
        {
            // Fin de raid (mort, extraction, abandon) : GameWorld disparaît
            // -> on nettoie tout, sinon le filtre vert suit dans les menus.
            if (Time.frameCount % 30 == 0 && GetGameWorld() == null)
            {
                Plugin.Log.LogInfo("[DeadlyGas] Fin de raid : dissipation du gaz.");
                ResetIfNeeded();
                return;
            }

            _elapsed += Time.deltaTime;

            if (Plugin.VisualEffects.Value) ApplyFog();

            var grace = Plugin.GraceSeconds.Value;
            if (_elapsed < grace || _damageBroken) return;

            if (_elapsed >= _nextTick)
            {
                _nextTick = _elapsed + 1f;
                DamageTick();
            }
        }

        private void DamageTick()
        {
            try
            {
                if (_applyDamage == null && !ResolveHealth())
                {
                    // On réessaie quelques ticks (le joueur peut ne pas être prêt)
                    if (++_resolveAttempts >= 10)
                    {
                        Plugin.Log.LogError("[DeadlyGas] Impossible de résoudre la santé après 10 essais — dégâts désactivés pour ce raid (activer Mode sonde et envoyer le log).");
                        _damageBroken = true;
                    }
                    return;
                }

                // PV/s calé pour tuer le thorax en MinutesToDie (hors grace).
                var perSecond = ChestHp / Mathf.Max(30f, Plugin.MinutesToDie.Value * 60f);
                foreach (var part in _bodyParts)
                    _applyDamage.Invoke(_healthController, new[] { part, (object)perSecond, _damageInfo });
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[DeadlyGas] DamageTick: {e} — dégâts désactivés pour ce raid.");
                _damageBroken = true;
            }
        }

        private static object GetGameWorld()
        {
            var gwType = TimerPatch.FindType("GameWorld");
            return gwType == null ? null : TimerPatch.SingletonInstance(gwType);
        }

        /// <summary>Propriété OU champ (public ou non) — MainPlayer est un champ
        /// dans certaines versions, invisible pour GetProperty.</summary>
        private static object GetMember(object obj, string name)
        {
            if (obj == null) return null;
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, F);
            return f?.GetValue(obj);
        }

        private bool ResolveHealth()
        {
            // GameWorld.MainPlayer.ActiveHealthController (membres champ OU propriété)
            var gw = GetGameWorld();
            var player = GetMember(gw, "MainPlayer");
            _healthController = GetMember(player, "ActiveHealthController");
            if (_healthController == null)
            {
                ProbeIfEnabled(player != null ? player.GetType() : gw?.GetType());
                return false;
            }

            _applyDamage = _healthController.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "ApplyDamage" && m.GetParameters().Length == 3
                    && m.GetParameters()[0].ParameterType.IsEnum
                    && m.GetParameters()[1].ParameterType == typeof(float));
            if (_applyDamage == null) { ProbeIfEnabled(_healthController.GetType()); return false; }

            var pars = _applyDamage.GetParameters();
            _bodyPartEnum = pars[0].ParameterType;
            // Toutes les parties du corps sauf les agrégats éventuels (Common/Everybody).
            _bodyParts = Enum.GetValues(_bodyPartEnum).Cast<object>()
                .Where(v => { var n = v.ToString(); return n != "Common" && n != "Everybody"; })
                .ToArray();

            // 3e paramètre : struct DamageInfo par défaut, DamageType=Poison si possible.
            var diType = pars[2].ParameterType;
            _damageInfo = Activator.CreateInstance(diType);
            var dtField = diType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType.IsEnum && f.Name.IndexOf("DamageType", StringComparison.OrdinalIgnoreCase) >= 0);
            if (dtField != null)
            {
                var poison = Enum.GetValues(dtField.FieldType).Cast<object>()
                    .FirstOrDefault(v => v.ToString() == "Poison");
                if (poison != null) dtField.SetValue(_damageInfo, poison); // mute la struct boxée
            }

            Plugin.Log.LogInfo($"[DeadlyGas] Santé résolue : ApplyDamage({_bodyPartEnum.Name}, float, {diType.Name}), {_bodyParts.Length} parties du corps.");
            return true;
        }

        private void ProbeIfEnabled(Type t)
        {
            if (t != null && Plugin.DebugProbe.Value) Probe.DumpMembers(t);
        }

        // ---------- Visuel ----------

        private void ApplyFog()
        {
            if (!_fogSaved)
            {
                _prevFog = RenderSettings.fog;
                _prevFogColor = RenderSettings.fogColor;
                _prevFogDensity = RenderSettings.fogDensity;
                _prevFogMode = RenderSettings.fogMode;
                _fogSaved = true;
            }
            var ramp = Mathf.Clamp01(_elapsed / Mathf.Max(1f, Plugin.GraceSeconds.Value));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.55f, 0.25f);
            RenderSettings.fogDensity = Mathf.Lerp(0f, Plugin.FogDensity.Value, ramp);
        }

        private void RestoreFog()
        {
            if (!_fogSaved) return;
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogDensity = _prevFogDensity;
            RenderSettings.fogMode = _prevFogMode;
        }

        private void OnGUI()
        {
            if (!Plugin.VisualEffects.Value) return;
            if (_tint == null)
            {
                _tint = new Texture2D(1, 1);
                _tint.SetPixel(0, 0, Color.white);
                _tint.Apply();
            }
            var ramp = Mathf.Clamp01(_elapsed / Mathf.Max(1f, Plugin.GraceSeconds.Value));
            var pulse = 0.85f + 0.15f * Mathf.Sin(_elapsed * 2f);
            var prev = GUI.color;
            GUI.color = new Color(0.25f, 0.6f, 0.2f, Plugin.TintIntensity.Value * ramp * pulse);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _tint);
            GUI.color = prev;
        }

        private void OnDestroy() => RestoreFog();
    }
}
