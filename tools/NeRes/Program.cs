// Lists and extracts named resources from a 16-bit NE module.
//
// The game keeps its dungeon artwork in MG11.DLL as resources of type "MORDOR" with names like
// MAZE, DOOR, WATR, QSND, FOG and BLNK (MODULE12.BAS:77-103). Reading them by name is the only
// way to be sure which bitmap is which; guessing from file order would be a guess.
//
// usage: NeRes <module> [outdir]

if (args.Length < 1) { Console.Error.WriteLine("usage: NeRes <module> [outdir]"); return 1; }
byte[] f = File.ReadAllBytes(args[0]);
string? outDir = args.Length > 1 ? args[1] : null;

ushort U16(int at) => (ushort)(f[at] | (f[at + 1] << 8));

int ne = U16(0x3C);
if (f[ne] != 'N' || f[ne + 1] != 'E') { Console.Error.WriteLine("not an NE module"); return 1; }

int resTable = ne + U16(ne + 0x24);
int shift = U16(resTable);
int p = resTable + 2;

// Resource names live in a table of length-prefixed strings after the type/name records; an id
// with the high bit clear is an offset into the resource table rather than an integer id.
string NameOf(ushort id)
{
    if ((id & 0x8000) != 0) return "#" + (id & 0x7FFF);
    int at = resTable + id;
    int len = f[at];
    return System.Text.Encoding.ASCII.GetString(f, at + 1, len);
}

if (outDir is not null) Directory.CreateDirectory(outDir);

while (U16(p) != 0)
{
    string type = NameOf(U16(p));
    int count = U16(p + 2);
    p += 8;
    for (int i = 0; i < count; i++, p += 12)
    {
        int off = U16(p) << shift;
        int len = U16(p + 2) << shift;
        string name = NameOf(U16(p + 6));
        Console.WriteLine($"{type,-10} {name,-10} offset 0x{off:X6}  length {len,8}");
        if (outDir is null) continue;
        var bytes = f.AsSpan(off, Math.Min(len, f.Length - off)).ToArray();
        File.WriteAllBytes(Path.Combine(outDir, $"{type}_{name}.bin"), bytes);
    }
}
return 0;
