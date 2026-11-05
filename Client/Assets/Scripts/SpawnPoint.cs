using UnityEngine;

namespace FPSPrototype.Client
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        public string spawnId;
        public int team;
        private void OnDrawGizmos()
        {
            Gizmos.color = team == 0 ? Color.cyan : new Color(1,.35f,.1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, .35f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 2);
        }
    }
}
