using Mordor.Core;

/// <summary>
/// Checks modItemCost (FRMCTYS.BAS) against what the real game prints in its store.
/// cost = ((1.5 * price) / (Log(stock - 1 + 1) + 1.15)) / 1 - drift * (0.05 * price)
/// where the drift term is zero for an item with no charges.
/// </summary>
public static class PriceProbe { public static void Run(string dir, string want) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"))
                       .Where(i => i.Id != 0 && i.Name.Contains(want, StringComparison.OrdinalIgnoreCase));
  foreach (var i in items)
  {
    Console.WriteLine($"{i.Name,-24} price {i.Price,12:N0}  charges {i.Charges}  floor {i.Floor}");
    for (int stock = 1; stock <= 3; stock++)
    {
      double cost = ((1.5 * i.Price) / (Math.Log(stock - 1 + 1) + 1.15)) / 1.0;
      Console.WriteLine($"    stock {stock}: modItemCost predicts {cost,14:N0}");
    }
  }
} }
