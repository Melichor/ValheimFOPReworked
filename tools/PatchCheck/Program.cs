// Checks, without starting the game, that every Harmony patch in the plugin fits the installed game: the patched
// method exists (exactly one, or the overload with the given argument types), and every argument a patch method asks
// for exists on it with a matching type. Harmony only finds these mistakes when the game loads the plugin.
//
//     dotnet build && dotnet run --project tools/PatchCheck
//     dotnet run --project tools/PatchCheck -- <Valheim folder> <plugin dll>
//
// Run it after a game update. It reads the assemblies as metadata only, so nothing of the game runs.

using System.Reflection;
using System.Text.RegularExpressions;

const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.DeclaredOnly;
string[] patchKinds = { "Prefix", "Postfix", "Finalizer", "Transpiler" };

string root = FindRoot(AppContext.BaseDirectory);
string game = args.Length > 0 ? args[0] : ReadValheimDir(Path.Combine(root, "Environment.props"));
string plugin = args.Length > 1
    ? args[1]
    : Path.Combine(root, "src", "ValheimFOPReworked", "bin", "Debug", "netstandard2.1", "ValheimFOPReworked.dll");
string managed = Path.Combine(game, "valheim_Data", "Managed");
string bepinex = Path.Combine(root, "deps", "BepInExPack_Valheim", "BepInEx", "core");
if (!File.Exists(plugin))
{
    return Fail($"{plugin} does not exist, build the plugin first");
}

// Game assemblies first: where BepInEx ships a library the game also has, the game's copy is the one that runs
var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (string path in Directory.GetFiles(managed, "*.dll").Concat(Directory.GetFiles(bepinex, "*.dll")).Append(plugin))
{
    byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
}
using var context = new MetadataLoadContext(new PathAssemblyResolver(byName.Values), "mscorlib");
Assembly assembly = context.LoadFromAssemblyPath(plugin);

int patches = 0;
var errors = new List<string>();
foreach (Type patch in assembly.GetTypes())
{
    List<CustomAttributeData> targets = patch.GetCustomAttributesData()
        .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToList();
    if (targets.Count == 0)
    {
        continue;
    }
    patches++;
    string name = patch.FullName!.Replace('+', '.');
    if (patch.GetMethod("TargetMethods", All) != null)
    {
        errors.Add($"{name}: TargetMethods cannot be checked, give each target its own patch class");
        continue;
    }

    Type? targetType = null;
    string? methodName = null;
    Type[]? argumentTypes = null;
    foreach (CustomAttributeData attribute in targets)
    {
        foreach (CustomAttributeTypedArgument argument in attribute.ConstructorArguments)
        {
            switch (argument.Value)
            {
                case Type type:
                    targetType = type;
                    break;
                case string text:
                    methodName = text;
                    break;
                case IReadOnlyCollection<CustomAttributeTypedArgument> types:
                    argumentTypes = types.Select(t => (Type)t.Value!).ToArray();
                    break;
            }
        }
    }
    if (targetType == null || methodName == null)
    {
        errors.Add($"{name}: no target type and method in its [HarmonyPatch]");
        continue;
    }

    MethodInfo[] candidates = Hierarchy(targetType)
        .Select(t => t.GetMethods(All).Where(m => m.Name == methodName).ToArray())
        .FirstOrDefault(found => found.Length > 0) ?? Array.Empty<MethodInfo>();
    if (argumentTypes != null)
    {
        candidates = candidates
            .Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(argumentTypes)).ToArray();
    }
    string target = $"{targetType.Name}.{methodName}";
    if (candidates.Length != 1)
    {
        errors.Add(candidates.Length == 0
            ? $"{name}: {target} does not exist"
            : $"{name}: {target} has {candidates.Length} overloads, give the argument types");
        continue;
    }
    MethodInfo original = candidates[0];

    foreach (MethodInfo method in patch.GetMethods(All).Where(m => patchKinds.Contains(m.Name)))
    {
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            string? problem = CheckParameter(original, targetType, method.Name, parameter);
            if (problem != null)
            {
                errors.Add($"{name}.{method.Name}: {problem}");
            }
        }
    }
    Console.WriteLine($"ok    {target,-40} {name}");
}

foreach (string error in errors)
{
    Console.WriteLine($"ERROR {error}");
}
Console.WriteLine($"{patches} patches checked against {game}, {errors.Count} errors");
return errors.Count == 0 ? 0 : 1;

static string? CheckParameter(MethodInfo original, Type targetType, string kind, ParameterInfo parameter)
{
    Type type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
    switch (parameter.Name)
    {
        case "__instance":
            if (original.IsStatic)
            {
                return $"{original.Name} is static, it has no __instance";
            }
            return type.IsAssignableFrom(targetType) ? null : $"__instance is {type.Name}, the method is on {targetType.Name}";
        case "__result":
            if (kind == "Prefix" && !parameter.ParameterType.IsByRef)
            {
                return "a prefix has to take __result by ref";
            }
            return type == original.ReturnType ? null : $"__result is {type.Name}, {original.Name} returns {original.ReturnType.Name}";
        case "__state":
        case "__exception":
        case "__args":
        case "__originalMethod":
        case "__runOriginal":
            return null;
    }
    if (parameter.Name!.StartsWith("___"))
    {
        string field = parameter.Name.Substring(3);
        FieldInfo? found = Hierarchy(targetType)
            .Select(t => t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                           BindingFlags.Static | BindingFlags.DeclaredOnly))
            .FirstOrDefault(f => f != null);
        if (found == null)
        {
            return $"{targetType.Name} has no field {field}";
        }
        return type == found.FieldType ? null : $"{parameter.Name} is {type.Name}, the field is {found.FieldType.Name}";
    }
    ParameterInfo? match = original.GetParameters().FirstOrDefault(p => p.Name == parameter.Name);
    if (match == null)
    {
        return $"{original.Name} has no argument {parameter.Name} " +
               $"(it has {string.Join(", ", original.GetParameters().Select(p => p.Name))})";
    }
    Type expected = match.ParameterType.IsByRef ? match.ParameterType.GetElementType()! : match.ParameterType;
    return type == expected ? null : $"{parameter.Name} is {type.Name}, the argument is {expected.Name}";
}

static IEnumerable<Type> Hierarchy(Type type)
{
    for (Type? t = type; t != null; t = t.BaseType)
    {
        yield return t;
    }
}

static string FindRoot(string start)
{
    for (DirectoryInfo? dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ValheimFOPReworked.slnx")))
        {
            return dir.FullName;
        }
    }
    throw new DirectoryNotFoundException("the repository root (ValheimFOPReworked.slnx) was not found");
}

static string ReadValheimDir(string props)
{
    Match match = File.Exists(props)
        ? Regex.Match(File.ReadAllText(props), "<ValheimDir>(.*?)</ValheimDir>")
        : Match.Empty;
    if (!match.Success)
    {
        throw new FileNotFoundException("no ValheimDir in Environment.props, run ./setup.sh first");
    }
    return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
}

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 2;
}
