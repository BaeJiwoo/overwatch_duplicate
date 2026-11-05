using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FPSPrototype.Client;
using FPSPrototype.Shared.Map;
using UnityEditor;
using UnityEngine;

namespace FPSPrototype.Editor
{
    [InitializeOnLoad]
    public static class Phase1Validation
    {
        private const string Active="FPSPrototype.Validation.Active";
        private const string Error="FPSPrototype.Validation.Error";
        private static readonly string Artifacts=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts"));
        [Serializable] private sealed class Report {
            public bool passed, editorGeneration, deterministicRegeneration, colliderParity, playMode;
            public string unityVersion, mapSha256, geometrySha256, error;
            public int objects, spawns, rayChecks;
        }
        static Phase1Validation() {EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}

        [MenuItem("FPS Prototype/Validate Phase 1 (includes Play Mode)")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            if(!Application.isBatchMode&&!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Artifacts);
            File.WriteAllText(Path.Combine(Artifacts,"phase1-validation.json"),JsonUtility.ToJson(new Report{unityVersion=Application.unityVersion},true));
            SessionState.SetString(Error,"");
            try {
                var map=TrainingArenaBuilder.Generate();
                var before=Fingerprint();
                var guids=AssetGuids();
                TrainingArenaBuilder.Generate();
                Require(before==Fingerprint(),"Regeneration changed geometry/materials/spawns.");
                Require(guids==AssetGuids(),"Regeneration duplicated or replaced assets.");
                var checks=ValidateGeometry(map);
                var config=AssetDatabase.LoadAssetAtPath<MapConfiguration>(TrainingArenaBuilder.OutputPath+"/MapConfiguration.asset");
                var report=new Report{editorGeneration=true,deterministicRegeneration=true,colliderParity=true,unityVersion=Application.unityVersion,mapSha256=config.sha256,geometrySha256=before,objects=map.objects.Length,spawns=map.spawns.Length,rayChecks=checks};
                File.WriteAllText(Path.Combine(Artifacts,"phase1-validation.json"),JsonUtility.ToJson(report,true));
                SessionState.SetBool(Active,true);SessionState.SetBool("FPSPrototype.Validation.PlayChecked",false);
                SessionState.SetFloat("FPSPrototype.Validation.Start",(float)EditorApplication.timeSinceStartup);
                // Let startup delay callbacks create the Editor's search database before
                // isPlayingOrWillChangePlaymode becomes true (important on a fresh project).
                EditorApplication.delayCall+=EnterPlayModeWhenReady;
            } catch(Exception e) {Fail(e.ToString());}
        }

        private static void EnterPlayModeWhenReady()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating) {
                EditorApplication.delayCall+=EnterPlayModeWhenReady;
                return;
            }
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            try {
                var error=SessionState.GetString(Error,"");Require(string.IsNullOrEmpty(error),error);
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat("FPSPrototype.Validation.Start",0)<90,"Play Mode validation timeout.");
                if(EditorApplication.isPlaying && Time.frameCount>15 && !SessionState.GetBool("FPSPrototype.Validation.PlayChecked",false)) {
                    Require(Camera.main!=null && Camera.main.GetComponent<ArenaPreview>()!=null,"Preview camera missing in Play Mode.");
                    var preview=Camera.main.GetComponent<ArenaPreview>();Require(preview.configuration!=null,"MapConfiguration missing.");
                    var map=JsonUtility.FromJson<MapDefinition>(preview.configuration.definition.text);
                    ValidateGeometry(map);
                    Require(Physics.Raycast(new Vector3(0,2,-22),Vector3.down,out var hit,5),"Play Mode ground ray missed.");
                    Require(Mathf.Abs(hit.point.y)<.03f,"Unexpected ground height.");
                    foreach(var spawn in map.spawns) {
                        var hits=Physics.OverlapBox(TrainingArenaBuilder.Vector(spawn.position)+Vector3.up*.9f,new Vector3(.34f,.89f,.34f));
                        Require(hits.Length==0,"Play Mode spawn overlaps collider: "+spawn.id);
                    }
                    Capture(Camera.main,"training-arena-overview.png",new Vector3(53,47,-58),new Vector3(0,1,0),58);
                    Capture(Camera.main,"training-arena-ground.png",new Vector3(2,1.8f,-24),new Vector3(0,3,0),75);
                    Capture(Camera.main,"training-arena-highground.png",new Vector3(-27,5.65f,-15),new Vector3(0,4.5f,5),75);
                    var path=Path.Combine(Artifacts,"phase1-validation.json");var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));
                    report.passed=report.playMode=true;File.WriteAllText(path,JsonUtility.ToJson(report,true));
                    SessionState.SetBool("FPSPrototype.Validation.PlayChecked",true);EditorApplication.ExitPlaymode();
                }
                if(SessionState.GetBool("FPSPrototype.Validation.PlayChecked",false)&&!EditorApplication.isPlayingOrWillChangePlaymode) {
                    SessionState.SetBool(Active,false);Debug.Log("PHASE1_VALIDATION_PASSED: generation, regeneration, geometry parity, Play Mode, screenshots.");
                    if(Application.isBatchMode)EditorApplication.Exit(0);
                }
            } catch(Exception e) {Fail(e.ToString());}
        }

        private static int ValidateGeometry(MapDefinition map)
        {
            MapValidator.Validate(map);Physics.SyncTransforms();
            var objects=UnityEngine.Object.FindObjectsByType<MapObjectIdentity>(FindObjectsSortMode.None);
            Require(objects.Length==map.objects.Length,"Object count differs from definition.");
            var lookup=objects.ToDictionary(o=>o.objectId,StringComparer.Ordinal);
            int checks=0;
            foreach(var o in map.objects) {
                Require(lookup.TryGetValue(o.id,out var identity),"Missing object: "+o.id);
                Require((identity.transform.position-TrainingArenaBuilder.Vector(o.position)).sqrMagnitude<.000001f,"Position mismatch: "+o.id);
                Require((identity.transform.localScale-TrainingArenaBuilder.Vector(o.size)).sqrMagnitude<.000001f,"Size mismatch: "+o.id);
                Require(Quaternion.Angle(identity.transform.rotation,Quaternion.Euler(TrainingArenaBuilder.Vector(o.rotation)))<.001f,"Rotation mismatch: "+o.id);
                var colliders=identity.GetComponents<Collider>();
                if(o.colliderType=="None") {Require(colliders.Length==0,"Visual-only object has collision.");continue;}
                Require(colliders.Length==1,"Expected one collider.");var collider=colliders[0];
                Require(o.colliderType=="Aabb"?collider is BoxCollider:collider is MeshCollider,"Wrong collider type.");
                foreach(var x in new[]{-.4f,0f,.4f})foreach(var z in new[]{-.4f,0f,.4f}) {
                    var offset=MapGeometry.Rotate(new Float3(x*o.size.x,0,z*o.size.z),o.rotation.y);
                    var origin=o.position+offset;origin.y=map.arenaSize.y+10;
                    var shared=MapGeometry.Raycast(o,origin,new Float3(0,-1,0),100,out var expected);
                    var unity=collider.Raycast(new Ray(TrainingArenaBuilder.Vector(origin),Vector3.down),out var hit,100);
                    Require(shared&&unity&&Math.Abs(expected-hit.distance)<.002f,"Server/Unity top surface mismatch: "+o.id);checks++;
                }
                // Verify vertical side hits and empty volume above the sloped ramp face.
                if(o.colliderType=="Ramp")foreach(var y in new[]{-.35f,.35f}) {
                    var origin=o.position+MapGeometry.Rotate(new Float3(-o.size.x*2,y*o.size.y,-.25f*o.size.z),o.rotation.y);
                    var dir=MapGeometry.Rotate(new Float3(1,0,0),o.rotation.y);
                    var shared=MapGeometry.Raycast(o,origin,dir,100,out var expected);
                    var unity=collider.Raycast(new Ray(TrainingArenaBuilder.Vector(origin),TrainingArenaBuilder.Vector(dir)),out var hit,100);
                    Require(shared==unity&&(!shared||Math.Abs(expected-hit.distance)<.002f),"Ramp side/air mismatch: "+o.id);checks++;
                }
            }
            var spawns=UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            Require(spawns.Length==map.spawns.Length,"Spawn count differs.");
            foreach(var s in map.spawns) {
                var point=spawns.Single(p=>p.spawnId==s.id);Require(point.team==s.team,"Spawn team differs.");
                Require((point.transform.position-TrainingArenaBuilder.Vector(s.position)).sqrMagnitude<.000001f,"Spawn position differs.");
                Require(Quaternion.Angle(point.transform.rotation,Quaternion.Euler(0,s.yaw,0))<.001f,"Spawn yaw differs.");
            }
            return checks;
        }

        private static string Fingerprint()
        {
            var b=new StringBuilder();
            foreach(var o in UnityEngine.Object.FindObjectsByType<MapObjectIdentity>(FindObjectsSortMode.None).OrderBy(o=>o.objectId,StringComparer.Ordinal)) {
                b.Append(o.objectId).Append('|').Append(o.colliderType).Append('|').Append(o.transform.parent.name);
                Append(b,o.transform.position);Append(b,o.transform.eulerAngles);Append(b,o.transform.localScale);
                var mesh=o.GetComponent<MeshFilter>().sharedMesh;
                foreach(var p in mesh.vertices)Append(b,p);foreach(var t in mesh.triangles)b.Append('|').Append(t);
                var mat=o.GetComponent<Renderer>().sharedMaterial;b.Append('|').Append(mat.name).Append('|').Append(mat.color.ToString("F6"));
            }
            foreach(var s in UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).OrderBy(s=>s.spawnId,StringComparer.Ordinal)) {b.Append(s.spawnId).Append(s.team);Append(b,s.transform.position);Append(b,s.transform.eulerAngles);}
            return TrainingArenaBuilder.Hash(Encoding.UTF8.GetBytes(b.ToString()));
        }
        private static void Append(StringBuilder b,Vector3 v) {b.Append('|').Append(v.x.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.z.ToString("R",CultureInfo.InvariantCulture));}
        private static string AssetGuids()=>string.Join("|",AssetDatabase.FindAssets("",new[]{TrainingArenaBuilder.OutputPath}).OrderBy(s=>s,StringComparer.Ordinal));
        private static void Capture(Camera camera,string name,Vector3 position,Vector3 target,float fov)
        {
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldFov=camera.fieldOfView;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var texture=new RenderTexture(1600,1000,24){antiAliasing=4};var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try {
                camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Artifacts,name),pixels.EncodeToPNG());
            } finally {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;UnityEngine.Object.DestroyImmediate(pixels);texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static void OnLog(string message,string stack,LogType type) {if(SessionState.GetBool(Active,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))SessionState.SetString(Error,message+"\n"+stack);}
        private static void Require(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
        private static void Fail(string error) {
            SessionState.SetBool(Active,false);Directory.CreateDirectory(Artifacts);
            var path=Path.Combine(Artifacts,"phase1-validation.json");
            var report=File.Exists(path)?JsonUtility.FromJson<Report>(File.ReadAllText(path)):new Report();
            report.passed=false;report.unityVersion=Application.unityVersion;report.error=error;
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.LogError("PHASE1_VALIDATION_FAILED: "+error);if(Application.isBatchMode)EditorApplication.Exit(1);
        }
    }
}
