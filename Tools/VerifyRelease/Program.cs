using System;
using System.IO;
using System.Linq;
using System.Reflection;

// Verifies that the shipped Release assembly contains none of the development
// tooling: no diagnostics types, no hook, no addon-lifecycle subscription, and no
// networking. Reads metadata only, so it never executes plugin code.
class Program
{
    static int Main(string[] args)
    {
        // Relative to the repo root, so this works from any checkout rather than
        // only the machine it was written on.
        var dll = args.Length > 0
            ? args[0]
            : Path.Combine("bin", "Release", "Logoria.dll");

        if (!File.Exists(dll))
        {
            Console.WriteLine($"No assembly at '{dll}'.");
            Console.WriteLine("Usage: dotnet run --project Tools\\VerifyRelease -- <path-to-Logoria.dll>");
            return 2;
        }

        // Where the Dalamud reference assemblies live. DALAMUD_HOME first, because
        // that is what CI sets after downloading the distribution; a build runner
        // has no XIVLauncher install, and reading that path unconditionally threw
        // DirectoryNotFoundException and failed the release step.
        var hooks = Environment.GetEnvironmentVariable("DALAMUD_HOME");

        if (string.IsNullOrWhiteSpace(hooks) || !Directory.Exists(hooks))
        {
            hooks = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "addon", "Hooks", "dev");
        }

        if (!Directory.Exists(hooks))
        {
            Console.WriteLine($"No Dalamud assemblies found at '{hooks}'.");
            Console.WriteLine("Set DALAMUD_HOME to a Dalamud distribution, or install XIVLauncher.");
            return 2;
        }

        Console.WriteLine($"resolving references from: {hooks}");

        var paths = Directory.GetFiles(hooks, "*.dll").ToList();
        paths.AddRange(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"));
        paths.Add(dll);

        var bySimpleName = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var n = Path.GetFileNameWithoutExtension(p);
            if (!bySimpleName.ContainsKey(n)) bySimpleName[n] = p;
        }

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(bySimpleName.Values), "System.Private.CoreLib");
        var asm = mlc.LoadFromAssemblyPath(dll);

        var banned = new[]
        {
            "DiagnosticsService", "DiagnosticsWindow", "CallbackCaptureService",
            "EurekaStateProbe", "CapturedEvent", "CapturedCallback", "ArrayCandidate",
        };

        var types = asm.GetTypes().Select(t => t.FullName ?? t.Name).ToList();
        Console.WriteLine($"assembly: {Path.GetFileName(dll)}   types: {types.Count}");

        var failed = false;

        foreach (var name in banned)
        {
            var hit = types.FirstOrDefault(t => t.Contains(name, StringComparison.Ordinal));
            if (hit != null) { Console.WriteLine($"  FAIL  type present: {hit}"); failed = true; }
            else Console.WriteLine($"  ok    absent: {name}");
        }

        // Referenced types tell you what the assembly can even reach for.
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).OrderBy(n => n).ToList();
        Console.WriteLine($"\nreferenced assemblies: {string.Join(", ", refs)}");

        foreach (var risky in new[] { "System.Net.Http", "System.Net.Sockets", "System.Net.Primitives" })
        {
            if (refs.Contains(risky)) { Console.WriteLine($"  FAIL  networking reference: {risky}"); failed = true; }
            else Console.WriteLine($"  ok    no reference to {risky}");
        }

        Console.WriteLine(failed ? "\nRESULT: FAILED" : "\nRESULT: clean");
        return failed ? 1 : 0;
    }
}
