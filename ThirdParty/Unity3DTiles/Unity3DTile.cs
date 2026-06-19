using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity3DTiles.Schema;
using UnityEngine.Networking;
using System.Threading.Tasks;
using GeoToolkit;

namespace Unity3DTiles
{
    public enum Unity3DTileContentState
    {
        UNLOADED = 0,   // Has never been requested
        LOADING = 1,    // Is waiting on a pending request
        PROCESSING = 2, // Request received.  Contents are being processed for rendering.  Depending on the content, it might make its own requests for external data.
        READY = 3      // Ready to render.
    };

    public enum Unity3DTileContentType
    {
        B3DM,
        PNTS,
        JSON,
        GLB,
        Unknown
    }

    [Serializable]
    public class Unity3DTileFrameState
    {
        public int LastVisitedFrame = -1;

        public bool InFrustumSet = false;  // Currently in view of a camera (And in this frames "Used set")
        public bool InUsedSet = false;     // should be checked with IsUsedThisFrame
        public bool IsUsedSetLeaf = false;
        public bool InRenderSet = false;   // This tile should be rendered this frame
        public bool InColliderSet = false; // This tile should have its collider enabled this frame
        public bool UsedLastFrame = false; // This tile was in the used set last frame
        public float DistanceToCamera = float.MaxValue;
        public float PixelsToCameraCenter = float.MaxValue;
        public float ScreenSpaceError = 0;
        public float FoveatedFactor = 1;
        public float Priority = float.MaxValue; // lower value means higher priority

        public void MarkUsed()
        {
            InUsedSet = true;
        }

        public bool IsUsedThisFrame
        {
            get
            {
                return InUsedSet && LastVisitedFrame == Time.frameCount;
            }
        }

        public bool IsUsedRecently()
        {
            return IsUsedThisFrame || UsedLastFrame;
        }


        public void Reset()
        {
            if (LastVisitedFrame == Time.frameCount)
            {
                return;
            }
            LastVisitedFrame = Time.frameCount;
            InUsedSet = false;
            InFrustumSet = false;
            IsUsedSetLeaf = false;
            InRenderSet = false;
            InColliderSet = false;
            DistanceToCamera = float.MaxValue;
            PixelsToCameraCenter = float.MaxValue;
            FoveatedFactor = float.MaxValue;
            ScreenSpaceError = 0;
            Priority = float.MaxValue;
        }
    }

    public class Unity3DTileInfo : MonoBehaviour
    {
        public Unity3DTileFrameState FrameState;
        public Unity3DTile Tile;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(Unity3DTileInfo))]
    public class Unity3DTileInfoEditor : UnityEditor.Editor
    {
        private UnityEditor.SerializedProperty frameState;

        public void OnEnable()
        {
            frameState = serializedObject.FindProperty("FrameState");
        }

        public override void OnInspectorGUI()
        {
            var ti = target as Unity3DTileInfo;
            //DrawDefaultInspector();
            UnityEditor.EditorGUILayout.LabelField("Used This Frame", ti.FrameState.IsUsedThisFrame.ToString());
            UnityEditor.EditorGUILayout.LabelField("Id", ti.Tile.Id);
            UnityEditor.EditorGUILayout.LabelField("Parent", ti.Tile.Parent != null ? ti.Tile.Parent.Id : "null");
            UnityEditor.EditorGUILayout.LabelField("Depth", ti.Tile.Depth.ToString());
            UnityEditor.EditorGUILayout.LabelField("Geometric Error", ti.Tile.GeometricError.ToString());
            UnityEditor.EditorGUILayout.LabelField("Refine", ti.Tile.Refine.ToString());
            UnityEditor.EditorGUILayout.LabelField("Has Empty Content", ti.Tile.HasEmptyContent.ToString());
            UnityEditor.EditorGUILayout.LabelField("Content Url", ti.Tile.ContentUrl);
            UnityEditor.EditorGUILayout.LabelField("Content Type", ti.Tile.ContentType.ToString());
            UnityEditor.EditorGUILayout.LabelField("Content State", ti.Tile.ContentState.ToString());
            UnityEditor.EditorGUILayout.LabelField("Request Fail Time", ti.Tile.requestFailTime.ToString());
            UnityEditor.EditorGUILayout.LabelField("FoveatedFactor", ti.Tile.FrameState.FoveatedFactor.ToString());
            UnityEditor.EditorGUILayout.LabelField("ScreenSpaceError", ti.Tile.FrameState.ScreenSpaceError.ToString());
            UnityEditor.EditorGUILayout.PropertyField(frameState);
        }
    }
#endif

    public class Unity3DTile
    {
        public static char[] urlQuery = new char[3] {'?', '&', '#'};
        public static int destroyCounter = 0;

        private Matrix4x4d transform;
        //private Matrix4x4d transform1;

        public Tile SchemaTile;
        public Unity3DTileset Tileset;
        public Unity3DTile Parent;
        public List<Unity3DTile> Children = new List<Unity3DTile>();

        //public Matrix4x4d EcefTransform;
        public Matrix4x4d ComputedTransform;
        public Unity3DTileBoundingVolume BoundingVolume;
        public Unity3DTileBoundingVolume ContentBoundingVolume;
        public Unity3DTilesetStyle Style;
        public Unity3DTileContentType ContentType;
        public Unity3DTileContent Content;
        public Unity3DTileContentState ContentState;
        public Unity3DTileFrameState FrameState = new Unity3DTileFrameState();
        public TileRefine Refine;

        public int Depth = 0;
        public int requestFailTime = 0;
        public float GeometricError;
        public bool IsChildTileset = false;
        public bool HasEmptyContent;
        public string Id;
        public string ContentUrl;
        public Vector3 boundsScale = Vector3.one;

        public bool CanTraverse(double screenSpaceError)
        {
            if (Parent != null)
            {
                if (Children.Count == 0) return false;
                if (ContentType == Unity3DTileContentType.JSON) return ContentState == Unity3DTileContentState.READY;
            }
            return FrameState.ScreenSpaceError > screenSpaceError;
        }

        public void MarkUsed()
        {
            FrameState.MarkUsed();
            Tileset.TileCache.MarkUsed(this);
        }

        public Unity3DTile(Unity3DTileset tileset, string basePath, Schema.Tile schemaTile, Unity3DTile parent)
        {
            Tileset = tileset;
            SchemaTile = schemaTile;
            if (schemaTile.Content != null)
            {
                Id = Path.GetFileNameWithoutExtension(schemaTile.Content.GetUri());
            }
            if (parent != null)
            {
                parent.Children.Add(this);
                Depth = parent.Depth + 1;
            }

            if (IsChildTileset)
            {
                transform = parent.ComputedTransform;
                ComputedTransform = transform;

                //transform1 = parent.EcefTransform;
                //EcefTransform = parent.EcefTransform;
            }
            else
            {
                transform = schemaTile.UnityTransform();
                var parentTransform = (parent != null) ? parent.ComputedTransform : Matrix4x4d.ToMatrix4x4d(tileset.GetRootTransform());
                ComputedTransform = parentTransform * transform;

                //transform1 = schemaTile.UnityTransform();
                //var ecefParent = (parent != null) ? parent.EcefTransform : Matrix4x4d.identity;
                //EcefTransform = ecefParent * transform1;
            }

            BoundingVolume = CreateBoundingVolume(schemaTile.BoundingVolume, ComputedTransform);

            if (schemaTile.Content != null && schemaTile.Content.BoundingVolume.IsDefined())
            {
                ContentBoundingVolume = CreateBoundingVolume(schemaTile.Content.BoundingVolume, ComputedTransform);
            }
            else
            {
                ContentBoundingVolume = CreateBoundingVolume(schemaTile.BoundingVolume, ComputedTransform);
            }

            if (!schemaTile.Refine.HasValue)
            {
                schemaTile.Refine = (parent == null) ? Schema.TileRefine.REPLACE : parent.SchemaTile.Refine.Value;
            }

            GeometricError = (float)schemaTile.GeometricError * (float)CoordinateConversion.tileScale;

            Refine = schemaTile.Refine.Value;

            HasEmptyContent = (schemaTile.Content == null);

            Parent = parent;

            if (HasEmptyContent)
            {
                ContentState = Unity3DTileContentState.READY;
            }
            else
            {
                ContentState = Unity3DTileContentState.UNLOADED;
                string relPath = schemaTile.Content.GetUri();
                if (relPath.IndexOfAny(urlQuery) != -1 || basePath.IndexOfAny(urlQuery) != -1)
                {
                    ContentUrl = UrlUtils.JoinUrls(basePath, schemaTile.Content.GetUri());
                }
                else
                {
                    ContentUrl = (basePath + schemaTile.Content.GetUri()).Replace("\\", "/");
                }
            }

            ContentType = GetContentType();

            requestFailTime = 0;
        }

        private Unity3DTileContentType GetContentType()
        {
            if (ContentUrl != null)
            {
                string ext = Path.GetExtension(UrlUtils.RemoveQuery(ContentUrl)).ToLower();
                if (ext.Equals(".b3dm"))
                {
                    return Unity3DTileContentType.B3DM;
                }
                if (ext.Equals(".pnts"))
                {
                    return Unity3DTileContentType.PNTS;
                }
                if (ext.Equals(".json"))
                {
                    return Unity3DTileContentType.JSON;
                }
                if (ext.Equals(".glb"))
                {
                    return Unity3DTileContentType.GLB;
                }
            }
            return Unity3DTileContentType.Unknown;
        }

        /// <summary>
        /// Lower priority will be loaded sooner
        /// </summary>
        /// <param name="priority"></param>
        public void RequestContent(float priority)
        {
            if (HasEmptyContent || ContentState != Unity3DTileContentState.UNLOADED)
                return;

            Request request = Tileset.RequestManager.GetRequestFromRecycleQueue();
            if (request == null)
            {
                request = new Request(this, null, null);
            }
            else
            {
                request.Tile = this;
            }
            Tileset.RequestManager.EnqueRequest(request);
        }

        private GameObject GenerateNodeObj()
        {
            GameObject go = new GameObject(Id);
            go.name = Id;

            //Debug.LogError($"EcefTransform:{EcefTransform}");
            //double[] llh = CoordinateConversion.ECEFToLLA(EcefTransform.m03, EcefTransform.m13, EcefTransform.m23);
            //Vector3 position = CoordinateConversion.LatLonHeightToWorldPosition(llh[0], llh[1], llh[2]);
            //Matrix4x4d enuRotationMatrix = CoordinateConversion.GetENURotationMatrix2(llh[0], llh[1]);
            //Vector3 forwardECEF = EcefTransform.rotation * Vector3.forward;
            //Vector4d forward = enuRotationMatrix * new Vector4d(forwardECEF.x, forwardECEF.y, forwardECEF.z, 0);
            //go.transform.position = position;
            //go.transform.forward = new Vector3((float)forward.x, (float)forward.y, (float)forward.z);
            //Debug.LogError($"scale:{CoordinateConversion.tileScale}");
            //go.transform.localScale = EcefTransform.lossyScale.ToVector3() * (float)CoordinateConversion.tileScale;

            go.transform.position = new Vector3((float)ComputedTransform.m03, (float)ComputedTransform.m13, (float)ComputedTransform.m23);
            Vector3 forward = ComputedTransform.rotation * Vector3.forward;
            go.transform.forward = forward.normalized;
            go.transform.localScale = ComputedTransform.lossyScale.ToVector3();
            go.layer = Tileset.Transform.gameObject.layer;
            go.transform.parent = Tileset.Behaviour.transform;
            var info = go.AddComponent<Unity3DTileInfo>();
            info.Tile = this;
            info.FrameState = FrameState;
            Content = new Unity3DTileContent(go);
            go.SetActive(false);
            go.transform.localEulerAngles += Vector3.up * 180;
            return go;
        }

        public async void Started()
        {
            if (ContentType == Unity3DTileContentType.B3DM)
            {
                GenerateNodeObj();
                UnityWebRequest request = UnityWebRequest.Get(ContentUrl);
                UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
                while (!asyncOperation.isDone)
                {
                    if (!FrameState.IsUsedThisFrame)
                    {
                        request.Abort();
                        FinishedRequestData(false, null);
                        return;
                    }
                    await Task.Yield();
                }
                if (request.isDone && request.result == UnityWebRequest.Result.Success)
                {
                    FinishedRequestData(true, request.downloadHandler.data);
                }
                else
                {
                    FinishedRequestData(false, null);
                    requestFailTime++;
                }
            }
            else if (ContentType == Unity3DTileContentType.JSON)
            {
                UnityWebRequest request = UnityWebRequest.Get(ContentUrl);
                UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
                while (!asyncOperation.isDone)
                {
                    if (!FrameState.IsUsedThisFrame)
                    {
                        request.Abort();
                        FinishedRequestJson(false, string.Empty);
                        return;
                    }
                    await Task.Yield();
                }
                if (request.isDone && request.result == UnityWebRequest.Result.Success)
                {
                    FinishedRequestJson(true, request.downloadHandler.text);
                }
                else
                {
                    FinishedRequestJson(false, string.Empty);
                    requestFailTime++;
                }
            }
        }

        private void FinishedRequestJson(bool success, string json)
        {
            Tileset.Statistics.RequestsThisFrame++;
            Tileset.Statistics.NetworkErrorsThisFrame += success ? 0 : 1;
            if (success)
            {
                Tileset.LoadChildTileset(ContentUrl, json, this);
                IsChildTileset = true;
                ContentState = Unity3DTileContentState.READY;
            }
            else
            {
                UnloadContent();
            }
            Tileset.RequestManager.RemoveActiveRequest(this);
        }

        private void FinishedRequestData(bool success, byte[] data)
        {
            Tileset.Statistics.RequestsThisFrame++;
            Tileset.Statistics.NetworkErrorsThisFrame += success ? 0 : 1;
            if (success)
            {
                ContentState = Unity3DTileContentState.PROCESSING;
                Tileset.B3DMBatchLoader.EnqueueLoad(data, this);
            }
            else
            {
                UnloadContent();
            }
            Tileset.RequestManager.RemoveActiveRequest(this);
        }

        public void FinishedLoad(bool success, string msg)
        {
            Tileset.Statistics.LoadThisFrame++;
            Tileset.Statistics.LoadErrorsThisFrame += success ? 0 : 1;
            bool duplicate = false;
            if (success)
            {
                ContentState = Unity3DTileContentState.READY;
                bool result = Tileset.TileCache.Add(this, out duplicate);
                if (!result)
                {
                    UnloadContent();
                }
            }
            else if (!duplicate)
            {
                UnloadContent();
            }
        }

        public void UnloadContent()
        {
            if (HasEmptyContent)
            {
                return;
            }
            ContentState = Unity3DTileContentState.UNLOADED;
            if (Content != null && Content.Go != null)
            {
                GameObject.Destroy(Content.Go);
                Content = null;
                destroyCounter++;
                if (destroyCounter >= Tileset.Behaviour.SceneOptions.MaxDestroyThreshold)
                {
                    destroyCounter = 0;
                    Tileset.Behaviour.RequestUnloadUnusedAssets();
                }
            }
        }

        public float GetFoveatedFactor(Vector3 camPos, Vector3 camDir)
        {
            var boundingSphere = BoundingVolume.GetBoundingSphere();
            Vector3 center = boundingSphere.position;
            float radius = boundingSphere.radius;
            Vector3 toLine = Vector3.Project(center - camPos, camDir) + camPos - center;
            bool notTouchingSphere = toLine.sqrMagnitude > radius;
            if (notTouchingSphere)
            {
                Vector3 closestOnSphere = center + Vector3.Normalize(toLine) * radius;
                Vector3 toClosestOnSphereNormalize = Vector3.Normalize(closestOnSphere - camPos);

                return 1 - Mathf.Abs(Vector3.Dot(camDir, toClosestOnSphereNormalize));
            }
            else
            {
                return 0;
            }
        }

        Unity3DTileBoundingVolume CreateBoundingVolume(Schema.BoundingVolume boundingVolume, Matrix4x4d transform)
        {
            if (boundingVolume.Box.Count == 12)
            {
                var box = boundingVolume.Box;
                // 考虑unity为左手坐标系, Y-Z需要修正顺序
                Vector3 center = new Vector3((float)box[0], (float)box[2], (float)box[1]);
                Vector3 halfAxesX = new Vector3((float)box[3], (float)box[5], (float)box[4]);
                Vector3 halfAxesY = new Vector3((float)box[9], (float)box[11], (float)box[10]);
                Vector3 halfAxesZ = new Vector3((float)box[6], (float)box[8], (float)box[7]);

                // TODO: Review this coordinate frame change
                // This does not take into account the coodinate frame of the glTF files and gltfUpAxis
                // https://github.com/AnalyticalGraphicsInc/3d-tiles/issues/280#issuecomment-359980111
                // center.x *= -1;
                // halfAxesX.x *= - 1;
                // halfAxesY.x *= -1;
                // halfAxesZ.x *= -1;

                var result = new TileOrientedBoundingBox(center, halfAxesX, halfAxesY, halfAxesZ);
                result.Transform(Matrix4x4d.ToMatrix4x4(transform));

                //result.Center = new Vector3();
                //result.HalfAxesX = hax;
                //result.HalfAxesY = isHeightZero ? Vector3.up : haz;
                //result.HalfAxesZ = hay;
                //result._BoundingSphere = result.BoundingSphere();

                //Vector3d ecefCenter = result.Center3d;
                //double[] center_llh = CoordinateConversion.ECEFToLLA(ecefCenter.x, ecefCenter.y, ecefCenter.z);
                //Vector3 position = CoordinateConversion.LatLonHeightToWorldPosition(center_llh[0], center_llh[1], center_llh[2]);
                //Vector3d ax = result.Center3d + result.HalfAxesX3d;
                //Vector3d ay = result.Center3d + result.HalfAxesY3d;
                //Vector3d az = result.Center3d + result.HalfAxesZ3d;
                //double[] ax_llh = CoordinateConversion.ECEFToLLA(ax.x, ax.y, ax.z);
                //double[] ay_llh = CoordinateConversion.ECEFToLLA(ay.x, ay.y, ay.z);
                //double[] az_llh = CoordinateConversion.ECEFToLLA(az.x, az.y, az.z);
                //Vector3 ax_position = CoordinateConversion.LatLonHeightToWorldPosition(ax_llh[0], ax_llh[1], ax_llh[2]);
                //Vector3 ay_position = CoordinateConversion.LatLonHeightToWorldPosition(ay_llh[0], ay_llh[1], ay_llh[2]);
                //Vector3 az_position = CoordinateConversion.LatLonHeightToWorldPosition(az_llh[0], az_llh[1], az_llh[2]);

                //Vector3 hax = ax_position - position;
                //Vector3 hay = ay_position - position;
                //Vector3 haz = az_position - position;

                //bool isHeightZero = halfAxesZ.ToVector3().magnitude < 1e-8;
                //result.Center = position;
                //result.HalfAxesX = hax;
                //result.HalfAxesY = isHeightZero ? Vector3.up : haz;
                //result.HalfAxesZ = hay;
                //result._BoundingSphere = result.BoundingSphere();
                //// if (isHeightZero) Debug.Log("Height Zero: " + Id);
                //boundsScale = new Vector3(hax.magnitude / (halfAxesX.ToVector3().magnitude * (float)CoordinateConversion.tileScale), isHeightZero ? 1 : haz.magnitude / (halfAxesZ.ToVector3().magnitude * (float)CoordinateConversion.tileScale), hay.magnitude / (halfAxesY.ToVector3().magnitude * (float)CoordinateConversion.tileScale));
                return result;
            }
            if (boundingVolume.Sphere.Count == 4)
            {
                var sphere = boundingVolume.Sphere;
                Vector3 center = new Vector3((float)sphere[0], (float)sphere[1], (float)sphere[2]);
                float radius = (float)sphere[3];
                var result = new TileBoundingSphere(center, radius);
                result.Transform(transform);
                return result;
            }
            if (boundingVolume.Region.Count == 6)
            {
                // TODO: Implement support for regions
                Debug.LogError("Regions not supported");
                return null;
            }
            Debug.LogError("boundingVolume must contain a box, sphere, or region");
            return null;
        }
    }
}
