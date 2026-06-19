using Unity.Mathematics;

namespace GeoToolkit.GeoJSON
{
    public class GeoJSONObject
    {

        public string type;

        public GeoJSONObject()
        {
        }

        public GeoJSONObject(JSONObject jsonObject)
        {
            if (jsonObject != null)
                type = jsonObject["type"].stringValue;
        }

        //Will always return a FeatureCollection...
        static public FeatureCollection Deserialize(string encodedString, GeojsonParameter geojsonParameter)
        {
            FeatureCollection collection;

            JSONObject jsonObject = new JSONObject(encodedString);
            if (jsonObject["type"].stringValue == "FeatureCollection")
            {
                collection = new FeatureCollection(jsonObject, geojsonParameter);
            }
            else
            {
                collection = new FeatureCollection();
                collection.features.Add(new FeatureObject(jsonObject, geojsonParameter));
            }

            return collection;
        }

        virtual public JSONObject Serialize()
        {

            JSONObject rootObject = new JSONObject(JSONObject.Type.Object);
            rootObject.AddField("type", type);

            SerializeContent(rootObject);

            return rootObject;
        }

        protected virtual void SerializeContent(JSONObject rootObject) { }
    }

    public class GeojsonParameter
    {
        public double2 centerLonLat;
        public bool isRaycastHight;
    }

}