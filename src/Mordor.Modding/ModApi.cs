using Mordor.Core;

namespace Mordor.Modding;

/// <summary>A mod's entry point. The class named by the manifest's <c>entryType</c> implements it.</summary>
public interface IMod
{
    /// <summary>
    /// Called once, at startup, for an enabled mod - or, for an <see cref="IModLifecycle"/> mod,
    /// the first time it is switched on on the main menu's Mods page. Wire up hooks here. An exception thrown from
    /// here disables the mod for the session (and undoes its Harmony patches); the game carries on.
    /// </summary>
    void Load(IModContext ctx);
}

/// <summary>
/// A mod that can be switched on and off without a restart (API version 7), on the main menu's Mods
/// page - the only place mods are switched, with no game window open. Implemented beside
/// <see cref="IMod"/> by the entry class; a mod without it is switched on and off only at the next
/// start, as before.
///
/// The lifecycle: <see cref="IMod.Load"/> once, the first time the mod is switched on (at startup,
/// or on the main menu's Mods page), to wire its hooks into the context - and never again in the
/// session, so nothing is subscribed twice. Then <see cref="OnEnable"/> each time it becomes active
/// (straight after Load, and after every switch back on) and <see cref="OnDisable"/> each time it
/// is switched off. While it is off the host calls none of its hooks, events, decisions, actions or
/// UI slots (a game begun after it carries none of them; a mod window left open closes) -
/// and after <see cref="OnDisable"/> the host removes every Harmony patch made with its
/// <see cref="IModContext.Harmony"/> (<c>UnpatchAll(id)</c>). So a mod that patches does it in
/// <see cref="OnEnable"/>, not in Load, and the patches come back with it.
///
/// Either call that throws disables the mod for the session, as any other hook.
/// </summary>
public interface IModLifecycle
{
    /// <summary>The mod is active now: after Load, and after each switch back on. (Re)apply Harmony patches here.</summary>
    void OnEnable();

    /// <summary>
    /// The mod was switched off: its hooks are no longer called. Flush what it keeps (a file waiting
    /// to be written), stop timers of its own. Its Harmony patches are removed by the host after this.
    /// </summary>
    void OnDisable();
}

/// <summary>what switching a mod on or off on the main menu's Mods page did (<see cref="ModHost.Switch"/>).</summary>
public enum ModSwitchResult
{
    /// <summary>Done now: the mod is running, or not running, as asked.</summary>
    Now,
    /// <summary>Kept in settings.ini; it takes effect at the next start (a mod without <see cref="IModLifecycle"/>, or one disabled for throwing).</summary>
    NextStart,
    /// <summary>Switched on, but it threw as it loaded or enabled: disabled for the session (see crash.log).</summary>
    Failed,
}

/// <summary>A mod's handle on the game. Each mod gets its own.</summary>
public interface IModContext
{
    ModManifest Manifest { get; }
    /// <summary>The mod's own folder, where its manifest and .dll are.</summary>
    string Directory { get; }
    /// <summary>Things that happened. Observe only.</summary>
    ModEvents Events { get; }
    /// <summary>Questions the game asks. Leave one unset and the game does what the original did.</summary>
    ModDecisions Decisions { get; }
    /// <summary>
    /// The moments a mod may act through the game's own commands (casting, using an item), awaited
    /// by the game before it goes on. Leave one unset and nothing happens there.
    /// </summary>
    ModActions Actions { get; }
    /// <summary>Places a mod can add to the game's windows.</summary>
    ModUI UI { get; }
    /// <summary>The mod's settings, kept in settings.ini under <c>[Mod:id]</c>.</summary>
    ModSettings Settings { get; }
    /// <summary>Lines into the port's log (crash.log beside the game folder).</summary>
    ModLog Log { get; }
    /// <summary>
    /// The game in play now, read live: the party, the one in command, the spell and item tables -
    /// for a mod's own window (<see cref="ModUI.AddWindow"/>). Nothing (no party) on the title.
    /// </summary>
    ModGame Game { get; }
    /// <summary>
    /// A folder for the mod's own files: the saves folder, where settings.ini is (so
    /// <c>MORDOR_SAVES</c> moves it). A mod names its files after itself (the Kaizen mod's
    /// <c>kaizen.json</c>).
    /// </summary>
    string DataDirectory { get; }
    /// <summary>
    /// A Harmony instance whose id is the mod's id, for patching what has no hook. Every patch it
    /// makes during <see cref="IMod.Load"/> is written to the log, and all of them are removed if
    /// the mod is disabled for throwing.
    /// </summary>
    HarmonyLib.Harmony Harmony { get; }
}

// ---------------------------------------------------------------------------------------- events

/// <summary>A switch of the character in command.</summary>
public sealed record CharacterSwitch(Character Now, bool InDungeon);

/// <summary>A row of the Store's buy list was clicked.</summary>
/// <param name="Character">Who is shopping.</param>
/// <param name="Item">The row's item.</param>
/// <param name="WhileChoosing">Select Alignment was already up for another row.</param>
/// <param name="Taken">Whether the click took the row (false: it only beeped).</param>
public sealed record StoreRowClick(Character Character, Item Item, bool WhileChoosing, bool Taken);

/// <summary>Which of the dungeon's captions a line was shown in.</summary>
public enum CaptionKind
{
    /// <summary>lblCharacterHit: a blow of the character being watched (MONSMOD.BAS:444-504).</summary>
    CharacterHit,
    /// <summary>lblMonsterHit: a monster's attack on the character being watched.</summary>
    MonsterHit,
    /// <summary>lblMonsterHit: a friendly group's offer to join (MONSMOD.BAS:4343-4346).</summary>
    JoinOffer,
    /// <summary>lblCharacterHit: monGetGold's share line (MONSMOD.BAS:2158, 2174).</summary>
    GoldShare,
    /// <summary>lblCharacterHit: the chest-opening count's caption.</summary>
    Chest,
    /// <summary>lblTrap: the trap readout on a chest, or what opening it left ("Not Trapped", "Trap Disarmed", the trap).</summary>
    Trap,
}

/// <summary>
/// A line the original shows only in one of its captions, never in Messages: it is overwritten by
/// the next one and gone.
/// </summary>
/// <param name="Kind">Which caption.</param>
/// <param name="Text">What it said.</param>
/// <param name="Character">Whose it is, where the line has one (the attacker, or the one hit).</param>
public sealed record Caption(CaptionKind Kind, string Text, Character? Character);

/// <summary>A sold item went onto the Store's shelf.</summary>
/// <param name="Character">Who sold it.</param>
/// <param name="Item">The item.</param>
/// <param name="Alignment">The shelf row it went to: the sold instance's alignment.</param>
/// <param name="Say">Write a line to the Messages window.</param>
public sealed record StoreShelf(Character Character, Item Item, ItemAlignment Alignment, Action<string> Say);

/// <summary>How the party came back to town from the dungeon.</summary>
public enum TownArrivalKind
{
    /// <summary>Walked out alive: the stairs up from level 1 (MAINMOD2.BAS:3955-3959), or any other way a living party comes back to town.</summary>
    Walked,
    /// <summary>Came back dead: the body carried up by Wait for Rescue (FRMDUNM.BAS:144-196); the town shows the dead screen.</summary>
    Rescued,
}

/// <summary>
/// The party is back in town from the dungeon. Not raised at the game's start, nor for Exit to
/// Main Menu (which goes to the title, not the town), nor for a session ending below.
/// </summary>
/// <param name="Party">The party, as it arrived.</param>
/// <param name="Selected">The character in command.</param>
/// <param name="How">How it came up.</param>
public sealed record TownArrival(Party Party, Character Selected, TownArrivalKind How);

/// <summary>
/// Things that happened. Handlers cannot change the outcome. A handler that throws disables its
/// mod for the session.
/// </summary>
public sealed class ModEvents
{
    /// <summary>A line was added to the Messages window (<c>sub085B</c>).</summary>
    public event Action<string>? MessageLogged;
    /// <summary>Another character took command (in town or in the dungeon).</summary>
    public event Action<CharacterSwitch>? CharacterSwitched;
    /// <summary>A row of the Store's buy list was clicked.</summary>
    public event Action<StoreRowClick>? StoreRowClicked;
    /// <summary>
    /// A caption was put up in the dungeon (<see cref="CaptionKind"/>): the hit lines, the gold
    /// share, the chest and trap captions. Raised as the caption is set, with its text; an emptied
    /// caption raises nothing.
    /// </summary>
    public event Action<Caption>? CaptionShown;
    /// <summary>
    /// The party is back in town from the dungeon (<see cref="TownArrival"/>), after the town's
    /// rest and after whatever <see cref="ModDecisions.TownArrival"/> asked for was done.
    /// </summary>
    public event Action<TownArrival>? ArrivedInTown;
    /// <summary>
    /// A sale in the Store put the item on the shelf (cmdSell_Click, FRMCTYS.BAS:264-270: not
    /// cursed, not floor-0 unaligned stock, that alignment's row below the cap). Not raised for a
    /// sale that shelves nothing.
    /// </summary>
    public event Action<StoreShelf>? StoreShelved;
    /// <summary>
    /// A character was deleted on the main menu (Delete Character, once its save is gone), with its
    /// name: a mod that keeps something of its own per character (the Kaizen mod's buff lists) lets
    /// it go. The only way a character is deleted.
    /// </summary>
    public event Action<string>? CharacterDeleted;
    /// <summary>
    /// Hits taken off a monster, or a monster killed, by a party member or one of their companions
    /// (API version 5), raised by the combat engine as it happens (<see cref="CombatDamage"/>): the
    /// true figures, shown or not - each blow, breath, spit, stone, shock or spell, once per monster
    /// struck, with the hits it actually took (capped at what the monster had left), and each kill
    /// on its own. Kinds: Physical (a character's weapon), Magical (a character's spell, their own
    /// or an item's), Companion (credited to the owner). Observe only; with no mod listening the
    /// engine does nothing more.
    /// </summary>
    public event Action<CombatDamage>? DamageDealt;
    /// <summary>
    /// Experience put on a party member's total (API version 8), raised by the combat engine at
    /// every award (<see cref="CombatExperience"/>): sub0F11, whose only callers are damage dealt -
    /// the character's own blow, spell or charm, or a companion's damage credited to its owner at a
    /// quarter (<see cref="ExperienceSource.Damage"/>) - and a chest's trap disarmed
    /// (<see cref="ExperienceSource.Chest"/>). The amount is what the total actually went up by, after
    /// the clamp at the pin. Observe only; with no mod listening the engine does nothing more.
    /// </summary>
    public event Action<CombatExperience>? ExperienceGained;

    internal void RaiseDamageDealt(CombatDamage d) => DamageDealt?.Invoke(d);
    internal void RaiseExperienceGained(CombatExperience e) => ExperienceGained?.Invoke(e);
    internal void RaiseCharacterDeleted(string name) => CharacterDeleted?.Invoke(name);
    internal void RaiseStoreShelved(StoreShelf s) => StoreShelved?.Invoke(s);
    internal void RaiseArrivedInTown(TownArrival a) => ArrivedInTown?.Invoke(a);
    internal void RaiseMessageLogged(string line) => MessageLogged?.Invoke(line);
    internal void RaiseCharacterSwitched(CharacterSwitch s) => CharacterSwitched?.Invoke(s);
    internal void RaiseStoreRowClicked(StoreRowClick c) => StoreRowClicked?.Invoke(c);
    internal void RaiseCaptionShown(Caption c) => CaptionShown?.Invoke(c);
}

// ------------------------------------------------------------------------------------------ game

/// <summary>
/// The game in play, as a mod's own window needs it: who is in the party, who is in command, what
/// they know and carry. Read live - the same <see cref="Character"/> objects the game plays with -
/// and on the UI thread. <see cref="Changed"/> says when to look again. A mod reads it; changing
/// what it hands out is changing the game, which a mod does only through its hooks.
/// </summary>
public sealed class ModGame
{
    /// <summary>The party in play, in its order; null on the title, before a game or after one.</summary>
    public Party? Party { get; private set; }
    /// <summary>The character in command (<c>Party.Selected</c>), or null with no party.</summary>
    public Character? Selected => Party is { Members.Count: > 0 } p ? p.Selected : null;
    /// <summary>Every spell (MDATA2), in table order; empty with no game.</summary>
    public IReadOnlyList<Spell> Spells { get; private set; } = Array.Empty<Spell>();
    /// <summary>The item table (MDATA3), by id.</summary>
    public Func<short, Item?> Item { get; private set; } = _ => null;
    /// <summary>The spells this member knows (<c>Spellbook.Known</c>, the spell page's list).</summary>
    public IReadOnlyList<Spell> KnownSpells(Character c) => Spellbook.Known(c, Spells);

    /// <summary>
    /// The game began or ended, another character took command, or the party was drawn again
    /// (members joined or left, a blow, a spell): look again. Raised often - a handler should
    /// redraw only what has changed. A handler that throws disables its mod, as any other hook.
    /// </summary>
    public event Action? Changed;

    public ModGame() { }

    internal ModGame(Func<ModHost.PickBox?> box) { _box = box; }

    private readonly Func<ModHost.PickBox?> _box = () => null;

    /// <summary>
    /// The game's own "Cast on Who?" box for <paramref name="spell"/> (API version 4) - the one a
    /// cast by hand in the dungeon shows, titled <c>Cast "Spell"</c> and listing the party in its
    /// order - and the member picked, or null when it was cancelled (or with no party, or no
    /// window to show it over, as in a headless run). Only the party is offered, never companions.
    /// Nothing is cast: the answer is the mod's to keep (the Kaizen window's row target).
    /// <paramref name="over"/> is a control of the mod's own window (an Avalonia <c>Control</c>,
    /// typed as object); the box is shown over the game's frame that window belongs to, so the
    /// keyboard goes back to the game when it closes. Call it on the UI thread, and not from
    /// inside a drag-and-drop handler (post it): it waits for the player.
    /// </summary>
    public Task<Character?> AskCastOnWho(Spell spell, object? over = null) => AskCastOnWho(spell, over, null);

    /// <summary>
    /// <see cref="AskCastOnWho(Spell, object?)"/> with the box starting on <paramref name="current"/>, a member's
    /// name (the row's target being changed), or on the first member when nobody of that name is in the party.
    /// </summary>
    public async Task<Character?> AskCastOnWho(Spell spell, object? over, string? current)
    {
        var party = Party;
        if (party == null || party.Members.Count == 0 || _box() is not ModHost.PickBox box) return null;
        var people = party.Members.ToList();
        int start = Math.Max(0, people.FindIndex(m => string.Equals(m.Name.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase)));
        int pick = await box($"Cast \"{spell.Name.TrimEnd()}\"", Spellbook.CastOnWho, people.Select(m => m.Name.TrimEnd()).ToList(), over, start);
        // the party changed while the box was up (a game ended): nobody
        return pick >= 0 && pick < people.Count && ReferenceEquals(Party, party) ? people[pick] : null;
    }

    internal void Set(Party? party, IReadOnlyList<Spell> spells, Func<short, Item?> item)
    {
        Party = party; Spells = spells; Item = item;
    }

    internal void RaiseChanged() => Changed?.Invoke();
}

/// <summary>What a <see cref="ModDragged"/> carries: a spell from a Spells list, or a pack slot.</summary>
public enum ModDragKind { Spell, PackItem }

/// <summary>
/// A drag from one of the game's own lists, as a mod's window may take it (API version 4): the
/// same drags that set the spell buffers - a line of the Spells list in Objects &amp; Spells (in
/// town and in the dungeon), or a line of the pack. <paramref name="Owner"/> is the character
/// whose list it came from (the one in command); <paramref name="Index"/> is the spell's place in
/// the spell table (<see cref="ModGame.Spells"/>) or the pack slot (0 is the Hands, and an empty
/// slot is dragged too).
///
/// The game puts it in the drag's data under <see cref="Format"/> beside its own; a mod's
/// drop target reads it with Avalonia's <c>e.Data.Get(ModDragged.Format) as ModDragged</c>
/// (<c>DragDrop.DragOverEvent</c>, <c>DragDrop.DropEvent</c>, the effect <c>Link</c>). It says
/// which line was dragged, nothing more: what the line shows the player (an item's level of
/// identification included) is the mod's to respect - read <see cref="Slot"/>'s <c>Known</c>
/// before anything else about the item.
/// </summary>
public sealed record ModDragged(ModDragKind Kind, Character Owner, int Index)
{
    /// <summary>The name it travels under in the drag's data.</summary>
    public const string Format = "mordor/mod-dragged";

    /// <summary>The dragged spell, or null (not a spell, or no such place in <paramref name="spells"/>).</summary>
    public Spell? SpellIn(IReadOnlyList<Spell> spells) =>
        Kind == ModDragKind.Spell && Index >= 0 && Index < spells.Count ? spells[Index] : null;

    /// <summary>The dragged pack slot's contents, or null: not a pack line, the Hands, or an empty slot.</summary>
    public InventorySlot? Slot =>
        Kind == ModDragKind.PackItem && Index > 0 && Index < Character.PackSize && !Owner.SlotEmpty(Index) ? Owner.Pack[Index] : null;
}

// --------------------------------------------------------------------------------------- actions

/// <summary>
/// The dungeon, handed to a <see cref="ModActions"/> hook: the party as it stands, the game's
/// tables, and two commands that run the dungeon's own cast and Use paths for a party member with
/// a target chosen beforehand instead of the "Cast on Who?" box. Spell points, charges, the lines,
/// the sounds and every refusal are the game's, as a cast by hand.
///
/// Valid only while its hook runs: once the hook's task has finished, <see cref="Cast"/> and
/// <see cref="UseItem"/> refuse (false) and do nothing, so a mod cannot act at any other moment.
/// </summary>
public sealed class DungeonContext
{
    private readonly Func<bool> _monsters;
    private readonly Func<Character, Spell, Character?, Task<bool>> _cast;
    private readonly Func<Character, InventorySlot, Character?, Task<bool>> _use;
    private readonly Action<string> _say;

    /// <param name="party">The party, in its order.</param>
    /// <param name="spells">Every spell (MDATA2), in table order.</param>
    /// <param name="item">The item table (MDATA3), by id.</param>
    /// <param name="monstersPresent">Whether monsters stand on the party's square, engaged or not, now.</param>
    /// <param name="cast">The game's cast for a caster, a spell and a target (null: nobody chosen).</param>
    /// <param name="useItem">The game's Use for a caster, a pack slot and a target (null: nobody chosen).</param>
    /// <param name="say">A line to the Messages window.</param>
    /// <param name="lastRefusal">The game's refusal (<c>CastGate</c>) of the last command, or null.</param>
    public DungeonContext(Party party, IReadOnlyList<Spell> spells, Func<short, Item?> item, Func<bool> monstersPresent,
                          Func<Character, Spell, Character?, Task<bool>> cast,
                          Func<Character, InventorySlot, Character?, Task<bool>> useItem,
                          Action<string> say, Func<RefusalKind?>? lastRefusal = null)
    {
        Party = party; Spells = spells; Item = item;
        _monsters = monstersPresent; _cast = cast; _use = useItem; _say = say; _refusal = lastRefusal;
    }

    private readonly Func<RefusalKind?>? _refusal;

    /// <summary>
    /// What the game's gate (<c>CastGate</c>) said to the last <see cref="Cast"/> or
    /// <see cref="UseItem"/>, if it refused it: <c>RefusalKind.AntiMagic</c> for "Anti-Magic area!"
    /// (an anti-magic or extinguish square), the points, the spell level and the rest. Null when
    /// the last command was not refused by the gate (it went through, or was refused before the
    /// game was asked). Only what the game has just said aloud.
    /// </summary>
    public RefusalKind? LastRefusal => _refusal?.Invoke();

    /// <summary>The party, in order (<c>Party.Members</c>): hits, spell points, guilds, packs.</summary>
    public Party Party { get; }
    public IReadOnlyList<Character> Members => Party.Members;
    /// <summary>Every spell (MDATA2), in table order.</summary>
    public IReadOnlyList<Spell> Spells { get; }
    /// <summary>The item table (MDATA3), by id.</summary>
    public Func<short, Item?> Item { get; }
    /// <summary>Monsters on the party's square now, engaged or waiting (<c>fn0EEF() &gt; 0</c>).</summary>
    public bool MonstersPresent => _monsters();
    /// <summary>False once the hook it was handed to has finished.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>The spells this member knows (<c>Spellbook.Known</c>, the spell page's list).</summary>
    public IReadOnlyList<Spell> KnownSpells(Character c) => Spellbook.Known(c, Spells);

    /// <summary>
    /// Cast a spell as <paramref name="caster"/>, through the dungeon's own cast (the gate, then
    /// the effect, the points after it). <paramref name="target"/> stands for the "Cast on Who?"
    /// pick and is used only by a spell that asks it; a spell that asks nobody (a party-wide one)
    /// ignores it. True when the cast went through. False, with nothing done, while monsters are
    /// present (a cast would become a combat order), for a caster not in the party or dead, for a
    /// spell that asks and no target in the party, and for a spell whose case needs a choice other
    /// than a party member (a map spell, a raise, a bind): those are left to the player. The game's
    /// own refusals (not enough points, an anti-magic square...) are said as by hand - as if the caster were the one in command, in the "you" wording - and give false.
    /// </summary>
    public Task<bool> Cast(Character caster, Spell spell, Character? target = null) =>
        IsOpen ? _cast(caster, spell, target) : Task.FromResult(false);

    /// <summary>
    /// Use the item in <paramref name="slot"/> of <paramref name="caster"/>'s pack, through the
    /// game's Use (its refusals, then the spell the item holds at the item's level, the charge only
    /// when the cast went through). Only an item known at <c>IdLevel.All</c> and holding a spell is
    /// used (below All the game shows nothing of an item's spell, so a mod is told nothing about it
    /// either: refused before anything is looked at); otherwise as <see cref="Cast"/>, and refused
    /// while the caster is held fast, as the Use button is.
    /// </summary>
    public Task<bool> UseItem(Character caster, InventorySlot slot, Character? target = null) =>
        IsOpen ? _use(caster, slot, target) : Task.FromResult(false);

    /// <summary>Write a line to the Messages window.</summary>
    public void Say(string line) { if (IsOpen) _say(line); }

    /// <summary>The host closes it when the hook's moment is over.</summary>
    public void Close() => IsOpen = false;
}

/// <summary>
/// The moments a mod may act. Each hook is awaited by the game before it goes on, and a hook that
/// throws (or whose task fails) disables its mod for the session, as any other.
/// </summary>
public sealed class ModActions
{
    /// <summary>
    /// The party has just gone down into the dungeon from town (the city's Dungeon picture or d):
    /// raised once the dungeon is up, before its clocks start. Not when a character saved below
    /// ground is loaded straight into the dungeon, nor after a raise below ground.
    /// </summary>
    public Func<DungeonContext, Task>? EnteredDungeonFromTown { get; set; }

    /// <summary>
    /// The party is taking the stairs up out of level 1 into town: raised after a won round is
    /// finished and before the dungeon closes (so before the town's rest). Not for a rescue, nor
    /// for Exit to Main Menu. If the hook's task does not finish at once and the party has left the
    /// stairs (or the dungeon) by the time it does, the climb does not go on.
    /// </summary>
    /// <remarks>
    /// Neither moment runs while either is still running: the stairs taken again while a hook is
    /// awaited do nothing, and the host itself runs no hooks of a second call made meanwhile.
    /// </remarks>
    public Func<DungeonContext, Task>? LeavingByStairs { get; set; }

    /// <summary>
    /// A round of an engaged fight is about to be played (API version 14): the one-second combat
    /// timer has ticked (timCombat, FRMDUNM.BAS:616; the round, MULTIMOD.BAS:497) and no character
    /// has acted yet. The hook may give orders through <see cref="CombatRoundContext"/> - the same
    /// order keys and Cast a player presses between ticks - and they are what the round plays.
    /// Called once per tick, synchronously and not awaited; never for monsters only waiting, never
    /// for the free round a step gives them, never outside a fight. With no mod hooked nothing is
    /// made and the round is the original's.
    /// </summary>
    public Action<CombatRoundContext>? CombatRound { get; set; }
}

/// <summary>
/// One row of the dungeon's monster list as the player sees it (monShowMonsters, MONSMOD.BAS:4400-4404).
/// Nothing the list does not show: no hit points ("You cannot see a monster's hit points"), no
/// monster behind a class name or "Unseen Monster".
/// </summary>
/// <param name="Slot">The group's slot, 0 to 3: the number key 1 to 4 that aims at it (MAINMOD2.BAS:4131-4136).</param>
/// <param name="Name">The name the list shows, through fn0E23 for the character in command: the monster as the
/// party believes it, its class while unidentified (met in fog), "Unseen Monster(s)" when invisible to them.</param>
/// <param name="Count">How many stand, as the list shows it - only for a monster that comes in groups of two to
/// twelve; null where the list shows no count.</param>
/// <param name="Singular">The same name for one (fn0E23 at a count of one, as the row reads when one is left), for
/// matching a name whatever the count: "Kobold" where <paramref name="Name"/> is "Kobolds".</param>
public sealed record MonsterGroupView(int Slot, string Name, int? Count, string Singular);

/// <summary>A member's standing order as the Party window's Option column has it (<c>gv1E66(i)</c>).</summary>
/// <param name="Kind">None, Fight, Defend, or Spell (<c>.Att</c>, or a spell in <c>.M56C4</c>).</param>
/// <param name="SpellId">The spell, for Spell; 0 otherwise.</param>
/// <param name="ItemSlot">The pack slot an item order casts from (<c>M5766</c> with M56C4 the item's own spell), or null.</param>
/// <param name="OpeningChest">A chest count is running for them (o): the Option column's "Open". f, d, space and
/// a spell buffer call it off, as by hand.</param>
public sealed record CombatOrderView(OrderKind Kind, short SpellId, InventorySlot? ItemSlot, bool OpeningChest = false);

/// <summary>
/// An engaged fight between two ticks, handed to <see cref="ModActions.CombatRound"/>: the party,
/// the monster list as shown (<see cref="Groups"/>), each member's order and aim, and the commands a
/// player gives between ticks - the order keys f, d and space (MAINMOD2.BAS:3594-3604, 4011-4054),
/// a number key's aim (4131-4136), Cast with monsters engaged (cmdCast_Click, FRMCHAR3.BAS:174-184)
/// and Use on an item that holds a spell (sub15CD, MAINMOD2.BAS:6129-6137) - each for a named
/// member, standing for "switch to them and press it". Each runs the game's own path: the checks,
/// refusals and lines are the game's (a refusal in the "you" wording, as by hand), and the order
/// is what the round then plays. No command touches the fight in any other way.
///
/// Every command refuses (false, nothing done) once the hook has returned, once the fight is over
/// or another has begun, and for a member not in the party or dead.
/// </summary>
public sealed class CombatRoundContext
{
    private readonly Func<Character, OrderKind, bool> _order;
    private readonly Func<Character, Spell, bool> _cast;
    private readonly Func<Character, InventorySlot, bool> _use;
    private readonly Func<Character, int, bool> _aim;
    private readonly Func<Character, int?> _aimed;
    private readonly Func<Character, CombatOrderView> _orderOf;

    /// <param name="party">The party, in its order.</param>
    /// <param name="spells">Every spell (MDATA2).</param>
    /// <param name="item">The item table (MDATA3), by id.</param>
    /// <param name="fight">An object standing for the fight: the same for every round of one fight. Never the engine.</param>
    /// <param name="groups">The monster list as shown, the groups still standing.</param>
    /// <param name="orderOf">A member's standing order.</param>
    /// <param name="aimedAt">The slot a member is aiming at, or null.</param>
    /// <param name="order">f, d or space for a member (Fight, Defend, None).</param>
    /// <param name="cast">Cast for a member with monsters engaged: the spell becomes the order.</param>
    /// <param name="useItem">Use for a member on a pack slot whose item holds a spell: it becomes the order.</param>
    /// <param name="aim">A number key for a member: aim at the slot.</param>
    public CombatRoundContext(Party party, IReadOnlyList<Spell> spells, Func<short, Item?> item, object fight,
                              IReadOnlyList<MonsterGroupView> groups, Func<Character, CombatOrderView> orderOf,
                              Func<Character, int?> aimedAt, Func<Character, OrderKind, bool> order,
                              Func<Character, Spell, bool> cast, Func<Character, InventorySlot, bool> useItem,
                              Func<Character, int, bool> aim)
    {
        Party = party; Spells = spells; Item = item; Fight = fight; Groups = groups;
        _orderOf = orderOf; _aimed = aimedAt; _order = order; _cast = cast; _use = useItem; _aim = aim;
    }

    /// <summary>The party, in order: hits, spell points, guilds, packs - what the Party window shows.</summary>
    /// <remarks>
    /// <c>Party.Orders</c> is the game's own: an order's <c>Target</c> there is the engine's index into
    /// every single monster, which the list does not show. Read the aim through <see cref="AimedAt"/>
    /// (a slot, as the list has it) and give orders through the commands here, never through
    /// <c>Orders</c> directly.
    /// </remarks>
    public Party Party { get; }
    public IReadOnlyList<Character> Members => Party.Members;
    /// <summary>Every spell (MDATA2), in table order.</summary>
    public IReadOnlyList<Spell> Spells { get; }
    /// <summary>The item table (MDATA3), by id.</summary>
    public Func<short, Item?> Item { get; }
    /// <summary>
    /// An object standing for the fight: the same every round of one fight, another for the next. It is
    /// only a token - the engine, which holds the monsters' hits, is never handed out.
    /// </summary>
    public object Fight { get; }
    /// <summary>The monster list as shown, in slot order: only groups with someone standing.</summary>
    public IReadOnlyList<MonsterGroupView> Groups { get; }
    /// <summary>False once the hook it was handed to has returned.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>The spells this member knows (<c>Spellbook.Known</c>, the spell page's list).</summary>
    public IReadOnlyList<Spell> KnownSpells(Character c) => Spellbook.Known(c, Spells);
    /// <summary>The member's standing order, as the Option column shows it.</summary>
    public CombatOrderView OrderOf(Character c) => _orderOf(c);
    /// <summary>The slot (0-3) the member is aiming at, or null.</summary>
    public int? AimedAt(Character c) => _aimed(c);

    /// <summary>
    /// f, d or space: the member's order becomes Fight, Defend (refused while held fast, as the d key) or
    /// None; a chest count running for them is called off first, as the keys do.
    /// </summary>
    public bool Order(Character c, OrderKind kind) =>
        IsOpen && kind is OrderKind.None or OrderKind.Fight or OrderKind.Defend && _order(c, kind);
    /// <summary>
    /// Cast with monsters engaged: the spell becomes the member's standing order, recast each round,
    /// after the game's own check (mdrcharCheckCast); a refusal is said and leaves what it leaves, and
    /// gives false. A heal or a cure is refused here as by hand (MAINMOD2.BAS:512-519).
    /// </summary>
    public bool Cast(Character c, Spell spell) => IsOpen && _cast(c, spell);
    /// <summary>
    /// Use on a pack slot: an item known at All that holds a spell becomes the member's order, cast
    /// each round at the item's level, a charge each. Below All refused before it is looked at. As by
    /// hand, sub0BDE then runs (MAINMOD2.BAS:6131-6132): every member with no order drops a chest count
    /// and takes up their Combat Setting.
    /// </summary>
    public bool UseItem(Character c, InventorySlot slot) => IsOpen && _use(c, slot);
    /// <summary>A number key: aim the member at the group in <paramref name="slot"/> (0-3), if anyone stands in it.</summary>
    public bool Aim(Character c, int slot) => IsOpen && _aim(c, slot);

    /// <summary>The host closes it when the hook has returned.</summary>
    public void Close() => IsOpen = false;
}

// ------------------------------------------------------------------------------------- decisions

/// <summary>What ALT-p does in town.</summary>
public enum CityAltPAction
{
    /// <summary>The original: pool the party's on-hand gold to the character in command.</summary>
    PoolOnHand,
    /// <summary>The Bank's "Party Pool &amp; Deposit": everybody's gold, on hand and banked, into the account of the character in command.</summary>
    PoolAndDeposit,
}

/// <summary>ALT-p was pressed in town, in a party of two or more.</summary>
public sealed record CityAltPRequest(Party Party, Character Selected);

/// <summary>A mod's answer to <see cref="CityAltPRequest"/>, with the Messages line to write.</summary>
public sealed record CityAltPAnswer(CityAltPAction Action, string? Message = null)
{
    /// <summary>The original's answer.</summary>
    public static readonly CityAltPAnswer Original = new(CityAltPAction.PoolOnHand);
}

/// <summary>What the town does as the party arrives from the dungeon.</summary>
public enum TownArrivalAction
{
    /// <summary>The original: nothing.</summary>
    Nothing,
    /// <summary>
    /// The Bank's "Party Deposit" (FRMCITYB.BAS:43-47, <c>Party.PartyDeposit</c>): every member's
    /// gold on hand into their own account. Then the Wealthiest record is looked at, as the Bank's
    /// status line does (sub2884).
    /// </summary>
    PartyDeposit,
    /// <summary>
    /// The Bank's "Party Pool" (FRMCITYB.BAS:38-42, <c>Party.PoolToBank</c>): every member's gold on
    /// hand into one member's account, <see cref="TownArrivalAnswer.PoolTo"/> (none named, or one not
    /// in the party: nothing moves). All or nothing on an Overflow, as the Bank's. Then the
    /// Wealthiest record is looked at, as for <see cref="PartyDeposit"/>. API version 12.
    /// </summary>
    PartyPool,
}

/// <summary>A mod's answer to <see cref="TownArrival"/>.</summary>
/// <param name="Action">What to do.</param>
/// <param name="Message">
/// The Messages line, given the total the Party Deposit or Party Pool moved; asked only when it
/// moved something. Null, or a null line: nothing is written.
/// </param>
/// <param name="PoolTo">Whose account a <see cref="TownArrivalAction.PartyPool"/> pools into (API version 12).</param>
public sealed record TownArrivalAnswer(TownArrivalAction Action, Func<VbCurrency, string?>? Message = null, Character? PoolTo = null)
{
    /// <summary>The original's answer: nothing.</summary>
    public static readonly TownArrivalAnswer Original = new(TownArrivalAction.Nothing);
}

/// <summary>
/// Questions the game asks. Each is null until a mod sets it, and a null (or a mod that throws)
/// gets the original's answer.
/// </summary>
public sealed class ModDecisions
{
    /// <summary>
    /// While the Store's Select Alignment frame is up, may another row be taken? The original
    /// says no: the row beeps and the list, its scroll bar, the search and EXIT wait for an
    /// alignment button. True here withdraws the pending choice and takes the new row, and
    /// leaves the list and EXIT usable. Any mod answering true is enough.
    /// </summary>
    public Func<bool>? StoreAllowsRowChangeWhileChoosing { get; set; }

    /// <summary>
    /// What ALT-p does in town. Null, or a null answer, is the original's on-hand pool. The first
    /// mod with a non-null answer decides. The refusal for a party of one comes first and is not
    /// asked about; the dungeon never asks.
    /// </summary>
    public Func<CityAltPRequest, CityAltPAnswer?>? CityAltP { get; set; }

    /// <summary>
    /// How many lines the Messages window keeps. Null, or a null answer, is the original's twenty
    /// (mc04F2). The first mod with a non-null answer decides; an answer below one counts as one.
    /// </summary>
    public Func<int?>? MessagesKept { get; set; }

    /// <summary>
    /// What the town does as the party arrives from the dungeon (<see cref="Mordor.Modding.TownArrival"/>,
    /// with how it came). Null, or a null answer, is the original's: nothing. The first mod with a
    /// non-null answer decides.
    /// </summary>
    public Func<TownArrival, TownArrivalAnswer?>? TownArrival { get; set; }

    /// <summary>
    /// Whether a row of the Store's Buy list is shown. Null, or a null answer, is the original's:
    /// every item with stock is listed. Any mod answering false hides the row. Asked each time the
    /// list is drawn (opening, a buy or sale, a character switch, a mod setting changing).
    /// </summary>
    public Func<StoreRowQuery, bool?>? StoreRowVisible { get; set; }

    /// <summary>
    /// The character in command is short of gold for a town service (their purse, gold on hand and
    /// bank together, is below the cost). Null, or a null answer, is the original's: charPay
    /// refuses and nothing moves. The first mod with a non-null answer decides; a
    /// <see cref="ShortfallAnswer"/> has other members (in the city, alive) hand over exactly the
    /// shortfall from their bank accounts, in the order the answer names them
    /// (<see cref="ShortfallAnswer.From"/>; every other member in party order when it names none;
    /// <c>Party.CoverShortfall</c>), and then the charge goes through as the original's. If they
    /// together cannot cover it nothing moves and the original's refusal follows. Also asked, without moving
    /// anything, where the town tests the purse before charging (Make Level's button and refusal,
    /// the Seer's Locate Character). Not asked in a party of one, nor below ground.
    /// </summary>
    public Func<ShortfallRequest, ShortfallAnswer?>? CoverShortfall { get; set; }

    /// <summary>
    /// The order of the Store's Buy list. Null, or a null answer, is the original's: the shelf's
    /// item order (sub260A, FRMCTYS.BAS:512-555, lists by item index; nothing sorts it). Asked each
    /// time the list is drawn, after <see cref="StoreRowVisible"/> has left out what it hides, with
    /// the rows listed in the shelf's order. The first mod with a non-null answer decides. The answer
    /// must be the same rows (the same <see cref="Item"/> objects), each once, reordered: anything
    /// else is taken as a fault and the mod is disabled for the session.
    /// </summary>
    public Func<StoreOrderQuery, IReadOnlyList<Item>?>? StoreRowOrder { get; set; }

    /// <summary>
    /// Whose bank account gold the town pays a character (<see cref="TownIncome"/>: a sale to the
    /// Store or to the Confinement) goes into instead of the seller's hand: the seller's own, or
    /// another member's. Null, or a null answer, is the original's: into the hand. The first mod
    /// with a non-null answer decides; an answer that is not a member of the party counts as null.
    /// A sum that would overflow that account refuses the sale, as one that would overflow the hand
    /// does. API version 12 (10 and 11 had <c>TownIncomeToBank</c>, true for the seller's account).
    /// </summary>
    public Func<TownIncome, Character?>? TownIncomeBank { get; set; }
}

/// <summary>What paid a character in town (see <see cref="ModDecisions.TownIncomeBank"/>).</summary>
public enum TownIncomeKind
{
    /// <summary>An item sold to the General Store (cmdSell_Click, FRMCTYS.BAS:240).</summary>
    StoreSale,
    /// <summary>A companion sold to the Confinement (cmdSell_Click, FRMCITYC.BAS:284).</summary>
    CompanionSale,
}

/// <summary>Gold the town is about to pay a character.</summary>
/// <param name="Party">The party (API version 12).</param>
/// <param name="Character">Who is paid (the character in command).</param>
/// <param name="Amount">How much.</param>
/// <param name="Kind">For what.</param>
public sealed record TownIncome(Party Party, Character Character, VbCurrency Amount, TownIncomeKind Kind);

/// <summary>The rows of the Store's Buy list about to be drawn, for <see cref="ModDecisions.StoreRowOrder"/>.</summary>
/// <param name="Character">Who is shopping (the character in command).</param>
/// <param name="Rows">The rows listed (hidden ones already left out), in the shelf's order.</param>
/// <param name="Lookup">The item table (MDATA3), by id.</param>
public sealed record StoreOrderQuery(Character Character, IReadOnlyList<Item> Rows, Func<short, Item?> Lookup);

/// <summary>The sort mark a sortable Store header shows (see <see cref="ModUI.StoreHeaderSort"/>).</summary>
public enum StoreSortArrow
{
    /// <summary>Sortable, not the sort now: no mark.</summary>
    None,
    /// <summary>Sorted by it, first to last: ▲.</summary>
    Ascending,
    /// <summary>Sorted by it, last to first: ▼.</summary>
    Descending,
}

/// <summary>A town service costs more than the payer's purse.</summary>
/// <param name="Party">The party.</param>
/// <param name="Payer">The character paying (the one in command).</param>
/// <param name="Cost">What the service costs.</param>
/// <param name="Shortfall">How much the payer's purse (gold on hand and bank) lacks.</param>
public sealed record ShortfallRequest(Party Party, Character Payer, long Cost, VbCurrency Shortfall);

/// <summary>A mod's answer to <see cref="ShortfallRequest"/>: have the party cover it.</summary>
/// <param name="Line">
/// The Messages line for one giver's part (who gave, to whom, how much), asked once per giver
/// once the gold has moved. Null, or a null line: nothing is written for it.
/// </param>
/// <param name="From">
/// Who gives, in this order, each from their bank account (API version 12). Null: every other
/// member in party order. The payer, anyone not in the party, below ground or dead is passed over.
/// </param>
public sealed record ShortfallAnswer(Func<Party.ShortfallGift, Character, string?>? Line = null, IReadOnlyList<Character>? From = null);

/// <summary>A row of the Store's Buy list, about to be drawn.</summary>
/// <param name="Character">Who is shopping (the character in command).</param>
/// <param name="Item">The row's item.</param>
/// <param name="Usable">The Store's own test, which draws the row bold rather than grey: the character can equip it (<c>Character.EquipProblem</c> is null).</param>
public sealed record StoreRowQuery(Character Character, Item Item, bool Usable);

/// <summary>A line of the pack on the Objects &amp; Spells window's Items page, about to be drawn.</summary>
/// <param name="Character">Whose pack.</param>
/// <param name="Item">The slot's item.</param>
/// <param name="Slot">The slot, with how well the item is known (<c>Known</c>).</param>
/// <param name="Index">The slot's place in the pack, 0 being the Hands.</param>
public sealed record PackRowInfo(Character Character, Item Item, InventorySlot Slot, int Index);

/// <summary>What a mod adds after a pack line's text.</summary>
/// <param name="Suffix">Text drawn after the name.</param>
/// <param name="Colour">The suffix's colour (red, green, blue), or null for the row's own.</param>
/// <param name="Tooltip">A tooltip over the suffix, or null.</param>
public sealed record PackRowDecoration(string Suffix, (byte R, byte G, byte B)? Colour = null, string? Tooltip = null)
{
    /// <summary>
    /// A further piece drawn after this one, with its own colour and tooltip (a mark, then a label),
    /// or null. Null by default, so a single piece is drawn as before.
    /// </summary>
    public PackRowDecoration? Then { get; init; }
}

/// <summary>A line of the Messages window, about to be drawn (see <see cref="ModUI.MessageLineColour"/>).</summary>
/// <param name="Text">The line, as the game said it.</param>
/// <param name="Kind">What it is about, as the game said where it made the line (<see cref="MessageKind.None"/> for most).</param>
public sealed record MessageLineInfo(string Text, MessageKind Kind);

/// <summary>A line of the Bank's Deposit/Withdraw Items list, about to be drawn.</summary>
/// <param name="Character">Whose vault (the character in command).</param>
/// <param name="Item">The line's item.</param>
/// <param name="Slot">The vault slot's record, with how well the item is known (<c>Known</c>).</param>
/// <param name="VaultSlot">The slot's place in the vault, 0 to 39 (<c>Character.Bank</c>).</param>
public sealed record BankRowInfo(Character Character, Item Item, InventorySlot Slot, int VaultSlot);

/// <summary>A cell of a Store column, about to be drawn (see <see cref="StoreColumn"/>).</summary>
/// <param name="Character">Who is shopping (the character in command).</param>
/// <param name="Item">The row's item.</param>
/// <param name="Lookup">The item table (MDATA3), by id: for what the character has in the pack.</param>
public sealed record StoreCellQuery(Character Character, Item Item, Func<short, Item?> Lookup);

/// <summary>
/// A cell's text with a tooltip and a colour, for a column slot that takes one (a Store column).
/// A plain string is drawn as the row's own text.
/// </summary>
public sealed record ModCell(string Text, string? Tooltip = null, (byte R, byte G, byte B)? Colour = null);

// ------------------------------------------------------------------------------------ UI slots

/// <summary>A toolbar button, as the help's Toolbar page names it.</summary>
/// <param name="Command">The button's <c>ToolCommand</c> name ("Automap", "ControlPanel", ...).</param>
/// <param name="HelpName">Its name in the help ("AutoMap", "Control Panel", ...).</param>
/// <param name="Key">The key the help says does the same, or null ("F8", "a", ...).</param>
public sealed record ToolbarButton(string Command, string HelpName, string? Key);

/// <summary>What an Objects &amp; Spells command is handed when it is pressed.</summary>
public sealed class ObjectsCommandContext
{
    public ObjectsCommandContext(Character character, bool inDungeon, bool inCombat, Action<string> say, Func<short, Item?> item)
    {
        Character = character; InDungeon = inDungeon; InCombat = inCombat; Say = say; Item = item;
    }
    /// <summary>The item table (MDATA3), by id.</summary>
    public Func<short, Item?> Item { get; }
    /// <summary>The character whose pack the window shows (the one in command).</summary>
    public Character Character { get; }
    public bool InDungeon { get; }
    /// <summary>A fight is on.</summary>
    public bool InCombat { get; }
    /// <summary>Write a line to the Messages window.</summary>
    public Action<string> Say { get; }
}

/// <summary>A button a mod adds to the Items page of Objects &amp; Spells, under Equip, Use, Drop and Info.</summary>
public sealed class ObjectsCommand
{
    public ObjectsCommand(string label, Func<ObjectsCommandContext, bool> run, Func<bool>? isShown = null)
    {
        Label = label; Run = run; IsShown = isShown;
    }
    public string Label { get; }
    /// <summary>
    /// Does the work. Return true when the pack changed: the host then saves the character and
    /// redraws the pack and the buffers.
    /// </summary>
    public Func<ObjectsCommandContext, bool> Run { get; }
    /// <summary>Whether the button is shown now; read again whenever a mod setting changes. Null: always.</summary>
    public Func<bool>? IsShown { get; }
}

/// <summary>What a Bank command is handed when it is pressed.</summary>
public sealed class BankCommandContext
{
    public BankCommandContext(Character character, Action<string> say, Func<short, Item?> item)
    {
        Character = character; Say = say; Item = item;
    }
    /// <summary>The character whose vault the Bank shows (the one in command).</summary>
    public Character Character { get; }
    /// <summary>Write a line to the Messages window.</summary>
    public Action<string> Say { get; }
    /// <summary>The item table (MDATA3), by id.</summary>
    public Func<short, Item?> Item { get; }
}

/// <summary>
/// A button a mod adds to the Bank, on the "Deposit/Withdraw Items" caption's line: the
/// "BankCommands" slot of docs/MODDING.md.
/// </summary>
public sealed class BankCommand
{
    public BankCommand(string label, Func<BankCommandContext, bool> run, Func<bool>? isShown = null)
    {
        Label = label; Run = run; IsShown = isShown;
    }
    public string Label { get; }
    /// <summary>
    /// Does the work. Return true when the vault changed: the host then saves the character and
    /// redraws the Bank (and the Store, when it is open).
    /// </summary>
    public Func<BankCommandContext, bool> Run { get; }
    /// <summary>Whether the button is shown now; read again whenever a mod setting changes. Null: always.</summary>
    public Func<bool>? IsShown { get; }
}

/// <summary>
/// A column a mod adds to the Party Information window, after Option, in town and in the dungeon:
/// the "PartyRowExtras" slot of docs/MODDING.md. The rows are drawn afresh whenever the window is
/// (after a fight, a level, a switch...) and whenever a mod setting changes, so a cell is always
/// read from the character as it is now.
/// </summary>
public sealed class PartyColumn
{
    /// <param name="header">The column's heading, beside Name, Hits, Spells, Status and Option.</param>
    /// <param name="cell">
    /// What a member's row shows: a string (drawn as the other columns' text is) or an Avalonia
    /// <c>Control</c> (typed as object so this assembly needs no Avalonia) - a bar, say. Null: nothing.
    /// </param>
    /// <param name="isShown">Whether the column is there now. Null: always.</param>
    /// <param name="width">Its width in pixels.</param>
    public PartyColumn(string header, Func<Character, object?> cell, Func<bool>? isShown = null, double width = 56)
        : this(header, q => cell(q.Character), isShown, width, 0) { }

    private PartyColumn(string header, Func<PartyCellQuery, object?> query, Func<bool>? isShown, double width, int _)
    {
        Header = header; Query = query; IsShown = isShown; Width = width;
    }

    /// <summary>
    /// A column whose cells are handed the game's tables with the member (<see cref="PartyCellQuery"/>),
    /// for a cell that names a monster or an item - a guild quest's target, say. Otherwise as the
    /// constructor's.
    /// </summary>
    public static PartyColumn WithTables(string header, Func<PartyCellQuery, object?> cell, Func<bool>? isShown = null, double width = 56) =>
        new(header, cell, isShown, width, 0);

    public string Header { get; }
    /// <summary>A member's cell from the member alone: the tables find nothing.</summary>
    public Func<Character, object?> Cell => c => Query(new PartyCellQuery(c, _ => null, _ => null));
    /// <summary>A member's cell, with the game's tables (what the Party Information window asks).</summary>
    public Func<PartyCellQuery, object?> Query { get; }
    public Func<bool>? IsShown { get; }
    public double Width { get; }
}

/// <summary>A member's cell in a mod's Party Information column, about to be drawn (<see cref="PartyColumn.WithTables"/>).</summary>
/// <param name="Character">The member.</param>
/// <param name="Monster">The monster table (MDATA5), by id.</param>
/// <param name="Item">The item table (MDATA3), by id.</param>
public sealed record PartyCellQuery(Character Character, Func<short, Monster?> Monster, Func<short, Item?> Item);

/// <summary>
/// A check box a mod adds to the General Store's page, on the Buy Items heading's line: the
/// "StoreOptions" slot of docs/MODDING.md. It shows <see cref="Get"/> and a click hands the new
/// value to <see cref="Set"/>; it is read again whenever a mod setting changes, so a box bound to a
/// mod setting and the same setting's toggle on the Mods page always agree. The Store's list is
/// drawn again after a mod setting changes (as it is for StoreRowVisible), so a box that switches a
/// Store answer on or off takes effect at once.
/// </summary>
public sealed class StoreOption
{
    /// <param name="label">The text beside the box.</param>
    /// <param name="get">Whether the box is ticked now.</param>
    /// <param name="set">The box was clicked: the new value.</param>
    /// <param name="isShown">Whether the box is there now. Null: always.</param>
    /// <param name="tooltip">Said when the mouse rests on it. Null: nothing.</param>
    public StoreOption(string label, Func<bool> get, Action<bool> set, Func<bool>? isShown = null, string? tooltip = null)
    {
        Label = label; Get = get; Set = set; IsShown = isShown; Tooltip = tooltip;
    }
    public string Label { get; }
    public Func<bool> Get { get; }
    public Action<bool> Set { get; }
    public Func<bool>? IsShown { get; }
    public string? Tooltip { get; }
}

/// <summary>
/// A column a mod adds to the General Store's Buy Items list, between Item and the U G N E counts:
/// the "StoreColumns" slot of docs/MODDING.md. The cells are read as the rows are drawn - when the
/// list is (a character switch, a buy or sale, an equip in town, a mod setting changing) - so a
/// cell is always read from the character as they are now. While any Store column is shown the
/// Store's window is made wider by the columns' widths and a little more for the Item column.
/// </summary>
public sealed class StoreColumn
{
    /// <param name="header">The column's heading.</param>
    /// <param name="cell">A row's cell: a string, a <see cref="ModCell"/>, or null for nothing.</param>
    /// <param name="isShown">Whether the column is there now. Null: always.</param>
    /// <param name="width">Its width in pixels.</param>
    public StoreColumn(string header, Func<StoreCellQuery, object?> cell, Func<bool>? isShown = null, double width = 70)
    {
        Header = header; Cell = cell; IsShown = isShown; Width = width;
    }
    public string Header { get; }
    public Func<StoreCellQuery, object?> Cell { get; }
    public Func<bool>? IsShown { get; }
    public double Width { get; }
}

/// <summary>A window a mod offers. See <see cref="ModUI.AddWindow"/>.</summary>
/// <param name="Title">Its title, and the name of its toolbar button.</param>
/// <param name="Build">Its content, built each time it is opened.</param>
/// <param name="IsShown">Whether it is offered now (a toggle, say). Null: always.</param>
public sealed record ModWindow(string Title, Func<object> Build, Func<bool>? IsShown = null);

/// <summary>Places a mod can add to the game's windows.</summary>
public sealed class ModUI
{
    /// <summary>
    /// The tooltip for a toolbar button, or null for none. The original has none (VB3 buttons have
    /// no tooltip property), so the default is none.
    /// </summary>
    public Func<ToolbarButton, string?>? ToolbarTooltip { get; set; }

    /// <summary>
    /// Something drawn after a pack line's text on the Items page of Objects &amp; Spells, in town
    /// and in the dungeon, or null for nothing (the original draws only the name, bold or italic).
    /// The first mod with a non-null answer decides. Asked for every item line each time the list
    /// is drawn; empty slots are not asked about.
    /// </summary>
    public Func<PackRowInfo, PackRowDecoration?>? PackRowDecoration { get; set; }

    /// <summary>
    /// Something drawn after a line of the Bank's Deposit/Withdraw Items list, or null for nothing
    /// (the original draws only the name). The first mod with a non-null answer decides. Asked for
    /// every line each time the list is drawn; empty vault slots have no line and are not asked about.
    /// </summary>
    public Func<BankRowInfo, PackRowDecoration?>? BankRowDecoration { get; set; }

    /// <summary>
    /// The colour (red, green, blue) of a line of the Messages window, town and dungeon, or null for
    /// the window's own (the original draws every line alike). The first mod with a non-null answer
    /// decides. Asked once per line as it is added; the text is never changed. API version 9.
    /// </summary>
    public Func<MessageLineInfo, (byte R, byte G, byte B)?>? MessageLineColour { get; set; }

    private readonly List<BankCommand> _bank = new();

    /// <summary>Add a button to the Bank, on the "Deposit/Withdraw Items" caption's line (the BankCommands slot).</summary>
    public void AddBankCommand(BankCommand command) => _bank.Add(command);

    public IReadOnlyList<BankCommand> BankCommands => _bank;

    /// <summary>
    /// Whether a header of the Store's Buy list can be clicked to sort, and the mark it shows. Asked
    /// with the header's text - "Item" for the Store's own Item column, a Store column's own header
    /// for a mod's (<see cref="StoreColumn"/>, only while it is shown) - whenever the list is drawn.
    /// Null, or a null answer, is the original's: a plain header that does nothing when clicked. The
    /// first mod with a non-null answer owns the header: a click on it goes to that mod's
    /// <see cref="StoreHeaderClicked"/>. The order itself is <see cref="ModDecisions.StoreRowOrder"/>.
    /// </summary>
    public Func<string, StoreSortArrow?>? StoreHeaderSort { get; set; }

    /// <summary>
    /// A header this mod owns (<see cref="StoreHeaderSort"/>) was clicked, with its text. Not raised
    /// while the Store's alignment choice holds the list (the original's lock, FRMCTYS.BAS:981).
    /// The list is drawn again after it.
    /// </summary>
    public Action<string>? StoreHeaderClicked { get; set; }

    private readonly List<ObjectsCommand> _objects = new();
    private readonly List<ModWindow> _windows = new();
    private readonly List<PartyColumn> _partyColumns = new();

    /// <summary>Add a button to the Items page of Objects &amp; Spells, in town and in the dungeon.</summary>
    public void AddObjectsCommand(ObjectsCommand command) => _objects.Add(command);

    /// <summary>Add a column to the Party Information window's rows (the PartyRowExtras slot).</summary>
    public void AddPartyColumn(PartyColumn column) => _partyColumns.Add(column);

    public IReadOnlyList<PartyColumn> PartyColumns => _partyColumns;

    private readonly List<StoreOption> _storeOptions = new();

    /// <summary>Add a check box to the General Store's page (the StoreOptions slot).</summary>
    public void AddStoreOption(StoreOption option) => _storeOptions.Add(option);

    public IReadOnlyList<StoreOption> StoreOptions => _storeOptions;

    private readonly List<StoreColumn> _storeColumns = new();

    /// <summary>Add a column to the General Store's Buy Items list (the StoreColumns slot).</summary>
    public void AddStoreColumn(StoreColumn column) => _storeColumns.Add(column);

    public IReadOnlyList<StoreColumn> StoreColumns => _storeColumns;

    /// <summary>
    /// Offer a window. <paramref name="build"/> returns its content, an Avalonia <c>Control</c>
    /// (typed as object so this assembly needs no Avalonia), built afresh each time it opens. The
    /// host puts a button with the title at the end of the toolbar's icons, which opens and closes
    /// it, and an Open button on the Mods page (the main menu's and the Control Panel's Mods button). It is a window of its own, above
    /// the game's frame; whether it was open, and where, is kept in settings.ini and it opens again
    /// at the next start. While <paramref name="isShown"/> says false the button is gone and the
    /// window closed (and opened again when it says true, if it was open).
    /// </summary>
    public void AddWindow(string title, Func<object> build, Func<bool>? isShown = null) =>
        _windows.Add(new ModWindow(title, build, isShown));

    public IReadOnlyList<ObjectsCommand> ObjectsCommands => _objects;
    public IReadOnlyList<ModWindow> Windows => _windows;

    // ------------------------------------------------------------------ the Automap (API version 13)

    /// <summary>
    /// The items a right-click on a square of the Automap's map offers (picMaze; not the grey round it,
    /// the numbers or the level row), or null for none. While any running mod answers with at least
    /// one item, the right-click opens a small menu whose first item is the original's own right-click,
    /// <c>picMaze_MouseDown</c>'s re-plot ("Redraw/Refresh Level", FRMAUTOM.BAS:600-602), and then each
    /// mod's items in load order; with none it re-plots at once, as the original. Asked at each
    /// right-click, with the square under the pointer. An item's <see cref="AutomapMenuItem.Run"/> is
    /// called when it is chosen, and the marks are asked for again after it.
    /// </summary>
    public Func<AutomapView, AutomapSquare, IReadOnlyList<AutomapMenuItem>?>? AutomapMenu { get; set; }

    /// <summary>
    /// What to draw over the level the Automap shows (<see cref="AutomapView.ShownLevel"/>): square
    /// tints, frames and arrows (<see cref="AutomapMark"/>), drawn by the game on the map's own squares
    /// at the size the map is drawn, on whole device pixels. Asked whenever the level is drawn, paged,
    /// hovered, clicked, after a menu item and after <see cref="AutomapView.Refresh"/>; null or empty:
    /// nothing. The marks of every running mod are drawn, in load order.
    /// </summary>
    public Func<AutomapView, IReadOnlyList<AutomapMark>?>? AutomapMarks { get; set; }

    /// <summary>
    /// The pointer came onto another square of the map, or off the map (null). Told once per change of
    /// square, then the marks are asked for again.
    /// </summary>
    public Action<AutomapView, AutomapSquare?>? AutomapHover { get; set; }

    /// <summary>
    /// A left click on a square of the map (not while a <see cref="AutomapView.PickSquare"/> is waiting,
    /// which takes the click). The original does nothing with a single click on picMaze, so this adds to
    /// nothing; a double click still resizes the window as the original's does. Return true when the mod
    /// did something. Every running mod is told; the marks are asked for again after.
    /// </summary>
    public Func<AutomapView, AutomapSquare, bool>? AutomapClick { get; set; }
}

// ---------------------------------------------------------------------------------------- automap

/// <summary>
/// A square of the Automap: the dungeon level and its place on it, counted from nought - X east and Y
/// north, as the port keeps squares (the Automap's own readout, "You are at 5,7,1", adds one to each).
/// </summary>
public sealed record AutomapSquare(int Level, int X, int Y);

/// <summary>What an <see cref="AutomapMark"/> draws.</summary>
public enum AutomapMarkKind
{
    /// <summary>The square's inside, inside its edge lines, filled with the colour at about 60% (the map shows through).</summary>
    Tint,
    /// <summary>A ring of the colour just inside the square's edge lines, with a dark line inside it so it reads on grey and on black.</summary>
    Frame,
    /// <summary>An arrow from the middle of one square to the middle of another on the same level, outlined dark.</summary>
    Arrow,
}

/// <summary>
/// Something a mod draws over the Automap's level (<see cref="ModUI.AutomapMarks"/>). The game draws it
/// on the map's own squares, at the size and place the map is drawn now, its edges on whole device
/// pixels: the square edges the map's own bitmap has (<c>AutomapLayout.DeviceEdges</c>). The square's
/// edge lines - its walls and doors - are never covered by a tint or a frame.
/// </summary>
/// <param name="Kind">What is drawn.</param>
/// <param name="X">The square (an arrow's start), from nought, east.</param>
/// <param name="Y">The square (an arrow's start), from nought, north.</param>
/// <param name="Colour">Its colour (red, green, blue).</param>
/// <param name="ToX">An arrow's end, east.</param>
/// <param name="ToY">An arrow's end, north.</param>
/// <param name="Bold">A frame half as thick again.</param>
public sealed record AutomapMark(AutomapMarkKind Kind, int X, int Y, (byte R, byte G, byte B) Colour, int ToX = 0, int ToY = 0, bool Bold = false)
{
    /// <summary>The square's inside filled with the colour, the map showing through.</summary>
    public static AutomapMark Tint(int x, int y, (byte R, byte G, byte B) colour) => new(AutomapMarkKind.Tint, x, y, colour);
    /// <summary>A ring of the colour inside the square's edge lines.</summary>
    public static AutomapMark Frame(int x, int y, (byte R, byte G, byte B) colour, bool bold = false) => new(AutomapMarkKind.Frame, x, y, colour, Bold: bold);
    /// <summary>An arrow from one square's middle to another's, on the level shown.</summary>
    public static AutomapMark Arrow(int fromX, int fromY, int toX, int toY, (byte R, byte G, byte B) colour) =>
        new(AutomapMarkKind.Arrow, fromX, fromY, colour, toX, toY);
}

/// <summary>An item of the Automap's right-click menu (<see cref="ModUI.AutomapMenu"/>).</summary>
/// <param name="Label">What it says.</param>
/// <param name="Run">Chosen: what it does. Guarded as every call into a mod.</param>
/// <param name="Swatch">A small square of this colour before the label, or null.</param>
/// <param name="IsEnabled">False: shown greyed, and cannot be chosen.</param>
public sealed record AutomapMenuItem(string Label, Action Run, (byte R, byte G, byte B)? Swatch = null, bool IsEnabled = true)
{
    /// <summary>A line between items, not an item.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>A line between two groups of items.</summary>
    public static AutomapMenuItem Separator { get; } = new("-", () => { }) { IsSeparator = true };
}

/// <summary>
/// The Automap open now, as a mod's Automap hooks see it (API version 13): the level shown, a way to
/// show another, to ask the player to pick a square, and to have the marks drawn again. Valid while
/// that map is open; the game makes it, a probe may make one of its own.
/// </summary>
public sealed class AutomapView
{
    private readonly Func<int> _shown, _deepest;
    private readonly Func<int, (int Width, int Height)> _size;
    private readonly Action<int> _show;
    private readonly Action _refresh, _cancel;
    private readonly Action<string, Action<AutomapSquare?>> _pick;
    private readonly Func<bool> _picking;

    /// <param name="shownLevel">The level the map shows now (hsbLevel.Value).</param>
    /// <param name="deepestLevel">The deepest it can be paged to (hsbLevel.Max).</param>
    /// <param name="levelSize">A level's width and height in squares.</param>
    /// <param name="showLevel">Page the map to a level, as its level bar does (clamped to 1 and the deepest).</param>
    /// <param name="refresh">Ask the mods for their marks again and draw them.</param>
    /// <param name="pick">Put up a prompt and wait for the player to click a square (see <see cref="PickSquare"/>).</param>
    /// <param name="cancelPick">Give up a pick that is waiting (its callback gets null).</param>
    /// <param name="picking">Whether a pick is waiting.</param>
    public AutomapView(Func<int> shownLevel, Func<int> deepestLevel, Func<int, (int Width, int Height)> levelSize,
                       Action<int> showLevel, Action refresh, Action<string, Action<AutomapSquare?>> pick,
                       Action cancelPick, Func<bool> picking)
    {
        _shown = shownLevel; _deepest = deepestLevel; _size = levelSize; _show = showLevel;
        _refresh = refresh; _pick = pick; _cancel = cancelPick; _picking = picking;
    }

    /// <summary>The level shown now.</summary>
    public int ShownLevel => _shown();
    /// <summary>The deepest level the map can be paged to: the deepest anyone has reached.</summary>
    public int DeepestLevel => _deepest();
    /// <summary>A level's size in squares (0 by 0 for a level the game does not have).</summary>
    public (int Width, int Height) LevelSize(int level) => _size(level);
    /// <summary>Page the map to <paramref name="level"/>, as its level bar does (held to 1 and <see cref="DeepestLevel"/>).</summary>
    public void ShowLevel(int level) => _show(level);
    /// <summary>Ask every mod for its marks again and draw them (after the mod's own data changed).</summary>
    public void Refresh() => _refresh();
    /// <summary>Whether a <see cref="PickSquare"/> is waiting.</summary>
    public bool Picking => _picking();

    /// <summary>
    /// Ask the player to pick a square: <paramref name="prompt"/> takes the place of the Automap's
    /// status line (lblInfo) until it is answered; the level bar and &lt; &gt; still page the map; the
    /// next left click on a square of the map answers <paramref name="done"/> with that square, and
    /// Escape or a right-click answers null. One pick at a time: a second one first answers the first
    /// with null. <paramref name="done"/> is guarded as every call into a mod, and is not called for a
    /// mod no longer running.
    /// </summary>
    public void PickSquare(string prompt, Action<AutomapSquare?> done) => _pick(prompt, done);

    /// <summary>Give up a pick that is waiting: its callback is answered with null.</summary>
    public void CancelPick() => _cancel();

    /// <summary>The same map, with a pick's callback wrapped (the host's guard for one mod).</summary>
    internal AutomapView WithPickGuard(Func<Action<AutomapSquare?>, Action<AutomapSquare?>> wrap) =>
        new(_shown, _deepest, _size, _show, _refresh, (p, d) => _pick(p, wrap(d)), _cancel, _picking);
}

// ------------------------------------------------------------------------------------------- log

/// <summary>Lines into the port's log, each marked with the mod's id.</summary>
public sealed class ModLog
{
    private readonly string _id;
    private readonly Action<string> _sink;
    private readonly Action<string> _errors;
    /// <param name="sink">The mods log, where every line goes.</param>
    /// <param name="errors">Where an Error goes as well: the game's crash.log.</param>
    internal ModLog(string id, Action<string> sink, Action<string>? errors = null)
    {
        _id = id; _sink = sink;
        // an Error reaches the sink through this, so it is written there once
        _errors = errors ?? sink;
    }
    public void Info(string line) => _sink($"mod {_id}: {line}");
    public void Error(string line, Exception? ex = null) =>
        _errors($"mod {_id}: ERROR {line}" + (ex == null ? "" : Environment.NewLine + ex));
}
