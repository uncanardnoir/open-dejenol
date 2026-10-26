using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.QoL;

/// <summary>
/// The "Store: slot and worn columns" toggle: what an item is worn as, and what the character in
/// command already wears there. The class is the game's own (Character.WearClassOf: the item type's
/// entry in gv1592 pointing into gv15C6, MDATA1), and so is whether it is worn at all
/// (Character.MaxWornOfClass: 1 for the fifteen worn classes, 0 for the eight consumables, -1 for
/// Miscellaneous Object). Only the short label is the mod's.
/// </summary>
public static class WearSlot
{
    /// <summary>The short labels, by the class's own name in MDATA1 (gv15C6).</summary>
    private static readonly Dictionary<string, string> Short = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Weapon"] = "Weapon",
        ["Armor"] = "Armor",
        ["Shield"] = "Shield",
        ["Head Protection"] = "Head",
        ["Hand Protection"] = "Hands",
        ["Cloth"] = "Body",
        ["Foot Protection"] = "Feet",
        ["Wrist Protection"] = "Wrists",
        ["Garment"] = "Garment",
        ["Ornament"] = "Ornament",
        ["Liquid"] = "Liquid",
        ["Particles"] = "Particles",
        ["Parchment"] = "Parchment",
        ["Book"] = "Book",
        ["Stick"] = "Stick",
        ["Neck Ornament"] = "Neck",
        ["Round Object"] = "Round",
        ["Square Object"] = "Square",
        ["Waist Protection"] = "Waist",
        ["Old Object"] = "Old Object",
        ["Element"] = "Element",
        ["Glittering Object"] = "Glittering",
        ["Crest"] = "Crest",
        ["Miscellaneous Object"] = "Misc",
    };

    /// <summary>The class's full name in MDATA1, or "" when the tables are not loaded or it is out of range.</summary>
    public static string ClassName(int wearClass) =>
        wearClass >= 0 && wearClass < Mdata1.ItemDescriptions.Count ? Mdata1.ItemDescriptions[wearClass].Name.Trim() : "";

    /// <summary>
    /// A wear class's short label: "Waist" for Waist Protection, "Body" for Cloth (the cloaks and
    /// robes), "Misc" for Miscellaneous Object; a name with no short form (a class a changed MDATA1
    /// added) is shown whole; "" for none.
    /// </summary>
    public static string Label(int wearClass)
    {
        string name = ClassName(wearClass);
        return name == "" ? "" : Short.TryGetValue(name, out var s) ? s : name;
    }

    /// <summary>An item's short label (its class's).</summary>
    public static string LabelOf(Item it) => Label(Character.WearClassOf(it));

    /// <summary>"Waist Protection - Belt": the class's full name and the item type's (gv1592).</summary>
    public static string Tooltip(Item it)
    {
        string cls = ClassName(Character.WearClassOf(it));
        string type = Mdata1.ItemTypeName(it.Type, "").Trim();
        return cls == "" ? type : type == "" ? cls : $"{cls} - {type}";
    }

    /// <summary>
    /// What <paramref name="ch"/> has on in the same wear class as <paramref name="it"/>, as the
    /// pack shows it (Loot.NameFor, at the slot's own level of knowledge, alignment and charges), or
    /// "" when nothing is, and always "" for a class that is not worn one at a time (MaxWornOfClass
    /// not above nought: the consumables and Miscellaneous Object). What counts as on is the
    /// game's own count of what is worn (Character.WornOfClass: every equipped slot of that class).
    /// </summary>
    public static string Worn(Character ch, Item it, Func<short, Item?> lookup)
    {
        int cls = Character.WearClassOf(it);
        if (Character.MaxWornOfClass(cls) <= 0) return "";
        foreach (var s in ch.Inventory)
        {
            if (!s.Equipped || lookup(s.ItemId) is not Item on || Character.WearClassOf(on) != cls) continue;
            return Loot.NameFor(on, s.Known, s.Alignment, s.Charges);
        }
        return "";
    }

    /// <summary>The Slot column's cell: the label, with the full names in the tooltip.</summary>
    public static ModCell SlotCell(Item it) => new(LabelOf(it), Tooltip(it));

    /// <summary>The Worn column's cell: what is on in that class, or blank.</summary>
    public static ModCell WornCell(Character ch, Item it, Func<short, Item?> lookup)
    {
        string on = Worn(ch, it, lookup);
        return new ModCell(on, on == "" ? null : "Worn now: " + on);
    }

    /// <summary>The pack line's label colour: a dim grey, apart from the row's text.</summary>
    public static readonly (byte R, byte G, byte B) PackColour = (140, 140, 155);

    /// <summary>
    /// The pack line's " [Waist]" after an item known at least Partially - the level at which the
    /// line already names the item's type (Loot.NameFor: "Belt"), from which the class follows, so
    /// it tells nothing the line does not. Barely known: nothing (the line then names the class
    /// itself, "Waist Protection", and the label would only repeat it). The Hands, MDATA3's item 0
    /// in pack slot 0, have none: they are the character's own, not something worn, and "1. Hands
    /// [Weapon]" read as if they were.
    /// </summary>
    public static PackRowDecoration? PackLabel(Item it, IdLevel known)
    {
        if (it.Id == 0 || known < IdLevel.Partial) return null;
        string label = LabelOf(it);
        return label == "" ? null : new PackRowDecoration($" [{label}]", PackColour, Tooltip(it));
    }
}
