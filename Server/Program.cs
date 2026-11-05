using System;
using System.IO;

namespace FPSPrototype.Server
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1 || args[0] != "--validate-map" || args.Length > 2)
            {
                Console.WriteLine("Phase 1 map validation tool. UDP server runtime arrives in Phase 2.");
                Console.WriteLine("Usage: FPSPrototype.Server --validate-map [path]");
                return args.Length == 0 ? 0 : 2;
            }
            try
            {
                var path = args.Length == 2 ? args[1] : Path.Combine(AppContext.BaseDirectory, "Maps", "training-arena.json");
                var map = MapLoader.Load(path);
                Console.WriteLine($"VALID {map.displayName}: {map.objects.Length} objects, {map.spawns.Length} spawns, {map.arenaSize.x}m x {map.arenaSize.z}m");
                Console.WriteLine("SHA256 " + MapLoader.Hash(path));
                Console.WriteLine("Unity runtime is not loaded. Networking/movement/combat are not implemented in Phase 1.");
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("Map validation failed: " + e.Message); return 1; }
        }
    }
}
