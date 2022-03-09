using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "CellsInfo", menuName = "Instanced Terrain/Cell Culling/Cells Info")]
public class TerrainManagerData : ScriptableObject
{
    /// <summary>
    /// Cell centers, extent in y dir (x z cellsize is same)
    /// </summary>
    public List<Vector4> cellData = new List<Vector4>();
    public List<int2> cellOrder = new List<int2>();
    public Vector2 maxTeSizeXZ;

    [Min(0.5f)] public Vector2 cellSize;
    [HideInInspector] private Vector2 lastCellSize;

    public bool changedCellSize = false;

    public int CellCount
    {
        get => cellOrder.Count;
    }

    public TerrainManagerData()
    {

    }

    private void OnValidate()
    {
        if(lastCellSize != cellSize)
        {
            lastCellSize = cellSize;
            changedCellSize = true;
        }

    }

    // Lesz egy ilyen gomb
    public void GenerateCellData(List<InstancedTerrainDrawer> itds)
    {
        cellData.Clear();
        cellOrder.Clear();
        maxTeSizeXZ = Vector2.zero;
        Dictionary<int2, float2> helperDict = new Dictionary<int2, float2>();
        // Using dictionary is faster
        foreach (var itd in itds)
        {
            var teSize = itd.terrainElementData.boundingBoxSize * itd.terrainElementData.boundingBoxSizeMultiplier;
            if (maxTeSizeXZ.x < teSize.x)
                maxTeSizeXZ.x = teSize.x;
            if (maxTeSizeXZ.y < teSize.y)
                maxTeSizeXZ.y = teSize.y;

            foreach (var lc in itd.terrainElementData.teMatricesCellOrder)
            {
                var key = lc.Key;
                var val = lc.Value;
                if (helperDict.ContainsKey(key))
                {
                    // Choose the min/max
                    if(helperDict[key].y < val.yMax)
                        helperDict[key] = new float2(helperDict[key].x, val.yMax);

                    if (helperDict[key].x > val.yMin)
                        helperDict[key] = new float2(val.yMin, helperDict[key].x);
                }
                else
                {
                    helperDict.Add(key, new float2(val.yMin, val.yMax));
                }
            }
        }

        
        foreach (var pair in helperDict)
        {
            var k = pair.Key;
            var v = pair.Value;
            Vector4 centerExtent = new Vector4(
                (k.x + 0.5f) * cellSize.x,
                v.x + (v.y - v.x) / 2f,
                (k.y + 0.5f) * cellSize.y,
                (v.y - v.x) / 2f
                );

            cellData.Add(centerExtent);
            cellOrder.Add(k);
        }
    }
}
