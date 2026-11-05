using System;

namespace FPSPrototype.Shared.Map
{
    [Serializable]
    public struct Float3
    {
        public float x, y, z;
        public Float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Float3 operator *(Float3 a, float s) => new Float3(a.x * s, a.y * s, a.z * s);
        public static float Dot(Float3 a, Float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public bool IsFinite => Finite(x) && Finite(y) && Finite(z);
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public override string ToString() => $"({x}, {y}, {z})";
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public int schemaVersion;
        public string id;
        public string displayName;
        public Float3 arenaSize;
        public MapMaterial[] materials;
        public MapObject[] objects;
        public SpawnDefinition[] spawns;
    }

    [Serializable]
    public sealed class MapMaterial
    {
        public string id;
        public Float3 color;
        public float metallic;
        public float smoothness;
        public float emission;
    }

    [Serializable]
    public sealed class MapObject
    {
        public string id;
        public string group;
        public string shape;
        public string colliderType;
        public Float3 position;
        public Float3 rotation;
        // Full dimensions, shared by visual geometry and collision.
        public Float3 size;
        public string material;
    }

    [Serializable]
    public sealed class SpawnDefinition
    {
        public string id;
        public int team;
        public Float3 position;
        public float yaw;
    }
}
