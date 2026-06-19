using UnityEngine;

namespace GeoToolkit.RadarEnvelope
{
    public class DonutEffectControl : MonoBehaviour
    {
        [HideInInspector]
        public int id;
        [HideInInspector]
        private Material material;
        private Color matColor;
        public float radiusWithRatio = 100;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            material = gameObject.GetComponent<MeshRenderer>().sharedMaterial;
            matColor = material.color;
        }
        public void SetDonutFade(float a)
        {
            if(material != null)
            {
                matColor.a = a;
                material.color = matColor;
            }
        }
    }
}

