using System.Collections.Generic;

namespace GeoToolkit.GeoJSON
{

    [System.Serializable]
    public class FeatureObject
    {

        public string type;
        public GeometryObject geometry;
        public Dictionary<string, string> properties;
        public List<float> VectorZ;
        public string id;

        public FeatureObject(JSONObject jsonObject, GeojsonParameter geojsonParameter)
        {
            type = jsonObject["type"].stringValue;
            id = jsonObject["id"] == null ? "-1" : jsonObject["id"].stringValue;
            geometry = parseGeometry(jsonObject["geometry"], geojsonParameter);

            properties = new Dictionary<string, string>();
            VectorZ = new List<float>();
            parseProperties(jsonObject["properties"]);
        }
        public FeatureObject(string encodedString, GeojsonParameter geojsonParameter)
        {
            JSONObject jsonObject = new JSONObject(encodedString);
            type = jsonObject["type"].stringValue;
            id = jsonObject["id"].stringValue;
            geometry = parseGeometry(jsonObject["geometry"], geojsonParameter);

            properties = new Dictionary<string, string>();
            VectorZ = new List<float>();
            parseProperties(jsonObject["properties"]);
        }

        public FeatureObject(GeometryObject featureGeometry)
        {
            type = "Feature";
            geometry = featureGeometry;

            properties = new Dictionary<string, string>();
        }

        protected void parseProperties(JSONObject jsonObject)
        {
            if (jsonObject == null || jsonObject.list == null) return;
            for (int i = 0; i < jsonObject.list.Count; i++)
            {
                if (jsonObject.keys == null) continue;
                string key = (string)jsonObject.keys[i];
                JSONObject value = (JSONObject)jsonObject.list[i];
                if (value.isString)
                    properties.Add(key, value.stringValue);
                if (value.isNumber)
                    properties.Add(key, value.doubleValue.ToString());
                if (value.isArray)
                {
                    getHeight(ref VectorZ, value);
                }


            }
        }

        private void getHeight(ref List<float> heightArray, JSONObject jsonProperty)
        {
            foreach (var l in jsonProperty)
            {
                if (l.count > 0)
                {
                    getHeight(ref heightArray, l);
                }
                else
                {
                    heightArray.Add(l.floatValue);
                }
            }
        }

        protected GeometryObject parseGeometry(JSONObject jsonObject, GeojsonParameter geojsonParameter)
        {
            if (jsonObject["type"] == null)
            {
                return null;
            }
            switch (jsonObject["type"].stringValue)
            {
                case "Point":
                    return new PointGeometryObject(jsonObject, geojsonParameter);
                case "MultiPoint":
                    return new MultiPointGeometryObject(jsonObject, geojsonParameter);
                case "LineString":
                    return new LineStringGeometryObject(jsonObject, geojsonParameter);
                case "MultiLineString":
                    return new MultiLineStringGeometryObject(jsonObject, geojsonParameter);
                case "Polygon":
                    return new PolygonGeometryObject(jsonObject, geojsonParameter);
                case "MultiPolygon":
                    return new MultiPolygonGeometryObject(jsonObject, geojsonParameter);
                default:
                    break;
            }
            return null;
        }

        public JSONObject Serialize()
        {
            JSONObject rootObject = new JSONObject(JSONObject.Type.Object);

            rootObject.AddField("type", type);

            //Geometry
            JSONObject geometryObject = geometry.Serialize();
            rootObject.AddField("geometry", geometryObject);

            //Properties
            JSONObject jsonProperties = new JSONObject(JSONObject.Type.Object);
            foreach (KeyValuePair<string, string> property in properties)
            {
                jsonProperties.AddField(property.Key, property.Value);
            }
            rootObject.AddField("properties", jsonProperties);

            return rootObject;
        }
    }
}
