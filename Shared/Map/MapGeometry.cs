using System;

namespace FPSPrototype.Shared.Map
{
    public readonly struct Bounds3
    {
        public readonly Float3 Min, Max;
        public Bounds3(Float3 min, Float3 max) { Min = min; Max = max; }
        public bool Contains(Float3 p) => p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y && p.z >= Min.z && p.z <= Max.z;
        public bool Overlaps(Bounds3 b) => Min.x < b.Max.x && Max.x > b.Min.x && Min.y < b.Max.y && Max.y > b.Min.y && Min.z < b.Max.z && Max.z > b.Min.z;
    }

    // Shared static geometry, not a movement solver. Sweeps/steps/sliding arrive in Phase 4.
    public static class MapGeometry
    {
        public static Bounds3 Bounds(MapObject o)
        {
            var size = o.size;
            if (o.rotation.y == 90 || o.rotation.y == 270) size = new Float3(size.z, size.y, size.x);
            return new Bounds3(o.position - size * 0.5f, o.position + size * 0.5f);
        }

        public static Float3 Rotate(Float3 p, float yaw)
        {
            if (yaw == 0) return p;
            if (yaw == 90) return new Float3(p.z, p.y, -p.x);
            if (yaw == 180) return new Float3(-p.x, p.y, -p.z);
            if (yaw == 270) return new Float3(-p.z, p.y, p.x);
            throw new ArgumentOutOfRangeException(nameof(yaw), "Only cardinal yaw is supported.");
        }

        public static Float3 ToLocal(MapObject o, Float3 p) => Rotate(p - o.position, (360 - o.rotation.y) % 360);

        public static bool SurfaceHeight(MapObject o, float x, float z, out float height)
        {
            height = 0;
            if (o.colliderType == "None") return false;
            var p = ToLocal(o, new Float3(x, o.position.y, z));
            if (Math.Abs(p.x) > o.size.x * 0.5f || Math.Abs(p.z) > o.size.z * 0.5f) return false;
            height = o.position.y + (o.colliderType == "Ramp" ? p.z * o.size.y / o.size.z : o.size.y * 0.5f);
            return true;
        }

        public static bool Contains(MapObject o, Float3 point)
        {
            if (o.colliderType == "None") return false;
            var p = ToLocal(o, point);
            var half = o.size * 0.5f;
            if (Math.Abs(p.x) > half.x || Math.Abs(p.z) > half.z || p.y < -half.y || p.y > half.y) return false;
            return o.colliderType != "Ramp" || p.y <= p.z * o.size.y / o.size.z;
        }

        // Ray direction can have any nonzero length; returned distance is in meters.
        public static bool Raycast(MapObject o, Float3 origin, Float3 direction, float maxDistance, out float distance)
        {
            distance = 0;
            if (o.colliderType == "None" || !origin.IsFinite || !direction.IsFinite || !Float3.Finite(maxDistance) || maxDistance < 0) return false;
            var length = (float)Math.Sqrt(Float3.Dot(direction, direction));
            if (!Float3.Finite(length) || length < 0.000001f) return false;
            var p = ToLocal(o, origin);
            var d = Rotate(direction * (1 / length), (360 - o.rotation.y) % 360);
            float enter = 0, exit = maxDistance;
            var h = o.size * 0.5f;
            if (!Clip(new Float3(1, 0, 0), h.x, p, d, ref enter, ref exit) ||
                !Clip(new Float3(-1, 0, 0), h.x, p, d, ref enter, ref exit) ||
                !Clip(new Float3(0, 1, 0), h.y, p, d, ref enter, ref exit) ||
                !Clip(new Float3(0, -1, 0), h.y, p, d, ref enter, ref exit) ||
                !Clip(new Float3(0, 0, 1), h.z, p, d, ref enter, ref exit) ||
                !Clip(new Float3(0, 0, -1), h.z, p, d, ref enter, ref exit)) return false;
            if (o.colliderType == "Ramp" && !Clip(new Float3(0, 1, -o.size.y / o.size.z), 0, p, d, ref enter, ref exit)) return false;
            distance = enter;
            return true;
        }

        private static bool Clip(Float3 normal, float limit, Float3 p, Float3 d, ref float enter, ref float exit)
        {
            var numerator = limit - Float3.Dot(normal, p);
            var denominator = Float3.Dot(normal, d);
            if (Math.Abs(denominator) < 0.000001f) return numerator >= 0;
            var t = numerator / denominator;
            if (denominator < 0) enter = Math.Max(enter, t); else exit = Math.Min(exit, t);
            return enter <= exit;
        }

        // Unit wedge: low edge at z=-0.5, high edge at z=+0.5; outward winding.
        public static Float3[] RampVertices() => new[] {
            new Float3(-.5f,-.5f,-.5f), new Float3(.5f,-.5f,-.5f),
            new Float3(-.5f,-.5f,.5f), new Float3(.5f,-.5f,.5f),
            new Float3(-.5f,.5f,.5f), new Float3(.5f,.5f,.5f) };

        public static int[] RampTriangles() => new[] { 0,1,2, 1,3,2, 0,4,1, 1,4,5, 2,3,4, 3,5,4, 0,2,4, 1,5,3 };
    }
}
