using System;
using System.Linq;
using System.Reflection;

namespace DeadlyGas
{
    /// <summary>
    /// Mode sonde : dumpe types et membres dans le log BepInEx pour qu'on
    /// puisse ajuster les noms si une mise à jour d'EFT les a changés.
    /// </summary>
    public static class Probe
    {
        public static void DumpTypesContaining(string fragment)
        {
            if (!Plugin.DebugProbe.Value) return;
            var hits = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => t.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(60);
            Plugin.Log.LogInfo($"[DeadlyGas][Sonde] Types contenant '{fragment}' :");
            foreach (var t in hits) Plugin.Log.LogInfo($"  - {t.FullName} ({t.Assembly.GetName().Name})");
        }

        public static void DumpMembers(Type t)
        {
            Plugin.Log.LogInfo($"[DeadlyGas][Sonde] Membres de {t.FullName} :");
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var p in t.GetProperties(F).Take(80))
                Plugin.Log.LogInfo($"  P {p.PropertyType.Name} {p.Name}");
            foreach (var f in t.GetFields(F).Where(f => !f.Name.Contains("k__BackingField")).Take(80))
                Plugin.Log.LogInfo($"  F {f.FieldType.Name} {f.Name}");
            foreach (var m in t.GetMethods(F).Where(m => !m.IsSpecialName).Take(80))
                Plugin.Log.LogInfo($"  M {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");
        }
    }
}
