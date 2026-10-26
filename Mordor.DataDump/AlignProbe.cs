using Mordor.Core;
public static class AlignProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
  Console.WriteLine("AlignmentFlags values across all items:");
  foreach (var g in items.GroupBy(i => i.AlignmentFlags).OrderBy(g => g.Key))
    Console.WriteLine($"  0x{g.Key:X}  n={g.Count(),3}   floor0={g.Count(i => i.Floor == 0),3}  e.g. {string.Join(", ", g.Take(4).Select(i => i.Name))}");
  Console.WriteLine("\nFloor-0 (permanent store stock) items and their flags:");
  foreach (var i in items.Where(i => i.Floor == 0 && i.Price > 0).Take(12))
    Console.WriteLine($"  {i.Name,-24} align 0x{i.AlignmentFlags:X}  price {i.Price,12:N0}");
  // MAINMOD.BAS:894 tests Align > 1, not "more than one bit": these are the items where the two differ
  Console.WriteLine("\nAlign > 1 with a single bit (id, floor, price, cursed, starting stock?):");
  foreach (var i in items.Where(i => i.AlignmentFlags > 1 && (i.AlignmentFlags & (i.AlignmentFlags - 1)) == 0))
    Console.WriteLine($"  {i.Id,4} {i.Name,-24} align 0x{i.AlignmentFlags:X}  floor {i.Floor}  price {i.Price,12:N0}  cursed {i.Cursed}"
      + $"  {(i.Id is >= 356 and <= 361 ? "artifact" : i.Floor > 1 || i.Price > 200000 || i.Price == 0 || i.Cursed != 0 ? "not stocked" : "STOCKED")}");
  Console.WriteLine("\nSome dungeon items:");
  foreach (var i in items.Where(i => i.Floor >= 1).Take(10))
    Console.WriteLine($"  {i.Name,-24} align 0x{i.AlignmentFlags:X}  floor {i.Floor}");
} }
