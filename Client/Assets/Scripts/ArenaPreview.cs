using UnityEngine;

namespace FPSPrototype.Client
{
    // Phase 1 scene inspection only. Authoritative FPS control arrives in Phase 4.
    public sealed class ArenaPreview : MonoBehaviour
    {
        public MapConfiguration configuration;
        public bool showOverlay = true;
        private float yaw, pitch;
        private void Start()
        {
            yaw=transform.eulerAngles.y; pitch=transform.eulerAngles.x;
            if (pitch>180) pitch-=360;
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxisRaw("Mouse X")*2;
                pitch = Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-85,85);
                transform.rotation=Quaternion.Euler(pitch,yaw,0);
                var v=new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
                    (Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0),
                    (Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
                transform.position += transform.TransformDirection(Vector3.ClampMagnitude(v,1))*Time.deltaTime*(Input.GetKey(KeyCode.LeftShift)?18:8);
            }
            if(Input.GetKeyDown(KeyCode.Home)) {transform.position=new Vector3(53,47,-58);transform.LookAt(new Vector3(0,1,0));yaw=transform.eulerAngles.y;pitch=transform.eulerAngles.x;}
            if(Input.GetKeyDown(KeyCode.End)) {transform.position=new Vector3(0,1.65f,-31);transform.rotation=Quaternion.identity;yaw=pitch=0;}
        }
        private void OnGUI()
        {
            if(!showOverlay) return;
            var previous=GUI.color;
            GUI.color=new Color(.04f,.07f,.11f,.93f); GUI.DrawTexture(new Rect(20,20,530,105),Texture2D.whiteTexture);
            GUI.color=new Color(.2f,.9f,1); GUI.Label(new Rect(38,30,490,32),"TRAINING ARENA",new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold});
            GUI.color=Color.white; GUI.Label(new Rect(38,65,490,24),"PHASE 1  /  MAP PREVIEW  /  OFFLINE");
            GUI.Label(new Rect(38,89,490,24),"RMB + WASD: fly   Q/E: height   Shift: boost   Home/End: views");
            GUI.color=previous;
        }
    }
}
