using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit.RadarEnvelope
{
    public class EnvelopeTransparentController : MonoBehaviour
    {
        public static EnvelopeTransparentController Instance;
        [HideInInspector]
        public float cameraFOV = 60;
        public Transform sceneMainCameraTF;
        public List<FanRaderScanning> fanRaderScannings = new List<FanRaderScanning>();
        public List<DonutEffectControl> donutEffects = new List<DonutEffectControl>();

        private float rangeDistanceRatio = 0.6f;
        void Awake()
        {
            Instance = this;
        }
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (sceneMainCameraTF == null)
            {
                return;
            }
            float t = Mathf.Tan(cameraFOV * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < fanRaderScannings.Count; i++)
            {
                if (fanRaderScannings[i] != null && fanRaderScannings[i].transform.parent.gameObject.activeSelf)
                {
                    float r = fanRaderScannings[i].radiusWithRatio;
                    float r_range = r * 1.8f;
                    float r_scan = r * 2.5f;
                    float r_decal = r * 1.3f;
                    float dis_range = r_range / t;
                    float dis_decal = r_decal / t;
                    float dis_scan = r_scan / t;
                    float dis = r / t;

                    float cameraDis = Vector3.Distance(sceneMainCameraTF.position, fanRaderScannings[i].transform.position);

                    float scan_dis = Mathf.Min(dis_scan, Mathf.Max(cameraDis, dis));
                    float range_dis = Mathf.Min(dis_range, Mathf.Max(cameraDis, dis * rangeDistanceRatio));
                    float decal_dis = Mathf.Min(dis_decal, Mathf.Max(cameraDis, dis));

                    float scan_a = 1 - (scan_dis - dis_range) / (dis_scan - dis_range);
                    float decal_a = 1 - (decal_dis - dis) / (dis_decal - dis);

                    float range_a = (range_dis - dis * rangeDistanceRatio) / (dis_range - dis * rangeDistanceRatio);

                    range_a = Mathf.Max(0f, range_a);
                    fanRaderScannings[i].SetScanFade(scan_a);
                    fanRaderScannings[i].SetDecalFade(decal_a);
                    fanRaderScannings[i].SetRangeFade(range_a);
                }
            }
            for (int i = 0 ; i< donutEffects.Count; i++)
            {
                if (donutEffects[i] != null && donutEffects[i].gameObject.activeSelf)
                {
                    float r = donutEffects[i].radiusWithRatio;
                    float r_donut = r * 1.8f;
                    float dis_donut = r_donut / t;
                    float dis = r / t;

                    float cameraDis = Vector3.Distance(sceneMainCameraTF.position, donutEffects[i].transform.position);

                    float donut_dis = Mathf.Min(dis_donut, Mathf.Max(cameraDis, dis * rangeDistanceRatio));

                    float donut_a = (donut_dis - dis * rangeDistanceRatio) / (dis_donut - dis * rangeDistanceRatio);

                    donut_a = Mathf.Clamp(donut_a, 0, 1);
                    donutEffects[i].SetDonutFade(donut_a);
                }
            }
        }
    }
}
