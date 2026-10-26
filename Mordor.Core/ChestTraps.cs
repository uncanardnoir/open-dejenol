namespace Mordor.Core;

/// <summary>Where a Teleporter trap threw the party, in 1-based map coordinates.</summary>
public readonly record struct Teleported(int X, int Y);

/// <summary>What setting off a chest trap did.</summary>
public sealed class TrapResult
{
    public List<string> Lines { get; } = new();
    /// <summary>Set by the Teleporter trap; the caller moves the opener alone (MONSMOD.BAS:3572-3577).</summary>
    public Teleported? TeleportTo { get; set; }
}

/// <summary>
/// The chest a beaten encounter leaves behind, from <c>monOpenChest</c> in MONSMOD.BAS.
///
/// Ten traps, numbered as the original numbers them in gv08AA$ (MODULE1.BAS:408-418). The first
/// four hit the whole party; the rest hit only whoever opened the chest. Which is which is not a
/// judgement call - the source loops <c>For l0500 = 0 To UBound(Chars)</c> for exactly those four
/// and uses <c>pv04F2</c>, the opener, for the others.
///
/// **What a trap says.** Across all ten arms monOpenChest calls sub085B exactly twice - "SLIME:
/// &lt;item&gt; Destroyed!" and "Your companions were scared away!". Everything else is silent:
/// what tells you which trap fired is its name, written into lblTrap.Caption (MONSMOD.BAS:3560)
/// and carried here by the caller's _trapCaption. Six lines of this port's own invention were
/// removed on 2026-09-22 - see docs/FIDELITY.md.
/// </summary>
public static class ChestTraps
{
    /// <summary>gv08AA$: the names the original prints, indexed by trap number.</summary>
    public static readonly string[] Names =
    {
        "Not Trapped", "Poison Gas", "Disease Spores", "Exploding Trap", "Fire Trap", "Slime",
        "Fate", "Teleporter", "Blackout", "Fear", "Withering",
    };

    // Resistance kinds, in the order MDATA5/MDATA3 store them.
    private const int ResistFire = 0, ResistDisease = 4, ResistPoison = 5;

    /// <summary>mc0582: how many days a Withering trap adds to your age.</summary>
    public const int WitheringDays = 28;

    /// <summary>mc04D0: the pack is forty slots, of which one to thirty-nine hold items.</summary>
    public const int PackSlots = 40;

    /// <summary>
    /// fn11CA (MAINMOD2.BAS:652-656): everything can be destroyed except an item whose SpecialType
    /// falls in the guild quest range, 500 up to one less than 500 plus the number of guilds.
    /// </summary>
    public static bool Destructible(Item it) =>
        it.SpecialType < 500 || it.SpecialType > 500 + GuildData.All.Count - 1;

    /// <summary>
    /// Set off trap number <paramref name="trap"/>. <paramref name="party"/> is everyone present,
    /// <paramref name="opener"/> the one who tried the chest, and <paramref name="walls"/> answers
    /// with the wall word of a square given 1-based x and y, which the Teleporter trap needs.
    /// </summary>
    public static TrapResult Spring(
        int trap, IReadOnlyList<Character> party, Character opener, int dungeonLevel,
        Random rng, Func<short, Item?> item, Func<int, int, int> walls)
    {
        var r = new TrapResult();
        switch (trap)
        {
            case 1:     // Poison Gas
            case 2:     // Disease Spores
            {
                bool poison = trap == 1;
                int kind = poison ? ResistPoison : ResistDisease;
                foreach (var c in party)
                {
                    // As written, monOpenChest afflicts you when the roll falls *under* half your
                    // resistance, which reads backwards - see docs/FIDELITY.md. Reproduced.
                    if (!(rng.NextDouble() * 100 < Effects.ResistancePercent(c, kind, item) / 2.0)) continue;
                    if (poison) { c.Poisoned = true; c.PoisonLevel = dungeonLevel; }
                    else { c.Diseased = true; c.DiseaseLevel = dungeonLevel; }
                    // and says nothing: the trap's name in lblTrap is the whole announcement
                }
                break;
            }

            case 3:     // Exploding Trap - no resistance applies
            case 4:     // Fire Trap
            {
                int mult = trap == 3 ? 4 : 6;
                foreach (var c in party)
                {
                    double dmg = (int)(rng.NextDouble() * (dungeonLevel * mult * Math.Log(dungeonLevel + 2))) + 1;
                    if (trap == 4)
                        dmg -= dmg * (Effects.ResistancePercent(c, ResistFire, item) / 2.0 * 0.01);
                    // Hits - (a Single) goes back into an Integer, rounded half to even (MONSMOD.BAS:3530-3536)
                    c.CurHits = (int)Math.Round(c.CurHits - dmg, MidpointRounding.ToEven);
                }
                break;
            }

            case 5:     // Slime - destroys one item
            {
                if (rng.Next(100) + 1 >= 50 + dungeonLevel * 2) break;
                // The original rolls a *slot* in the 40-slot pack, not one of the items you have,
                // so a light pack usually means the roll lands on nothing. Equipped gear is not
                // spared; only guild quest items are.
                int slot = rng.Next(PackSlots - 1) + 1;
                var held = opener.Pack[slot];
                if (held == null || held.ItemId <= 0) break;
                var it = item(held.ItemId);
                if (it == null || !Destructible(it)) break;
                opener.RemoveFromPack(held);
                r.Lines.Add($"SLIME: {it.Name} Destroyed!");
                break;
            }

            case 6:     // Fate - sub0F29: both pools are rerolled, which can help or hurt
            {
                opener.CurHits = (int)(rng.NextDouble() * opener.MaxHits) + 1;
                opener.CurSpellPoints = (int)(rng.NextDouble() * opener.MaxSpellPoints) + 1;
                break;
            }

            case 7:     // Teleporter - anywhere on the level that is not rock and not a teleporter
            {
                const int Rock = 19, Teleporter = 14;
                for (int guard = 0; guard < 10000; guard++)
                {
                    int x = rng.Next(30) + 1, y = rng.Next(30) + 1;
                    int w = walls(x, y);
                    if ((w & (1 << Rock)) != 0 || (w & (1 << Teleporter)) != 0) continue;
                    r.TeleportTo = new Teleported(x, y);
                    break;
                }
                break;
            }

            case 8:     // Blackout - every spell you have running goes out
            {
                Effects.ClearSpellEffects(opener, item);
                break;
            }

            case 9:     // Fear - only a companion bound for life stands its ground
            {
                // the fallen are not scared off (MONSMOD.BAS:3618) and keep their slots
                int fled = 0;
                foreach (var (slot, c) in opener.Companions.Occupied.ToList())
                    if (c.Hits > 0 && c.BindingLevel != Binding.ForLife) { opener.Companions.Release(slot); fled++; }
                // "If M5943 < mc01A0 Then M5943 = -1" (MONSMOD.BAS:3625), whoever fled: a dead
                // companion being carried is put down and left lying in its slot, uncarried
                if (opener.CarryingCompanion < CompanionSlots.Slots) opener.CarryingCompanion = -1;
                if (fled > 0) r.Lines.Add("Your companions were scared away!");
                break;
            }

            case 10:    // Withering
            {
                // mdrcharAddAge True, pv04F2, mc0582, False (MONSMOD.BAS:3633): 28 days is "4 week(s)",
                // said as "You are now 4 week(s) older." or with the name in a party (MAINMOD2.BAS:6400-6405)
                string older = opener.AgeBy(WitheringDays, party.Count > 1);
                if (older != "") r.Lines.Add(older);
                break;
            }
        }

        foreach (var c in party) if (c.CurHits < 1) c.CurHits = 0;
        return r;
    }
}
