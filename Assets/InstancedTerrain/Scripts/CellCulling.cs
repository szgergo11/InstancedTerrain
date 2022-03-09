using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CellCulling
{
    // TODO:
    // TODO PRIO: HIGH
    // Save this so it can be loaded on startup
    public TerrainManagerData CellInf;

    public ComputeBuffer cellDataComputeB;
    public ComputeBuffer intersectedCellIndexesComputeB;
    public ComputeBuffer fullyVisibleCellIndexesComputeB;
    public ComputeBuffer checkRangesCounts_Intersected_FullyVisible_ComputeB;

    
    public void ReinitializeBuffers()
    {
        DisposeBuffers();
        InitializeBuffers();
    }

    public void InitializeBuffers()
    {
        intersectedCellIndexesComputeB = new ComputeBuffer(CellInf.cellData.Count, sizeof(uint), ComputeBufferType.Append);
        fullyVisibleCellIndexesComputeB = new ComputeBuffer(CellInf.cellData.Count, sizeof(uint), ComputeBufferType.Append);

        checkRangesCounts_Intersected_FullyVisible_ComputeB = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.IndirectArguments);

        cellDataComputeB = new ComputeBuffer(CellInf.cellData.Count, sizeof(float) * 4);
        cellDataComputeB.SetData(CellInf.cellData);
    }

    public void DisposeBuffers()
    {
        if(intersectedCellIndexesComputeB != null)
        {
            intersectedCellIndexesComputeB.Release();
            intersectedCellIndexesComputeB = null;
        }

        if(fullyVisibleCellIndexesComputeB != null)
        {
            fullyVisibleCellIndexesComputeB.Release();
            fullyVisibleCellIndexesComputeB = null;
        }

        if(checkRangesCounts_Intersected_FullyVisible_ComputeB != null)
        {
            checkRangesCounts_Intersected_FullyVisible_ComputeB.Release();
            checkRangesCounts_Intersected_FullyVisible_ComputeB = null;
        }

        if(cellDataComputeB != null)
        {
            cellDataComputeB.Release();
            cellDataComputeB = null;
        }
    }
}
