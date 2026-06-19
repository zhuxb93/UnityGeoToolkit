using System.Collections.Generic;

namespace GeoToolkit.GeoJSON
{

    [System.Serializable]
    public class FeatureCollection : GeoJSONObject
    {

        public List<FeatureObject> features;

        public FeatureCollection(string encodedString, GeojsonParameter geojsonParameter)
        {
            features = new List<FeatureObject>();

            JSONObject jsonObject = new JSONObject(encodedString);

            ParseFeatures(jsonObject["features"], geojsonParameter);
            type = "FeatureCollection";
        }

        public FeatureCollection(JSONObject jsonObject, GeojsonParameter geojsonParameter) : base(jsonObject)
        {
            features = new List<FeatureObject>();

            ParseFeatures(jsonObject["features"], geojsonParameter);
        }

        public FeatureCollection()
        {
            features = new List<FeatureObject>();
            type = "FeatureCollection";
        }

        protected void ParseFeatures(JSONObject jsonObject, GeojsonParameter geojsonParameter)
        {
            foreach (JSONObject featureObject in jsonObject.list)
            {
                features.Add(new FeatureObject(featureObject, geojsonParameter));
            }
        }

        override protected void SerializeContent(JSONObject rootObject)
        {

            JSONObject jsonFeatures = new JSONObject(JSONObject.Type.Array);
            foreach (FeatureObject feature in features)
            {
                jsonFeatures.Add(feature.Serialize());
            }
            rootObject.AddField("features", jsonFeatures);
        }

    }
}
