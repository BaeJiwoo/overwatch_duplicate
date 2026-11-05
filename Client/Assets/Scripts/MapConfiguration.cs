using UnityEngine;

namespace FPSPrototype.Client
{
    public sealed class MapConfiguration : ScriptableObject
    {
        public TextAsset definition;
        public string mapId;
        public string sha256;
        public int schemaVersion;
        public int objectCount;
        public int spawnCount;
    }
}
