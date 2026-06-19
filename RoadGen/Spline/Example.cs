using GeoDCBuildTools.RoadTool;
using System.Collections.Generic;
using UnityEngine;

public class Example : MonoBehaviour
{
    private void Start()
    {
        //#region ��·���
        //string geojson = "";
        //UnwrappedTileId tileId = new UnwrappedTileId(1, 1, 1);
        //// �����������ɵ�·
        //GenerateRoadResult generateRoadResult = SplineRoadLoader.GenerateRoadPrefab(geojson, tileId);
        //// ���������ɵ�·
        //Terrain terrain = new Terrain();
        //float terrainSize = terrain.terrainData.size.x;
        //generateRoadResult = SplineRoadLoader.GenerateRoadPrefab(geojson, tileId, terrainSize, terrain);

        //Mesh roadMesh = generateRoadResult.roadMesh;
        //GameObject roadObj = generateRoadResult.roadObj;
        //string roadFacilityInfo = generateRoadResult.roadFacilityInfo;

        //// ������·Ԫ��Json����
        //RoadFacilityData roadFacilityData = SplineRoadLoader.AnalysisRoadFacilityData(roadFacilityInfo);
        //List<FacilityPoint> facilities = roadFacilityData.facilityMarkPoints;
        //#endregion

        //#region ��·���
        //string geojson1 = "";
        //UnwrappedTileId tileId1 = new UnwrappedTileId(1, 1, 1);
        //// ��������������·
        //GenerateRailwayResult generateRailwayResult = SplineRailwayLoader.GenerateRailwayPrefab(geojson1, tileId1);
        //// ������������·
        //Terrain terrain1 = new Terrain();
        //float terrainSize1 = terrain.terrainData.size.x;
        //generateRailwayResult = SplineRailwayLoader.GenerateRailwayPrefab(geojson1, tileId1, terrainSize1, terrain1);
        //#endregion
    }
}
