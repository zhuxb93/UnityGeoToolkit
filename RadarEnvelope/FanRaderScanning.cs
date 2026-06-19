using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace GeoToolkit.RadarEnvelope
{
    public class FanRaderScanning : MonoBehaviour
    {
        [HideInInspector]
        public int equipmentId = -1;
        public float speed = 150;
        private Material material;
        private Material parentMaterial;
        public Material donutMaterial = null;
        private Color color = Color.white;
        private Color p_color = Color.white;
        private Color d_color = Color.white;
        public float radiusWithRatio = 100;

        private DecalProjector decalProjector;
        public void Start()
        {
            Renderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                material = new Material(renderer.sharedMaterial);
                renderer.sharedMaterial = material;
                color = material.color;
            }

            if (transform.parent != null)
            {
                Renderer prenderer = transform.parent.gameObject.GetComponent<MeshRenderer>();
                parentMaterial = new Material(prenderer.sharedMaterial);
                prenderer.sharedMaterial = parentMaterial;
                p_color = parentMaterial.color;

                decalProjector = transform.parent.gameObject.GetComponentInChildren<DecalProjector>(true);

                CircleDonutEnvelope donut = transform.parent.gameObject.GetComponentInChildren<CircleDonutEnvelope>(true);
                if (donut != null)
                {
                    Renderer drenderer = donut.gameObject.GetComponent<Renderer>();
                    donutMaterial = new Material(drenderer.sharedMaterial);
                    drenderer.sharedMaterial = donutMaterial;
                    d_color = donutMaterial.color;
                }
            }


        }

        public void FixedUpdate()
        {
            transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.World);
        }

        // 扫描材质 扇形扫描，范围贴画
        public void SetScanFade(float a)
        {
            if (material != null)
            {
                color.a = a;
                material.color = color;
            }
        }
        public void SetDecalFade(float a)
        {
            if (decalProjector != null)
            {
                float dpa = Mathf.Min(a, 0.75f);
                decalProjector.fadeFactor = dpa;
            }
        }
        // 范围材质 半球体  半椭球体  甜甜圈材质
        public void SetRangeFade(float a)
        {
            if (parentMaterial != null)
            {
                p_color.a = a;
                parentMaterial.color = p_color;
            }
            if (donutMaterial != null)
            {
                d_color.a = a;
                donutMaterial.color = d_color;
            }
        }
    }
}

