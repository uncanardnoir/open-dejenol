using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Mordor.Modding;

/// <summary>
/// A mod built against another Avalonia than the game's (API version 18, ModApi.AvaloniaMajor): the
/// game moved to Avalonia 12, which took out what 11 had marked obsolete, so a mod built against 11
/// would load and fail later at a call that is gone. The loader turns it away before any of it runs,
/// and the Mods page says why and what to do. The mods here are assemblies written by the probe with
/// nothing in them but their references, an Avalonia 11 one and an Avalonia 12 one.
/// </summary>
public static partial class ModProbe
{
    private static void AvaloniaChecks(string scratch)
    {
        Console.WriteLine("a mod built against another Avalonia (API version 18):");
        string mods = Path.Combine(scratch, "avalonia-mods");
        RefsOnlyMod(mods, "OldUi", new Version(11, 0, 0, 0));
        RefsOnlyMod(mods, "NewUi", new Version(ModApi.AvaloniaMajor, 2, 0, 0));
        string ini = Path.Combine(scratch, "avalonia.ini");
        var st = new ModSettingsStore(ini);
        st.SetEnabled("OldUi", true); st.SetEnabled("NewUi", true);
        var log = new List<string>();
        var h = Host(ini, log, mods);

        string want = "it was built against Avalonia 11, and this version of the game runs on Avalonia 12: "
                      + $"it must be rebuilt against the game's mod API (version {ModApi.ApiVersion}, docs/MODDING.md)";
        var old = h.Loaded.SingleOrDefault(m => m.Id == "OldUi");
        Check("the game runs Avalonia 12, API version 18", $"{ModApi.AvaloniaMajor} {ModApi.ApiVersion}", "12 18");
        Check("an Avalonia 11 mod is not loaded, and not running", $"{old != null} {old?.Instance == null} {old?.Running}", "True True False");
        Check("... it says why: built against Avalonia 11, rebuild against the mod API", old?.Refused, want);
        Check("... the Mods page says it cannot be loaded, and why", h.StatusOf("OldUi", true), "Cannot be loaded: " + want + ".");
        Check("... one line in the log, with no stack trace", log.Count(l => l == "mod OldUi: not loaded - " + want), 1);
        var fresh = h.Loaded.SingleOrDefault(m => m.Id == "NewUi");
        Check("an Avalonia 12 mod is not turned away (this one has no entry type, and fails on that instead)",
              $"{fresh?.Refused == null} {fresh?.FaultReason?.Contains("Avalonia")}", "True False");

        // the main menu's Enabled box: switched on there, it is refused the same way
        string ini2 = Path.Combine(scratch, "avalonia-late.ini");
        var late = Host(ini2, log, mods);
        Check("switched on on the main menu: refused, with the same reason",
              $"{late.Switch("OldUi", true)} {late.StatusOf("OldUi", true) == "Cannot be loaded: " + want + "."}", $"{ModSwitchResult.Failed} True");
    }

    /// <summary>
    /// A mod folder whose assembly is nothing but a manifest naming System.Runtime and Avalonia.Base of
    /// <paramref name="avalonia"/>'s version: what the loader reads before it runs anything of a mod.
    /// </summary>
    private static void RefsOnlyMod(string root, string id, Version avalonia)
    {
        string d = Path.Combine(root, id);
        Directory.CreateDirectory(d);
        var md = new MetadataBuilder();
        md.AddModule(0, md.GetOrAddString(id + ".dll"), md.GetOrAddGuid(Guid.NewGuid()), default, default);
        md.AddAssembly(md.GetOrAddString(id), new Version(1, 0, 0, 0), default, default, default, AssemblyHashAlgorithm.Sha1);
        md.AddAssemblyReference(md.GetOrAddString("System.Runtime"), new Version(10, 0, 0, 0), default,
                                md.GetOrAddBlob(ImmutableArray.Create<byte>(0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a)), default, default);
        md.AddAssemblyReference(md.GetOrAddString("Avalonia.Base"), avalonia, default,
                                md.GetOrAddBlob(ImmutableArray.Create<byte>(0xc8, 0xd4, 0x84, 0xa7, 0x01, 0x2f, 0x9a, 0x8b)), default, default);
        md.AddTypeDefinition(default, default, md.GetOrAddString("<Module>"), default,
                             MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(md), new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        using (var f = File.Create(Path.Combine(d, id + ".dll"))) image.WriteContentTo(f);
        File.WriteAllText(Path.Combine(d, "mod.json"),
            "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"version\": \"1\", \"gameVersion\": \"1.1\", "
            + "\"assembly\": \"" + id + ".dll\", \"entryType\": \"" + id + ".Entry\" }");
    }
}
