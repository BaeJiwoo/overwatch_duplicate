using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FPSPrototype.Shared.Map;

namespace FPSPrototype.Server
{
    public static class MapLoader
    {
        public static MapDefinition Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("Map exceeds 2 MiB limit.");
            var map = JsonSerializer.Deserialize<MapDefinition>(bytes, new JsonSerializerOptions {
                IncludeFields = true, PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            });
            MapValidator.Validate(map);
            return map;
        }
        public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
