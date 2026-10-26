using Mordor.Core;

/// <summary>
/// The store's fixed-price goods, by the original's own test: modItemCost returns the list price
/// for any item whose Floor is 0 (FRMCTYS.BAS:594-596), and selling one unaligned does not shelve
/// it (FRMCTYS.BAS:264). The help calls these "items that can only be bought in the store (and not
/// found in the dungeon)". Lists them with their alignment flags, and checks the port's pricing
/// and shelving agree with the Floor = 0 rule for every one.
/// </summary>
public static class StoreProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
  var floor0 = items.Where(i => i.Floor == 0).ToList();
  Console.WriteLine($"Floor-0 items (fixed price, FRMCTYS.BAS:594): {floor0.Count}");
  foreach (var i in floor0.OrderBy(i => i.Price))
    Console.WriteLine($"  [{i.Id,3}] {i.Name,-24} price {i.Price,12:N0} type {i.Type,2} flags 0x{i.AlignmentFlags:X} -> {string.Join("/", ItemAlignments.Possible(i))}");

  var alignable = floor0.Where(i => ItemAlignments.Possible(i).Any(a => a != ItemAlignment.Unaligned)).ToList();
  Console.WriteLine($"\nFloor-0 items that can carry a G/N/E alignment: {alignable.Count}");
  foreach (var i in alignable) Console.WriteLine($"  {i.Name}: {string.Join("/", ItemAlignments.Possible(i))}");

  // the port's two floor tests, read back against the rule
  var store = new StoreStock();
  int priceOff = 0, shelfOff = 0;
  foreach (var i in items)
  {
    bool fixedPrice = store.PriceOf(i, ItemAlignment.Unaligned) == i.Price;
    if (i.Floor == 0 && !fixedPrice) priceOff++;
    if (StoreStock.ShelvedBySelling(i, ItemAlignment.Unaligned) == (i.Floor == 0)) shelfOff++;
  }
  Console.WriteLine($"\nfloor-0 items not at list price: {priceOff}");
  Console.WriteLine($"items whose unaligned sale shelving disagrees with Floor = 0: {shelfOff}");
} }
