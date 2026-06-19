using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace GeoToolkit.GeoJSON
{

    [System.Serializable]
    public class GeometryObject : GeoJSONObject
    {

        public GeometryObject() : base()
        {
        }

        public GeometryObject(JSONObject jsonObject) : base(jsonObject)
        {
        }

        /*
		 * Returns all PositionObjects in the Geometry as a single list
		 */
        virtual public List<Vector3> AllPositions()
        {
            return null;
        }

        /*
		 * Returns first PositionObject in the Geometry
		 */
        virtual public Vector3 FirstPosition()
        {
            return Vector3.zero;
        }

        /*
		 * Returns the number of all PositionObjects in the Geometry
		 */
        virtual public int PositionCount()
        {
            return 0;
        }

        override protected void SerializeContent(JSONObject rootObject)
        {
            JSONObject coordinateObject = SerializeGeometry();
            rootObject.AddField("coordinates", coordinateObject);
        }

        virtual protected JSONObject SerializeGeometry() { return null; }
    }

    [System.Serializable]
    public class SingleGeometryObject : GeometryObject
    {
        public Vector3 coordinates;

        public double3 lonlatheight;

        public SingleGeometryObject() : base()
        {
            type = "Point";
            coordinates = Vector3.zero;
            lonlatheight = double3.zero;
        }

        public SingleGeometryObject(double longitude, double latitude, GeojsonParameter geojsonParameter) : base()
        {
            type = "Point";
            lonlatheight = new double3(longitude, latitude, 0);
            Vector3 worldPosition = GeoCoordinateUtils.LatLonToWorld(lonlatheight);
            coordinates = worldPosition;
        }

        public SingleGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject)
        {
            var longitude = jsonObject["coordinates"].list[0].doubleValue;
            var latitude = jsonObject["coordinates"].list[1].doubleValue;
            var height = jsonObject["coordinates"].list.Count > 2 ? jsonObject["coordinates"].list[2].floatValue : 0;

            var center = geojsonParameter == null ? double2.zero : geojsonParameter.centerLonLat;
            var isRaycastHigh = geojsonParameter == null ? false : geojsonParameter.isRaycastHight;
            coordinates = GeoCoordinateUtils.GetWorldPositionWithRaycast(center, new double3(longitude, latitude, height), isRaycastHigh);
            //var worldPosition = CesiumConversions.Instance.ConvertLongitudeLatitudeHeightToWorldPosition(longitude, latitude, ref meshCreateInfo.ecefToLocalMatrix) - meshCreateInfo.TileCenterUnity;

            lonlatheight = new double3(longitude, latitude, height);
        }

        override public List<Vector3> AllPositions()
        {
            List<Vector3> list = new List<Vector3>();
            list.Add(coordinates);
            return list;
        }

        override public Vector3 FirstPosition()
        {
            return coordinates;
        }

        override public int PositionCount()
        {
            return 1;
        }
    }
    [System.Serializable]
    public class ArrayGeometryObject : GeometryObject
    {
        public List<Vector3> coordinates;
        public List<double3> lonlatheights;
        public float maxMeshZ;
        public float minMeshZ;

        public ArrayGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject)
        {
            var coordinatesList = jsonObject["coordinates"].list;
            if (coordinatesList == null) return;
            coordinates = new List<Vector3>();
            lonlatheights = new List<double3>();
            if (maxMeshZ == 0)
            {
                maxMeshZ = coordinatesList.Where(t => t.list.Count > 2).Select(t => t.list[2].floatValue).DefaultIfEmpty(0).Max();
                minMeshZ = coordinatesList.Where(t => t.list.Count > 2).Select(t => t.list[2].floatValue).DefaultIfEmpty(0).Min();
            }

            foreach (JSONObject j in coordinatesList)
            {
                var longitude = j.list[0].doubleValue;
                var latitude = j.list[1].doubleValue;
                var height = j.list.Count > 2 ? j.list[2].floatValue : 0;
                var center = geojsonParameter == null ? double2.zero : geojsonParameter.centerLonLat;
                var isRaycastHigh = geojsonParameter == null ? false : geojsonParameter.isRaycastHight;
                Vector3 worldPosition = GeoCoordinateUtils.GetWorldPositionWithRaycast(center, new double3(longitude, latitude, height), isRaycastHigh);
                //var worldPosition = CesiumConversions.Instance.ConvertLongitudeLatitudeHeightToWorldPosition(longitude, latitude, ref meshCreateInfo.ecefToLocalMatrix) - meshCreateInfo.TileCenterUnity;

                lonlatheights.Add(new double3(longitude, latitude, height));
                coordinates.Add(worldPosition);
            }

        }

        override public List<Vector3> AllPositions()
        {
            return coordinates;
        }

        override public Vector3 FirstPosition()
        {
            if (coordinates.Count > 0)
                return coordinates[0];

            return Vector3.zero;
        }

        override public int PositionCount()
        {
            return coordinates.Count;
        }


    }
    [System.Serializable]
    public class ArrayArrayGeometryObject : GeometryObject
    {
        public List<List<Vector3>> coordinates;
        public List<List<double3>> lonlatheights;
        public float maxMeshZ = 0;
        public float minMeshZ = 0;

        public ArrayArrayGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject)
        {
            coordinates = new List<List<Vector3>>();
            lonlatheights = new List<List<double3>>();
            foreach (JSONObject l in jsonObject["coordinates"].list)
            {
                List<Vector3> polygon = new List<Vector3>();
                List<double3> lonlat1 = new List<double3>();
                coordinates.Add(polygon);
                lonlatheights.Add(lonlat1);
                if (l.list == null) continue;

                if (maxMeshZ == 0)
                {
                    maxMeshZ = l.list.Where(t => t.list.Count > 2).Select(t => t.list[2].floatValue).DefaultIfEmpty(0).Max();
                    minMeshZ = l.list.Where(t => t.list.Count > 2).Select(t => t.list[2].floatValue).DefaultIfEmpty(0).Min();
                }

                foreach (JSONObject l2 in l.list)
                {
                    var longitude = l2.list[0].doubleValue;
                    var latitude = l2.list[1].doubleValue;
                    var height = l2.list.Count > 2 ? l2.list[2].floatValue : 0;

                    var center = geojsonParameter == null ? double2.zero : geojsonParameter.centerLonLat;
                    var isRaycastHigh = geojsonParameter == null ? false : geojsonParameter.isRaycastHight;
                    Vector3 worldPosition = GeoCoordinateUtils.GetWorldPositionWithRaycast(center, new double3(longitude, latitude, height), isRaycastHigh);
                    //var worldPosition = CesiumConversions.Instance.ConvertLongitudeLatitudeHeightToWorldPosition(longitude, latitude, ref meshCreateInfo.ecefToLocalMatrix) - meshCreateInfo.TileCenterUnity;
                    polygon.Add(worldPosition);
                    lonlat1.Add(new double3(longitude, latitude, height));
                }
            }

        }

        override public List<Vector3> AllPositions()
        {
            List<Vector3> list = new List<Vector3>();
            foreach (List<Vector3> l in coordinates)
            {
                foreach (Vector3 pos in l)
                {
                    list.Add(pos);
                }
            }
            return list;
        }

        override public Vector3 FirstPosition()
        {
            if (coordinates.Count > 0 && coordinates[0].Count > 0)
                return coordinates[0][0];

            return Vector3.zero;
        }

        override public int PositionCount()
        {
            int totalPositions = 0;

            foreach (List<Vector3> l in coordinates)
            {
                totalPositions += coordinates.Count;
            }

            return totalPositions;
        }


    }


    [System.Serializable]
    public class ArrayArrayArrayGeometryObject : GeometryObject
    {
        public List<List<List<Vector3>>> coordinates;
        public List<List<List<double3>>> lonlatheights;
        public float maxMeshZ = 0;
        public float minMeshZ = 0;

        public ArrayArrayArrayGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject)
        {
            lonlatheights = new List<List<List<double3>>>();
            coordinates = new List<List<List<Vector3>>>();
            maxMeshZ = float.MinValue;
            minMeshZ = float.MaxValue;

            foreach (JSONObject l in jsonObject["coordinates"].list)
            {
                List<List<double3>> lonlat1 = new List<List<double3>>();
                List<List<Vector3>> polygonGroup = new List<List<Vector3>>();

                if (l.list == null) continue;

                foreach (JSONObject l2 in l.list)
                {
                    List<Vector3> polygonchild = new List<Vector3>();
                    List<double3> lonlat2 = new List<double3>();

                    foreach (JSONObject l3 in l2.list)
                    {
                        var longitude = l3.list[0].doubleValue;
                        var latitude = l3.list[1].doubleValue;
                        var height = l3.list.Count > 2 ? l3.list[2].floatValue : 0f;

                        // 更新最大、最小高度
                        if (height > maxMeshZ)
                            maxMeshZ = height;
                        if (height < minMeshZ)
                            minMeshZ = height;

                        var center = geojsonParameter == null ? double2.zero : geojsonParameter.centerLonLat;
                        var isRaycastHigh = geojsonParameter == null ? false : geojsonParameter.isRaycastHight;
                        Vector3 worldPosition = GeoCoordinateUtils.GetWorldPositionWithRaycast(center, new double3(longitude, latitude, height), isRaycastHigh);

                        polygonchild.Add(worldPosition);
                        lonlat2.Add(new double3(longitude, latitude, height));
                    }

                    polygonGroup.Add(polygonchild);
                    lonlat1.Add(lonlat2);
                }

                coordinates.Add(polygonGroup);
                lonlatheights.Add(lonlat1);
            }

            // 防止无效赋值，保证最小最大值可用
            if (maxMeshZ == float.MinValue) maxMeshZ = 0f;
            if (minMeshZ == float.MaxValue) minMeshZ = 0f;
        }

        override public List<Vector3> AllPositions()
        {
            List<Vector3> list = new List<Vector3>();
            foreach (var polygonGroup in coordinates)
            {
                foreach (var polygon in polygonGroup)
                {
                    list.AddRange(polygon);
                }
            }
            return list;
        }

        override public Vector3 FirstPosition()
        {
            if (coordinates.Count > 0 &&
                coordinates[0].Count > 0 &&
                coordinates[0][0].Count > 0)
            {
                return coordinates[0][0][0];
            }

            return Vector3.zero;
        }

        override public int PositionCount()
        {
            int totalPositions = 0;
            foreach (var polygonGroup in coordinates)
            {
                foreach (var polygon in polygonGroup)
                {
                    totalPositions += polygon.Count;
                }
            }
            return totalPositions;
        }
    }

    [System.Serializable]
    public class PointGeometryObject : SingleGeometryObject
    {
        public PointGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
        public PointGeometryObject(float longitude, float latitude, GeojsonParameter geojsonParameter) : base(longitude, latitude, geojsonParameter)
        {
        }
    }
    [System.Serializable]
    public class MultiPointGeometryObject : ArrayGeometryObject
    {
        public MultiPointGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
    }

    [System.Serializable]
    public class LineStringGeometryObject : ArrayGeometryObject
    {
        public LineStringGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
    }
    [System.Serializable]
    public class MultiLineStringGeometryObject : ArrayArrayGeometryObject
    {
        public MultiLineStringGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
    }

    [System.Serializable]
    public class PolygonGeometryObject : ArrayArrayGeometryObject
    {
        public PolygonGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
    }
    [System.Serializable]
    public class MultiPolygonGeometryObject : ArrayArrayArrayGeometryObject
    {
        public MultiPolygonGeometryObject(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject, geojsonParameter)
        {
        }
    }
}
