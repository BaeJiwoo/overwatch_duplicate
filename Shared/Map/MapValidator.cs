using System;
using System.Collections.Generic;
using FPSPrototype.Shared.Game;
using FPSPrototype.Shared.Network;

namespace FPSPrototype.Shared.Map
{
    public static class MapValidator
    {
        public static void Validate(MapDefinition map)
        {
            if (map == null) Fail("Map is null.");
            if (map.schemaVersion != ProtocolVersion.MapSchema) Fail("Unsupported map schema.");
            if (!Id(map.id) || string.IsNullOrWhiteSpace(map.displayName)) Fail("Missing map identity.");
            if (!Positive(map.arenaSize) || map.arenaSize.x > 1000 || map.arenaSize.y > 1000 || map.arenaSize.z > 1000) Fail("Invalid arena size.");
            if (map.materials == null || map.materials.Length == 0 || map.materials.Length > 64) Fail("Invalid materials.");
            if (map.objects == null || map.objects.Length == 0 || map.objects.Length > 4096) Fail("Invalid object count.");
            if (map.spawns == null || map.spawns.Length < 2 || map.spawns.Length > 64) Fail("Invalid spawn count.");
            var materials = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in map.materials)
            {
                if (m == null || !Id(m.id) || !materials.Add(m.id)) Fail("Invalid/duplicate material ID.");
                if (!m.color.IsFinite || !Unit(m.color.x) || !Unit(m.color.y) || !Unit(m.color.z) || !Unit(m.metallic) || !Unit(m.smoothness) || !Float3.Finite(m.emission) || m.emission < 0 || m.emission > 8) Fail("Invalid material values.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in map.objects)
            {
                if (o == null || !Id(o.id) || !ids.Add(o.id) || !Id(o.group)) Fail("Invalid/duplicate object ID or group.");
                if (!materials.Contains(o.material ?? "")) Fail("Unknown material: " + o.id);
                if (!o.position.IsFinite || !o.rotation.IsFinite || !Positive(o.size)) Fail("Invalid geometry: " + o.id);
                if (o.rotation.x != 0 || o.rotation.z != 0 || (o.rotation.y != 0 && o.rotation.y != 90 && o.rotation.y != 180 && o.rotation.y != 270)) Fail("Unsupported rotation: " + o.id);
                if (o.shape != "Box" && o.shape != "Ramp") Fail("Unsupported shape: " + o.id);
                if (o.colliderType != "Aabb" && o.colliderType != "Ramp" && o.colliderType != "None") Fail("Unsupported collider: " + o.id);
                if (o.shape == "Box" && (o.rotation.y != 0 || o.colliderType == "Ramp")) Fail("Box must be axis-aligned: " + o.id);
                if (o.shape == "Ramp" && o.colliderType != "Ramp") Fail("Ramp must use shared wedge collision: " + o.id);
                if (o.shape == "Ramp" && o.size.y / o.size.z > 0.75f) Fail("Ramp too steep: " + o.id);
                var b = MapGeometry.Bounds(o);
                if (b.Min.x < -map.arenaSize.x / 2 || b.Max.x > map.arenaSize.x / 2 || b.Min.z < -map.arenaSize.z / 2 || b.Max.z > map.arenaSize.z / 2 || b.Min.y < -2 || b.Max.y > map.arenaSize.y) Fail("Object outside arena: " + o.id);
            }
            var teams = new bool[2];
            foreach (var s in map.spawns)
            {
                if (s == null || !Id(s.id) || !ids.Add(s.id)) Fail("Invalid/duplicate spawn ID.");
                if (s.team < 0 || s.team > 1 || !s.position.IsFinite || !Float3.Finite(s.yaw) || s.yaw < 0 || s.yaw >= 360) Fail("Invalid spawn: " + s.id);
                teams[s.team] = true;
                if (Math.Abs(s.position.x) + GameRules.PlayerWidth / 2 >= map.arenaSize.x / 2 || Math.Abs(s.position.z) + GameRules.PlayerWidth / 2 >= map.arenaSize.z / 2) Fail("Spawn outside arena: " + s.id);
                var half = GameRules.PlayerWidth / 2;
                var body = new Bounds3(s.position - new Float3(half, 0, half), s.position + new Float3(half, GameRules.PlayerHeight, half));
                bool support = false;
                foreach (var o in map.objects)
                {
                    if (o.colliderType == "None") continue;
                    // Conservative ramp bounding box is deliberate: spawn volumes must be wholly clear.
                    if (body.Overlaps(MapGeometry.Bounds(o))) Fail("Spawn intersects solid: " + s.id + "/" + o.id);
                    if (MapGeometry.SurfaceHeight(o, s.position.x, s.position.z, out var h) && s.position.y - h >= 0 && s.position.y - h <= 0.1f) support = true;
                }
                if (!support) Fail("Spawn lacks ground support: " + s.id);
            }
            if (!teams[0] || !teams[1]) Fail("Both teams require spawns.");
        }

        private static bool Positive(Float3 v) => v.IsFinite && v.x > 0 && v.y > 0 && v.z > 0;
        private static bool Unit(float v) => Float3.Finite(v) && v >= 0 && v <= 1;
        private static bool Id(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 80) return false;
            foreach (var c in value) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }
        private static void Fail(string message) => throw new ArgumentException(message, "map");
    }
}
