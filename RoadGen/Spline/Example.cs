using GeoDCBuildTools.RoadTool;
using System.Collections.Generic;
using UnityEngine;

public class Example : MonoBehaviour
{
    private void Start()
    {
        //#region Road generation
        //string geojson = "";
        //UnwrappedTileId tileId = new UnwrappedTileId(1, 1, 1);
        //// Generate road mesh without terrain fitting.
        //GenerateRoadResult generateRoadResult = SplineRoadLoader.GenerateRoadPrefab(geojson, tileId);
        //// Generate road mesh fitted to terrain.
        //Terrain terrain = new Terrain();
        //float terrainSize = terrain.terrainData.size.x;
        //generateRoadResult = SplineRoadLoader.GenerateRoadPrefab(geojson, tileId, terrainSize, terrain);

        //Mesh roadMesh = generateRoadResult.roadMesh;
        //GameObject roadObj = generateRoadResult.roadObj;
        //string roadFacilityInfo = generateRoadResult.roadFacilityInfo;

        //// Parse generated road facility metadata.
        //RoadFacilityData roadFacilityData = SplineRoadLoader.AnalysisRoadFacilityData(roadFacilityInfo);
        //List<FacilityPoint> facilities = roadFacilityData.facilityMarkPoints;
        //#endregion

        //#region Railway generation
        //string geojson1 = "";
        //UnwrappedTileId tileId1 = new UnwrappedTileId(1, 1, 1);
        //// Generate railway mesh without terrain fitting.
        //GenerateRailwayResult generateRailwayResult = SplineRailwayLoader.GenerateRailwayPrefab(geojson1, tileId1);
        //// Generate railway mesh fitted to terrain.
        //Terrain terrain1 = new Terrain();
        //float terrainSize1 = terrain.terrainData.size.x;
        //generateRailwayResult = SplineRailwayLoader.GenerateRailwayPrefab(geojson1, tileId1, terrainSize1, terrain1);
        //#endregion
    }
}
