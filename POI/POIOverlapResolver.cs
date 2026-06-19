using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit
{
    public class POIOverlapResolver : MonoBehaviour
    {
        public ComputeShader computeShader;
        public Camera mainCamera;

        private List<GameObject> poiObjects = new List<GameObject>();
        private ComputeBuffer inputBuffer;
        private ComputeBuffer resultBuffer;

        private Vector3 lastCameraPos;
        private Quaternion lastCameraRot;
        private float lastMoveTime;

        public float delay = 1f; // 相机静止延迟1秒后才比较

        struct TextMeshProData
        {
            public Vector4 screenRect; // x, y, width, height
            public float distance;
            public int index;
            public int padding;
        }

        void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            lastCameraPos = mainCamera.transform.position;
            lastCameraRot = mainCamera.transform.rotation;
            lastMoveTime = Time.time;

            poiObjects.Clear();
            foreach (Transform child in transform)
            {
                poiObjects.Add(child.GetComponentInChildren<BoxCollider>(true).gameObject);
            }
        }

        void Update()
        {
            // 判断相机是否移动
            bool cameraMoved = (mainCamera.transform.position != lastCameraPos) ||
                               (mainCamera.transform.rotation != lastCameraRot);

            if (cameraMoved)
            {
                lastCameraPos = mainCamera.transform.position;
                lastCameraRot = mainCamera.transform.rotation;
                lastMoveTime = Time.time;
                return; // 相机移动时不执行遮挡剔除
            }

            // 相机静止超过延迟时间才执行一次遮挡剔除
            if (Time.time - lastMoveTime < delay)
                return;

            // 执行遮挡剔除
            RunPOIOverlap();
        }

        private void RunPOIOverlap()
        {
            int count = poiObjects.Count;
            if (count == 0) return;

            TextMeshProData[] inputData = new TextMeshProData[count];
            int[] resultsInit = new int[count];

            for (int i = 0; i < count; i++)
            {
                GameObject poi = poiObjects[i];
                BoxCollider box = poi.GetComponentInChildren<BoxCollider>();

                Vector3 center = box.center;
                Vector3 size = box.size;
                Vector3[] corners = new Vector3[8];
                Vector3 ext = size * 0.5f;

                corners[0] = poi.transform.TransformPoint(center + new Vector3(-ext.x, -ext.y, -ext.z));
                corners[1] = poi.transform.TransformPoint(center + new Vector3(ext.x, -ext.y, -ext.z));
                corners[2] = poi.transform.TransformPoint(center + new Vector3(-ext.x, ext.y, -ext.z));
                corners[3] = poi.transform.TransformPoint(center + new Vector3(ext.x, ext.y, -ext.z));
                corners[4] = poi.transform.TransformPoint(center + new Vector3(-ext.x, -ext.y, ext.z));
                corners[5] = poi.transform.TransformPoint(center + new Vector3(ext.x, -ext.y, ext.z));
                corners[6] = poi.transform.TransformPoint(center + new Vector3(-ext.x, ext.y, ext.z));
                corners[7] = poi.transform.TransformPoint(center + new Vector3(ext.x, ext.y, ext.z));

                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                foreach (var corner in corners)
                {
                    Vector3 screenPos = mainCamera.WorldToScreenPoint(corner);
                    minX = Mathf.Min(minX, screenPos.x);
                    maxX = Mathf.Max(maxX, screenPos.x);
                    minY = Mathf.Min(minY, screenPos.y);
                    maxY = Mathf.Max(maxY, screenPos.y);
                }

                float distance = Vector3.Distance(mainCamera.transform.position, poi.transform.position);

                inputData[i] = new TextMeshProData
                {
                    screenRect = new Vector4(minX, minY, maxX - minX, maxY - minY),
                    distance = distance,
                    index = i,
                    padding = 0
                };
                resultsInit[i] = 0;
            }

            if (inputBuffer == null || inputBuffer.count != count)
            {
                inputBuffer?.Release();
                resultBuffer?.Release();

                inputBuffer = new ComputeBuffer(count, sizeof(float) * 5 + sizeof(int) * 2);
                resultBuffer = new ComputeBuffer(count, sizeof(int));
            }

            inputBuffer.SetData(inputData);
            resultBuffer.SetData(resultsInit);

            int kernel = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernel, "inputData", inputBuffer);
            computeShader.SetBuffer(kernel, "results", resultBuffer);
            computeShader.Dispatch(kernel, count, 1, 1);

            int[] results = new int[count];
            resultBuffer.GetData(results);

            for (int i = 0; i < count; i++)
            {
                poiObjects[i].SetActive(results[i] == 0);
            }
        }

        private void OnDestroy()
        {
            inputBuffer?.Release();
            resultBuffer?.Release();
        }
    }
}
