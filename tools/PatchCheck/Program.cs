using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static readonly List<ModuleDefinition> GameModules = new List<ModuleDefinition>();
    private static int _errors;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("PatchCheck <mod.dll> <каталог Valheim>");
            return 2;
        }
        string modPath = args[0];
        string managed = Path.Combine(args[1], "valheim_Data", "Managed");
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        resolver.AddSearchDirectory(Path.Combine(args[1], "BepInEx", "core"));
        var rp = new ReaderParameters { AssemblyResolver = resolver };
        foreach (var name in new[] { "assembly_valheim.dll", "assembly_utils.dll", "assembly_guiutils.dll" })
            GameModules.Add(ModuleDefinition.ReadModule(Path.Combine(managed, name), rp));

        var mod = ModuleDefinition.ReadModule(modPath, rp);
        int patches = 0, fields = 0;

        foreach (var type in mod.GetTypes())
        {
            foreach (var attr in type.CustomAttributes.Where(a => a.AttributeType.Name == "HarmonyPatch"))
            {
                patches++;
                CheckPatch(type, attr);
            }
            foreach (var m in type.Methods.Where(m => m.HasBody))
                fields += CheckReflectionCalls(type, m);
        }

        int strings = CheckLocalization(mod);

        Console.WriteLine($"Патчей проверено: {patches}, обращений к закрытым членам: {fields}, строк с переводом: {strings}, ошибок: {_errors}");
        return _errors == 0 ? 0 : 1;
    }

    private static int CheckLocalization(ModuleDefinition mod)
    {
        var en = mod.GetTypes().FirstOrDefault(t => t.Namespace == "ValheimArena" && t.Name == "En");
        if (en == null) return 0;
        var keys = new HashSet<string>();
        var cctor = en.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic && m.HasBody);
        if (cctor != null)
        {
            var ins = cctor.Body.Instructions;
            for (int i = 0; i + 2 < ins.Count; i++)
                if (ins[i].OpCode == OpCodes.Ldstr && ins[i + 1].OpCode == OpCodes.Ldstr &&
                    ins[i + 2].Operand is MethodReference set && set.Name == "set_Item")
                    keys.Add((string)ins[i].Operand);
        }
        int count = 0;
        foreach (var type in mod.GetTypes())
        {
            if (type == en) continue;
            foreach (var m in type.Methods.Where(x => x.HasBody))
            {
                if (type.Name == "ArenaPlugin" && m.Name == "BindConfig") continue;
                foreach (var i in m.Body.Instructions)
                {
                    if (i.OpCode != OpCodes.Ldstr || !(i.Operand is string s) || !s.Any(c => c >= 'Ѐ' && c <= 'ӿ')) continue;
                    count++;
                    if (!keys.Contains(s)) Fail($"нет перевода ({type.Name}.{m.Name}): «{s}»");
                }
            }
        }
        return count;
    }

    private static void Fail(string msg)
    {
        _errors++;
        Console.WriteLine("  ОШИБКА: " + msg);
    }

    private static TypeDefinition FindGameType(string fullName)
    {
        foreach (var m in GameModules)
        {
            var t = m.GetType(fullName);
            if (t != null) return t;
            t = m.GetTypes().FirstOrDefault(x => x.FullName == fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static void CheckPatch(TypeDefinition patchClass, CustomAttribute attr)
    {
        var ctorArgs = attr.ConstructorArguments;
        if (ctorArgs.Count < 2) return;
        var targetRef = ctorArgs[0].Value as TypeReference;
        string methodName = ctorArgs[1].Value as string;
        TypeReference[] argTypes = null;
        if (ctorArgs.Count >= 3 && ctorArgs[2].Value is CustomAttributeArgument[] arr)
            argTypes = arr.Select(a => (TypeReference)a.Value).ToArray();
        if (targetRef == null || methodName == null) return;

        var target = FindGameType(targetRef.FullName);
        string label = $"{patchClass.Name} → {targetRef.FullName}.{methodName}";
        if (target == null) { Fail(label + ": тип не найден"); return; }

        var candidates = target.Methods.Where(m => m.Name == methodName).ToList();
        if (argTypes != null)
            candidates = candidates.Where(m => m.Parameters.Count == argTypes.Length &&
                                               m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(argTypes.Select(a => a.FullName))).ToList();
        if (candidates.Count == 0) { Fail(label + ": метод не найден"); return; }
        if (candidates.Count > 1) { Fail(label + ": перегрузка неоднозначна"); return; }
        var method = candidates[0];

        foreach (var hook in patchClass.Methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix"))
        {
            foreach (var p in hook.Parameters)
            {
                if (p.Name.StartsWith("__")) continue;
                var tp = method.Parameters.FirstOrDefault(x => x.Name == p.Name);
                if (tp == null) Fail($"{label}: у цели нет параметра «{p.Name}» ({hook.Name})");
                else if (StripRef(tp.ParameterType) != StripRef(p.ParameterType))
                    Fail($"{label}: параметр «{p.Name}» — {tp.ParameterType.FullName}, в {hook.Name} — {p.ParameterType.FullName}");
            }
            if (hook.Parameters.Any(p => p.Name == "__result") && method.ReturnType.FullName == "System.Void")
                Fail($"{label}: __result у метода без возвращаемого значения");
        }
        Console.WriteLine("  ok  " + label);
    }

    private static string StripRef(TypeReference t) => t.FullName.TrimEnd('&');

    private static int CheckReflectionCalls(TypeDefinition owner, MethodDefinition m)
    {
        int count = 0;
        var ins = m.Body.Instructions;
        for (int i = 0; i < ins.Count; i++)
        {
            if (ins[i].OpCode != OpCodes.Call || !(ins[i].Operand is MethodReference call)) continue;
            if (call.DeclaringType.Name != "AccessTools") continue;

            if (call.Name == "FieldRefAccess" && call is GenericInstanceMethod gim && gim.GenericArguments.Count == 2)
            {
                string field = PrevString(ins, i);
                if (field == null) continue;
                count++;
                var t = FindGameType(gim.GenericArguments[0].FullName);
                if (t == null) { Fail($"FieldRefAccess: тип {gim.GenericArguments[0].FullName} не найден"); continue; }
                var fd = FindField(t, field);
                if (fd == null) Fail($"FieldRefAccess: поле {t.Name}.{field} не найдено");
                else if (fd.FieldType.FullName != gim.GenericArguments[1].FullName)
                    Fail($"FieldRefAccess: {t.Name}.{field} имеет тип {fd.FieldType.FullName}, ожидается {gim.GenericArguments[1].FullName}");
                else Console.WriteLine($"  ok  поле {t.Name}.{field}");
            }
            else if (call.Name == "Method")
            {
                string name = null;
                TypeReference typeRef = null;
                int nameAt = -1;
                for (int k = i - 1; k >= Math.Max(0, i - 16); k--)
                    if (ins[k].OpCode == OpCodes.Ldstr) { name = (string)ins[k].Operand; nameAt = k; break; }
                if (nameAt >= 0)
                    for (int k = nameAt - 1; k >= Math.Max(0, nameAt - 3); k--)
                        if (ins[k].OpCode == OpCodes.Ldtoken && ins[k].Operand is TypeReference tr) { typeRef = tr; break; }
                if (name == null || typeRef == null) continue;
                count++;
                var t = FindGameType(typeRef.FullName);
                if (t == null) { Fail($"Method: тип {typeRef.FullName} не найден"); continue; }
                var md = FindMethod(t, name);
                if (md == null) Fail($"Method: метод {t.Name}.{name} не найден");
                else Console.WriteLine($"  ok  метод {t.Name}.{name}");
            }
        }
        return count;
    }

    private static string PrevString(Mono.Collections.Generic.Collection<Instruction> ins, int i)
    {
        for (int k = i - 1; k >= Math.Max(0, i - 3); k--)
            if (ins[k].OpCode == OpCodes.Ldstr) return (string)ins[k].Operand;
        return null;
    }

    private static FieldDefinition FindField(TypeDefinition t, string name)
    {
        for (var cur = t; cur != null; cur = cur.BaseType?.Resolve())
        {
            var f = cur.Fields.FirstOrDefault(x => x.Name == name);
            if (f != null) return f;
        }
        return null;
    }

    private static MethodDefinition FindMethod(TypeDefinition t, string name)
    {
        for (var cur = t; cur != null; cur = cur.BaseType?.Resolve())
        {
            var f = cur.Methods.FirstOrDefault(x => x.Name == name);
            if (f != null) return f;
        }
        return null;
    }
}
