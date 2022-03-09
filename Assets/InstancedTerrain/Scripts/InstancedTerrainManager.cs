using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class InstancedTerrainManager : MonoBehaviour
{
    public enum GizmoVisibilityMode
    {
        Always,
        OnPause,
        Never
    }

    public static InstancedTerrainManager instance;

    public GizmoVisibilityMode gizmoMode = GizmoVisibilityMode.Never;


    public Camera cam;

    public List<InstancedTerrainDrawer> instancedTerrainDrawers;
    public CellCulling cellCulling;


    private void Update()
    {
#if UNITY_EDITOR
        Validate();
#endif
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!
            (gizmoMode == GizmoVisibilityMode.Always ||
            (gizmoMode == GizmoVisibilityMode.OnPause && UnityEditor.EditorApplication.isPaused)))
            return;

        Validate();
        DrawCells();
#endif
    }
    
    private void OnEnable()
    {
        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        instance = this;


        Validate();
        cellCulling.InitializeBuffers();


        foreach (var itd in instancedTerrainDrawers)
        {
            itd.InitializeBuffers(cellCulling);
        }

    }

    private void OnDisable()
    {
        cellCulling.DisposeBuffers();

        foreach (var itd in instancedTerrainDrawers)
        {
            itd.DisposeBuffers();
        }


        instance = null;
    }


    public Vector4[] GetFrustumPlanes()
    {
        Vector4[] res = new Vector4[6];
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        for (int i = 0; i < 6; i++)
        {
            res[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }
        return res;
    }


    /// <summary>
    /// Warning: Very expensive method
    /// </summary>
    private void DrawCells()
    {
        if (cellCulling.CellInf.CellCount == 0)
            return;

        uint[] intersected = new uint[cellCulling.intersectedCellIndexesComputeB.count];
        uint[] fullyVisible = new uint[cellCulling.fullyVisibleCellIndexesComputeB.count];
        cellCulling.intersectedCellIndexesComputeB.GetData(intersected);
        cellCulling.fullyVisibleCellIndexesComputeB.GetData(fullyVisible);

        ComputeBuffer drawCellsComputeB = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.IndirectArguments);
        ComputeBuffer.CopyCount(cellCulling.intersectedCellIndexesComputeB, drawCellsComputeB, 0);
        ComputeBuffer.CopyCount(cellCulling.fullyVisibleCellIndexesComputeB, drawCellsComputeB, 4);
        uint[] counts = new uint[2];
        drawCellsComputeB.GetData(counts);
        drawCellsComputeB.Release();

        Gizmos.color = Color.red;
        for (int i = 0; i < counts[0]; i++)
        {
            Gizmos.DrawWireCube(
                cellCulling.CellInf.cellData[(int)intersected[i]],
                new Vector3(cellCulling.CellInf.cellSize.x, cellCulling.CellInf.cellData[(int)intersected[i]].w * 2f, cellCulling.CellInf.cellSize.y));
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < counts[1]; i++)
        {
            Gizmos.DrawWireCube(
                cellCulling.CellInf.cellData[(int)fullyVisible[i]],
                new Vector3(cellCulling.CellInf.cellSize.x, cellCulling.CellInf.cellData[(int)fullyVisible[i]].w * 2f, cellCulling.CellInf.cellSize.y));
        }
    }

    private void Validate()
    {
        cellCulling.CellInf.changedCellSize = false;

        bool generate = false;
        foreach (var itd in instancedTerrainDrawers)
        {
            if (itd.terrainElementData.CellSize != cellCulling.CellInf.cellSize)
            {
                Debug.Log("Terrain drawer's cellsize is not matching manager's cell size. Reordering in progress...", itd.terrainElementData);
                itd.terrainElementData.Reorder(cellCulling.CellInf.cellSize);
                generate = true;
            }
            
            if (itd.terrainElementData.changedCellStructure)
            {
                itd.terrainElementData.changedCellStructure = false;
                generate = true;
            }
        }

        if (generate)
        {
            Debug.Log("Generating cell data...");
            cellCulling.CellInf.GenerateCellData(instancedTerrainDrawers);
            cellCulling.ReinitializeBuffers();
            foreach (var itd in instancedTerrainDrawers)
            {
                itd.ReinitializeBuffers(cellCulling);
            }
        }

        
    }

}
