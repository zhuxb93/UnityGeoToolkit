
using UnityEngine;
namespace GeoToolkit.RadarEnvelope
{
    public class AdjunctEdit : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public float showRadius = 100;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }
#if UNITY_EDITOR
        // Update is called once per frame
        void OnDrawGizmos()
        {
            if (enabled)
            {
                // 绘制线框球
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(center, showRadius);
            }
        }
#endif
    }
}

