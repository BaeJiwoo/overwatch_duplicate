using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FPSPrototype.Client;
using FPSPrototype.Shared.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPSPrototype.Editor
{
    public static class TrainingArenaBuilder
    {
        public const string SourcePath = "Packages/com.fpsprototype.shared/Map/Data/training-arena.json";
        public const string OutputPath = "Assets/Generated/TrainingArena";
        public const string ScenePath = OutputPath + "/TrainingArena.unity";

        [MenuItem("FPS Prototype/Generate Training Arena")]
        public static void GenerateMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before generating.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Generate();
        }

        public static MapDefinition Generate()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            if (source == null) throw new FileNotFoundException("Shared map package not imported: " + SourcePath);
            var map = JsonUtility.FromJson<MapDefinition>(source.text);
            MapValidator.Validate(map); // Reject unsupported geometry before creating/modifying assets.
            EnsureFolder(OutputPath + "/Materials");
            EnsureFolder(OutputPath + "/Meshes");
            ConfigureProject();
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var m in map.materials)
            {
                var material = GetOrCreate<Material>(OutputPath + "/Materials/" + m.id + ".mat", () => new Material(Shader.Find("Standard")));
                material.name = m.id;
                material.color = new Color(m.color.x, m.color.y, m.color.z);
                material.SetFloat("_Metallic", m.metallic); material.SetFloat("_Glossiness", m.smoothness);
                if (m.emission > 0) {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",material.color*m.emission);}
                else {material.DisableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);}
                EditorUtility.SetDirty(material); materials.Add(m.id, material);
            }
            var ramp = GetOrCreate<Mesh>(OutputPath + "/Meshes/UnitRamp.asset", () => new Mesh());
            ramp.Clear(); ramp.name="UnitRamp";
            var vertices=MapGeometry.RampVertices(); var indices=MapGeometry.RampTriangles();
            // Split face vertices for crisp normals while retaining exactly the shared wedge surface.
            ramp.vertices=indices.Select(i=>Vector(vertices[i])).ToArray();
            ramp.triangles=Enumerable.Range(0,indices.Length).ToArray();
            ramp.RecalculateNormals(); ramp.RecalculateBounds(); EditorUtility.SetDirty(ramp);

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Training Arena");
            var groups=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var o in map.objects)
            {
                if(!groups.TryGetValue(o.group,out var parent)) {
                    parent=new GameObject(o.group).transform;parent.SetParent(root.transform,false);groups.Add(o.group,parent);
                }
                GameObject go;
                if(o.shape=="Box") {
                    go=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if(o.colliderType=="None") UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                } else {
                    go=new GameObject();go.AddComponent<MeshFilter>().sharedMesh=ramp;go.AddComponent<MeshRenderer>();
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=ramp;collider.convex=false;
                }
                go.name=o.id;go.transform.SetParent(parent,false);
                go.transform.position=Vector(o.position);go.transform.eulerAngles=Vector(o.rotation);go.transform.localScale=Vector(o.size);
                go.GetComponent<Renderer>().sharedMaterial=materials[o.material];
                go.GetComponent<Renderer>().shadowCastingMode=o.colliderType=="None"?ShadowCastingMode.Off:ShadowCastingMode.On;
                var identity=go.AddComponent<MapObjectIdentity>();identity.objectId=o.id;identity.colliderType=o.colliderType;
                go.isStatic=true;
            }
            var spawnRoot=new GameObject("SpawnPoints").transform;spawnRoot.SetParent(root.transform,false);
            foreach(var s in map.spawns) {
                var go=new GameObject(s.id);go.transform.SetParent(spawnRoot,false);go.transform.position=Vector(s.position);go.transform.rotation=Quaternion.Euler(0,s.yaw,0);
                var point=go.AddComponent<SpawnPoint>();point.spawnId=s.id;point.team=s.team;
            }
            var config=GetOrCreate<MapConfiguration>(OutputPath+"/MapConfiguration.asset",ScriptableObject.CreateInstance<MapConfiguration>);
            config.definition=source;config.mapId=map.id;config.schemaVersion=map.schemaVersion;config.sha256=Hash(source.bytes);config.objectCount=map.objects.Length;config.spawnCount=map.spawns.Length;
            EditorUtility.SetDirty(config);
            Lighting(root.transform);
            var cameraGo=new GameObject("Preview Camera");cameraGo.tag="MainCamera";
            var camera=cameraGo.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=220;camera.fieldOfView=58;
            camera.backgroundColor=new Color(.055f,.09f,.15f);camera.clearFlags=CameraClearFlags.Skybox;
            camera.allowHDR=true;cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position=new Vector3(53,47,-58);cameraGo.transform.LookAt(new Vector3(0,1,0));
            cameraGo.AddComponent<ArenaPreview>().configuration=config;
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Failed to save arena scene.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated {map.displayName}: {map.objects.Length} objects, {map.spawns.Length} spawns, map SHA256 {config.sha256}");
            return map;
        }

        private static void ConfigureProject()
        {
            EditorSettings.serializationMode=SerializationMode.ForceText;
            PlayerSettings.companyName="FPS Prototype";PlayerSettings.productName="Training Arena";
            PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var settingsAssets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if(settingsAssets.Length>0) {
                var settings=new SerializedObject(settingsAssets[0]);var input=settings.FindProperty("activeInputHandler");
                if(input!=null && input.intValue!=0) {input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            }
            QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=120;
        }
        private static void Lighting(Transform root)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.55f,.66f,.8f);RenderSettings.ambientEquatorColor=new Color(.34f,.4f,.48f);RenderSettings.ambientGroundColor=new Color(.16f,.2f,.28f);
            RenderSettings.fog=false;RenderSettings.reflectionIntensity=.35f;
            var sky=GetOrCreate<Material>(OutputPath+"/Materials/ArenaSky.mat",()=>new Material(Shader.Find("Skybox/Procedural")));
            sky.SetColor("_SkyTint",new Color(.4f,.57f,.7f));sky.SetColor("_GroundColor",new Color(.16f,.2f,.28f));sky.SetFloat("_Exposure",1.1f);
            EditorUtility.SetDirty(sky);RenderSettings.skybox=sky;
            var sun=new GameObject("Arena Sun").AddComponent<Light>();sun.transform.SetParent(root,false);sun.type=LightType.Directional;sun.intensity=1.25f;
            sun.color=new Color(1,.94f,.83f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;sun.transform.rotation=Quaternion.Euler(48,-30,0);RenderSettings.sun=sun;
            foreach(var z in new[]{-32f,32f}) {
                var l=new GameObject(z<0?"Cyan Spawn Light":"Orange Spawn Light").AddComponent<Light>();l.transform.SetParent(root,false);l.transform.position=new Vector3(0,3.8f,z);
                l.type=LightType.Point;l.range=14;l.intensity=2;l.color=z<0?new Color(.1f,.65f,1):new Color(1,.45f,.18f);l.shadows=LightShadows.None;
            }
        }
        private static T GetOrCreate<T>(string path,Func<T> create) where T:UnityEngine.Object {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;asset=create();AssetDatabase.CreateAsset(asset,path);return asset;
        }
        private static void EnsureFolder(string path) {
            if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        public static Vector3 Vector(Float3 p)=>new Vector3(p.x,p.y,p.z);
        public static Float3 Shared(Vector3 p)=>new Float3(p.x,p.y,p.z);
        public static string Hash(byte[] bytes) {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
