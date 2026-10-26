using Mordor.Core;
public static class Pics { public static void Run(string dir) {
  var m = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir,"MDATA5.MDR"));
  System.Console.WriteLine($"PicId min={m.Min(x=>x.PicId)} max={m.Max(x=>x.PicId)} distinct={m.Select(x=>x.PicId).Distinct().Count()}");
  foreach (var x in m.Take(6)) System.Console.WriteLine($"  {x.Name}: pic {x.PicId}");
} }
