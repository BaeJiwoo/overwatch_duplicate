using System;
using System.IO;
using System.Linq;
using FPSPrototype.Server;
using FPSPrototype.Shared.Map;
using FPSPrototype.Shared.Game;

internal static class Program
{
    private static readonly string PathToMap = Path.Combine(AppContext.BaseDirectory, "Maps", "training-arena.json");
    private static int passed, failed;
    private static MapDefinition Map() => MapLoader.Load(PathToMap);
    private static void Main()
    {
        Test("Canonical map loads in standalone .NET", () => {
            var m = Map(); Equal(80, m.arenaSize.x); Equal(80, m.arenaSize.z);
            Check(m.spawns.Count(s => s.team == 0) == 4 && m.spawns.Count(s => s.team == 1) == 4);
            Check(m.objects.Count(o => o.colliderType == "Ramp") == 2);
            Check(m.objects.Count(o => o.id.Contains("-step-") && o.colliderType == "Aabb") == 32);
        });
        Test("Map identity detects a byte-level content change", () => {
            var p=Path.Combine(Path.GetTempPath(),"fps-hash-"+Guid.NewGuid()+".json");
            try {File.Copy(PathToMap,p);Check(MapLoader.Hash(PathToMap)==MapLoader.Hash(p));
                File.AppendAllText(p,"\n");Check(MapLoader.Hash(PathToMap)!=MapLoader.Hash(p));
            } finally {File.Delete(p);}
        });
        Test("Shared has no engine dependencies", () => Check(!typeof(MapDefinition).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Unity"))));
        Test("Gameplay policy explicitly noncolliding", () => Check(!GameRules.PlayerMovementCollision && GameRules.ServerTickRate == 30));
        Reject("Duplicate object", m => m.objects[1].id = m.objects[0].id);
        Reject("Duplicate spawn", m => m.spawns[1].id = m.spawns[0].id);
        Reject("Unknown schema", m => m.schemaVersion++);
        Reject("Null objects", m => m.objects = null);
        Reject("NaN position", m => m.objects[1].position.x = float.NaN);
        Reject("Infinity dimensions", m => m.objects[1].size.z = float.PositiveInfinity);
        Reject("Negative dimensions", m => m.objects[1].size.x = -1);
        Reject("Rotated AABB", m => m.objects[1].rotation.y = 90);
        Reject("Noncardinal ramp", m => m.objects.First(o => o.shape == "Ramp").rotation.y = 45);
        Reject("Unsupported MeshCollider", m => m.objects[1].colliderType = "Mesh");
        Reject("Unknown material", m => m.objects[1].material = "missing");
        Reject("Outside map", m => m.objects[1].position.x = 100);
        Reject("Invalid spawn team", m => m.spawns[0].team = 2);
        Reject("Floating spawn", m => m.spawns[0].position.y = 10);
        Reject("Spawn inside reactor", m => m.spawns[0].position = new Float3(0, 1, 0));
        Reject("Missing team", m => { foreach (var s in m.spawns) s.team = 0; });
        Test("AABB ray, distance, parallel miss and inside", () => {
            var b = Box(); Check(MapGeometry.Raycast(b,new Float3(0,0,-5),new Float3(0,0,2),10,out var t)); Equal(4,t);
            Check(!MapGeometry.Raycast(b,new Float3(2,0,-5),new Float3(0,0,1),10,out _));
            Check(!MapGeometry.Raycast(b,new Float3(0,0,-5),new Float3(0,0,1),3.9f,out _));
            Check(MapGeometry.Raycast(b,new Float3(0,0,0),new Float3(1,0,0),10,out t)); Equal(0,t);
            Check(!MapGeometry.Raycast(b,new Float3(),new Float3(),10,out _));
        });
        Test("Ramp heights at low/mid/high and open air", () => {
            var r = Ramp(0);
            foreach (var z in new[] {-6f,0f,6f}) { Check(MapGeometry.SurfaceHeight(r,0,z,out var h)); Equal((z+6)/3,h); }
            Check(!MapGeometry.Contains(r,new Float3(0,3,-3)));
            Check(MapGeometry.Contains(r,new Float3(0,1,3)));
            Check(!MapGeometry.SurfaceHeight(r,4,0,out _));
        });
        Test("All cardinal ramps raycast exact top and side", () => {
            foreach (var yaw in new[] {0f,90f,180f,270f}) {
                var r = Ramp(yaw);
                var above = MapGeometry.Rotate(new Float3(0,10,3),yaw);
                Check(MapGeometry.Raycast(r,above,new Float3(0,-1,0),20,out var t)); Equal(7,t);
                var side = MapGeometry.Rotate(new Float3(-10,1,3),yaw);
                var dir = MapGeometry.Rotate(new Float3(1,0,0),yaw);
                Check(MapGeometry.Raycast(r,side,dir,20,out t)); Equal(7,t);
                Check(!MapGeometry.Raycast(r,MapGeometry.Rotate(new Float3(-10,3,-3),yaw),dir,20,out _));
            }
        });
        Test("Wedge triangles wind outwards and cover analytical faces", () => {
            var vertices=MapGeometry.RampVertices(); var indices=MapGeometry.RampTriangles(); var inside=new Float3(0,-.25f,.1f);
            for(int i=0;i<indices.Length;i+=3) {
                var a=vertices[indices[i]]; var ab=vertices[indices[i+1]]-a; var ac=vertices[indices[i+2]]-a;
                var n=new Float3(ab.y*ac.z-ab.z*ac.y,ab.z*ac.x-ab.x*ac.z,ab.x*ac.y-ab.y*ac.x);
                Check(Float3.Dot(n,inside-a)<0);
            }
        });
        Test("Both ground bypass paths have body clearance", () => {
            var m=Map(); foreach(var x in new[]{-37f,37f}) for(int z=-25;z<=25;z++) ClearBody(m,new Float3(x,.05f,z));
        });
        Test("Central route has clearance below both bridges", () => {
            var m=Map(); foreach(var z in new[]{-20f,-14f,-10f,10f,14f,20f}) ClearBody(m,new Float3(0,.05f,z));
        });
        Test("Highground continuous from ramp/steps to galleries and bridges", () => {
            var m=Map(); foreach(var x in new[]{-27f,27f}) foreach(var z in new[]{-15f,0f,15f}) Support(m,x,z,4);
            foreach(var z in new[]{-14f,14f}) for(int x=-26;x<=26;x+=2) Support(m,x,z,4);
            var stairs=m.objects.Where(o=>o.id.StartsWith("east-south-step-")).OrderBy(o=>o.id).ToArray();
            float previous=0; foreach(var s in stairs) {var h=s.position.y+s.size.y/2; Check(h-previous<=GameRules.StepHeight); previous=h;} Equal(4,previous);
        });
        Test("Boundary blocks all four cardinal exits", () => {
            var m=Map(); foreach(var d in new[]{new Float3(1,0,0),new Float3(-1,0,0),new Float3(0,0,1),new Float3(0,0,-1)})
                Check(m.objects.Where(o=>o.group=="boundary").Any(o=>MapGeometry.Raycast(o,new Float3(0,1,0),d,45,out _)));
        });
        Test("Strict JSON rejects unknown fields", () => {
            var p=Path.Combine(Path.GetTempPath(),"fps-map-"+Guid.NewGuid()+".json");
            try { File.WriteAllText(p,File.ReadAllText(PathToMap).Replace("\"schemaVersion\": 1","\"schemaVersion\": 1, \"unexpected\": true"));
                bool threw=false; try{MapLoader.Load(p);}catch(System.Text.Json.JsonException){threw=true;} Check(threw);
            } finally {File.Delete(p);}
        });
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private static void Support(MapDefinition m,float x,float z,float expected) => Check(m.objects.Any(o=>MapGeometry.SurfaceHeight(o,x,z,out var h)&&Math.Abs(h-expected)<.001));
    private static void ClearBody(MapDefinition m,Float3 feet) { var b=new Bounds3(feet-new Float3(.35f,0,.35f),feet+new Float3(.35f,1.8f,.35f)); Check(!m.objects.Any(o=>o.colliderType!="None"&&b.Overlaps(MapGeometry.Bounds(o)))); }
    private static MapObject Box() => new MapObject{shape="Box",colliderType="Aabb",size=new Float3(2,2,2)};
    private static MapObject Ramp(float yaw) => new MapObject{shape="Ramp",colliderType="Ramp",position=new Float3(0,2,0),size=new Float3(6,4,12),rotation=new Float3(0,yaw,0)};
    private static void Reject(string name,Action<MapDefinition> edit) => Test(name,()=>{var m=Map();edit(m);bool threw=false;try{MapValidator.Validate(m);}catch(ArgumentException){threw=true;}Check(threw);});
    private static void Equal(float expected,float actual) => Check(Math.Abs(expected-actual)<.0001f,$"Expected {expected}, got {actual}");
    private static void Check(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
    private static void Test(string name,Action body) {try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
}
