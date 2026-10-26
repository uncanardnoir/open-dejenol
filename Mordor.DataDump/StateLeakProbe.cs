using Mordor.Core;

/// <summary>
/// The four states a spell can put up - Levitate, Invisible, See Invisible, Protect - cast one at
/// a time, to see which ones arrive alone.
///
/// recomputeEffects (MODULE6.BAS:2785, MAINMOD2.sub0894) runs one loop over bit positions 0 to 5
/// testing the gear mask and the spell mask together, and assigns with the *gear* meanings. But
/// spells record their states at bits 3 to 6, not 0 to 3, so three of the four spell bits land
/// inside that loop's range wearing the wrong name. Four explicit tests afterwards read the spell
/// mask correctly, so the right state is always set as well - the loop can only add.
///
/// This walks the arithmetic rather than asserting the conclusion: for each spell bit, which gear
/// case does the loop reach, and what does that case set?
/// </summary>
public static class StateLeakProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Item? Lookup(short id) => items.FirstOrDefault(i => i.Id == id);

        string[] gearName = { "Levitate", "Invisible", "Protect", "See Invisible", "Critical Hit", "Backstab" };
        var spellBit = new (string Name, int Bit)[]
        {
            ("Levitate", Effects.SpellLevitate), ("Invisible", Effects.SpellInvisible),
            ("See Invisible", Effects.SpellSeeInvisible), ("Protect", Effects.SpellProtect),
        };

        Console.WriteLine("the loop runs over bits 0..5 and reads them with the gear meanings:");
        for (int b = 0; b < 6; b++)
            Console.WriteLine($"  bit {b}  ->  {gearName[b]}{(b >= 4 ? "   (no state: case 4 is empty, case 5 absent)" : "")}");

        Console.WriteLine("\na spell writes its state at a different bit, so the loop misreads it:");
        foreach (var (name, bit) in spellBit)
        {
            string reached = bit > 5 ? "outside the loop entirely"
                           : bit >= 4 ? $"the loop's case {bit}, which sets nothing"
                           : $"the loop's case {bit}, which sets {gearName[bit]}";
            Console.WriteLine($"  {name,-14} writes bit {bit}  ->  {reached}");
        }

        Console.WriteLine("\ncast end to end, one spell each, on a character with nothing equipped:");
        foreach (var (name, _) in spellBit)
        {
            int effect = name switch
            {
                "Levitate" => SpellEffect.Levitate, "Invisible" => SpellEffect.Invisible,
                "See Invisible" => SpellEffect.SeeInvisible, _ => SpellEffect.Protect,
            };
            var s = spells.FirstOrDefault(x => x.Effect == effect && x.Name.Trim().Length > 0);
            if (s == null) { Console.WriteLine($"  {name,-14} no spell in MDATA2 has that effect"); continue; }

            var c = Fresh();
            SpellEffect.ApplyToCharacter(effect, c, s, new Random(1), Lookup);
            Console.WriteLine($"  {name,-14} \"{s.Name.Trim()}\" (effect {effect})  ->  "
                            + $"levitating {c.Levitating,-5} invisible {c.InvisibleNow,-5} "
                            + $"sees invisible {c.SeeingInvisible,-5} protection {c.Protection}");
        }

        Console.WriteLine("\nand the same four as equipped gear, where the loop is right:");
        for (int b = 0; b < 4; b++)
        {
            var it = items.FirstOrDefault(i => i.Id != 0 && (i.Abilities & (1 << b)) != 0);
            if (it == null) { Console.WriteLine($"  {gearName[b],-14} no item carries that bit"); continue; }
            var c = Fresh();
            c.PutInPack(new InventorySlot { ItemId = it.Id, Charges = 1, Known = IdLevel.All, Equipped = true });
            Effects.Recompute(c, Lookup);
            Console.WriteLine($"  {gearName[b],-14} \"{it.Name.Trim()}\" (abilities 0x{it.Abilities:X})  ->  "
                            + $"levitating {c.Levitating,-5} invisible {c.InvisibleNow,-5} "
                            + $"sees invisible {c.SeeingInvisible,-5} protection {c.Protection}");
        }
    }

    /// <summary>A character with no gear and one guild, so every state seen came from the cast.</summary>
    public static Character Fresh()
    {
        var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = "Mage", Level = 10 });
        foreach (var slot in c.Inventory) slot.Equipped = false;
        return c;
    }
}
