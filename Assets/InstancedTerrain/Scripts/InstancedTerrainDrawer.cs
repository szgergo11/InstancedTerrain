using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class InstancedTerrainDrawer
{
    public ComputeBuffer cellStartIndexesComputeBuffer;
    public ComputeBuffer cellCountsComputeBuffer;
    public ComputeBuffer intersectedFrustumCheckRangesComputeBuffer;
    public ComputeBuffer fullyVisibleFrustumCheckRangesComputeBuffer;

    public ComputeBuffer intersectedFrustumResultComputeBuffer;
    public ComputeBuffer terrainElementMatricesInCellOrderFlatComputeBuffer;

    public ComputeBuffer hizFrustumResultCountComputeBuffer;
    public ComputeBuffer hizResultComputeBuffer;

    public ComputeBuffer argsBuffer;

    public ComputeBuffer dispatchArgsBuffer0;
    public ComputeBuffer dispatchArgsBuffer1;
    public ComputeBuffer dispatchArgsBuffer2;

    public TerrainElementData terrainElementData;

    public int TePositionCount { get; private set; }

    // NOTE:
    // LOD majd úgy lesz, hogy a depth cullingnál lod szintek szerint rakosgatja az indexeket külön bufferekbe (minden lodnak 1 buffer)

    public void ReinitializeBuffers(CellCulling cellCullingData)
    {
        DisposeBuffers();
        InitializeBuffers(cellCullingData);
    }

    public void SetMaterialBuffers()
    {
        //Debug.Log(terrainElementData.material.HasBuffer("_TEPositions"));
        //Debug.Log(terrainElementData.material.HasBuffer("_CullingResultIdxs"));
        terrainElementData.material.SetBuffer("_TEMatrices", terrainElementMatricesInCellOrderFlatComputeBuffer);
        terrainElementData.material.SetBuffer("_CullingResultIdxs", hizResultComputeBuffer);
    }


    public void InitializeBuffers(CellCulling cellCullingData)
    {
        TePositionCount = terrainElementData.teMatricesCellOrder.Sum(x => x.Value.matrices.Count);

        cellStartIndexesComputeBuffer = new ComputeBuffer(cellCullingData.CellInf.cellData.Count, sizeof(uint));
        cellCountsComputeBuffer = new ComputeBuffer(cellCullingData.CellInf.cellData.Count, sizeof(uint));

        intersectedFrustumCheckRangesComputeBuffer = new ComputeBuffer(cellCullingData.CellInf.cellData.Count, sizeof(uint) * 2, ComputeBufferType.Append);
        fullyVisibleFrustumCheckRangesComputeBuffer = new ComputeBuffer(cellCullingData.CellInf.cellData.Count, sizeof(uint) * 2, ComputeBufferType.Append);

        intersectedFrustumResultComputeBuffer = new ComputeBuffer(TePositionCount, sizeof(uint), ComputeBufferType.Append);
        terrainElementMatricesInCellOrderFlatComputeBuffer = new ComputeBuffer(TePositionCount, sizeof(float) * 4 * 4);

        hizFrustumResultCountComputeBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
        hizResultComputeBuffer = new ComputeBuffer(TePositionCount, sizeof(uint), ComputeBufferType.Append);

        uint[] cellCounts = new uint[cellCullingData.CellInf.cellOrder.Count];
        uint[] cellStartIndexes = new uint[cellCullingData.CellInf.cellOrder.Count];
        uint cellStartIdx = 0;
        int counter = 0;
        for (int i = 0; i < cellCullingData.CellInf.cellOrder.Count; i++)
        {
            var cellKey = cellCullingData.CellInf.cellOrder[i];

            if(terrainElementData.teMatricesCellOrder.ContainsKey(cellKey))
            {
                var value = terrainElementData.teMatricesCellOrder[cellKey];

                // TE positions
                terrainElementMatricesInCellOrderFlatComputeBuffer.SetData(value.matrices, 0, counter, value.matrices.Count);

                counter += value.matrices.Count;

                // Cell counts
                cellCounts[i] = (uint)value.matrices.Count;

                // Cell starts
                cellStartIndexes[i] = cellStartIdx;

                

                cellStartIdx += cellCounts[i];
            }
            else
            {
                // Cell counts
                cellCounts[i] = 0;

                // Cell starts
                if (i == 0)
                    cellStartIndexes[i] = 0;
                else
                    cellStartIndexes[i] = cellStartIndexes[i - 1];
            }
        }

        cellStartIndexesComputeBuffer.SetData(cellStartIndexes);
        cellCountsComputeBuffer.SetData(cellCounts);

        // TODO:
        // TODO PRIO: HIGH
        // Optimize allocations (make result buffers sizes more dynamic)

        uint[] args = new uint[5];
        args[0] = terrainElementData.renderMesh.GetIndexCount(0);
        args[1] = 0;    // Instance count (Set when drawing)
        args[2] = terrainElementData.renderMesh.GetIndexStart(0);
        args[3] = terrainElementData.renderMesh.GetBaseVertex(0);
        args[4] = 0;    // Start index (0 always)
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        dispatchArgsBuffer0 = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        dispatchArgsBuffer1 = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        dispatchArgsBuffer2 = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

        uint[] csArgs = new uint[] { 0, 1, 1, 0 };
        dispatchArgsBuffer0.SetData(csArgs);
        dispatchArgsBuffer1.SetData(csArgs);
        dispatchArgsBuffer2.SetData(csArgs);

        SetMaterialBuffers();
    }

    public void DisposeBuffers()
    {
        if(cellStartIndexesComputeBuffer != null)
        {
            cellStartIndexesComputeBuffer.Release();
            cellStartIndexesComputeBuffer = null;
        }
        if (intersectedFrustumCheckRangesComputeBuffer != null)
        {
            intersectedFrustumCheckRangesComputeBuffer.Release();
            intersectedFrustumCheckRangesComputeBuffer = null;
        }
        if (cellCountsComputeBuffer != null)
        {
            cellCountsComputeBuffer.Release();
            cellCountsComputeBuffer = null;
        }
        if (fullyVisibleFrustumCheckRangesComputeBuffer != null)
        {
            fullyVisibleFrustumCheckRangesComputeBuffer.Release();
            fullyVisibleFrustumCheckRangesComputeBuffer = null;
        }
        if (intersectedFrustumResultComputeBuffer != null)
        {
            intersectedFrustumResultComputeBuffer.Release();
            intersectedFrustumResultComputeBuffer = null;
        }
        if (terrainElementMatricesInCellOrderFlatComputeBuffer != null)
        {
            terrainElementMatricesInCellOrderFlatComputeBuffer.Release();
            terrainElementMatricesInCellOrderFlatComputeBuffer = null;
        }
        if (hizFrustumResultCountComputeBuffer != null)
        {
            hizFrustumResultCountComputeBuffer.Release();
            hizFrustumResultCountComputeBuffer = null;
        }
        if (hizResultComputeBuffer != null)
        {
            hizResultComputeBuffer.Release();
            hizResultComputeBuffer = null;
        }
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
        if (dispatchArgsBuffer0 != null)
        {
            dispatchArgsBuffer0.Release();
            dispatchArgsBuffer0 = null;
        }
        if (dispatchArgsBuffer1 != null)
        {
            dispatchArgsBuffer1.Release();
            dispatchArgsBuffer1 = null;
        }
        if (dispatchArgsBuffer2 != null)
        {
            dispatchArgsBuffer2.Release();
            dispatchArgsBuffer2 = null;
        }
    }
}
