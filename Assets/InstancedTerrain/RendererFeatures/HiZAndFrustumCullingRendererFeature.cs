using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

public class HiZAndFrustumCullingRendererFeature : ScriptableRendererFeature
{

    private class CustomRenderPass : ScriptableRenderPass
    {
        private const int MAX_TEXTURE_SIZE = 2048;

        private int _HiZBufferTextureID;
        private RenderTargetIdentifier _HiZBufferTexture_RTI;

        //private int rt2ID = Shader.PropertyToID("_TerrainLF_DEPTH");
        //private RenderTargetIdentifier rti2;

        private ScriptableRenderer renderer;
        private RenderTargetIdentifier _Source_RTI;

        private Material hizBufferMat;
        private int[] temporaryIDs = new int[0];
        private int textSize;
        private int mipCount;

        private int hiz_KernelID;
        private int intersected_KernelID;
        private int checkRanges_KernelID;
        private int cellFrustum_KernelID;
        private int fullyVisible_KernelID;
        private int clearArgs_KernelID;
        private int divideArgs_KernelID;

        private ComputeShader hizCullingCS;
        private ComputeShader cellFrustumCullingCS;
        private ComputeShader createCheckRangesCS;
        private ComputeShader intersectedCS;
        private ComputeShader fullyVisibleCS;
        private ComputeShader clearArgsCS;
        private ComputeShader divideArgsCS;


        public CustomRenderPass(Material material, ComputeShader hizCullingComputeShader, ComputeShader cellFrustumCullingComputeShader, ComputeShader createCheckRangesComputeShader, ComputeShader fullyVisibleCellsComputeShader, ComputeShader intersectedCellsComputeShader, ComputeShader clearArgsBuffersComputeShader, ComputeShader divideArgsBuffersComputeShader) : base()
        {
            hizBufferMat = material;
            _HiZBufferTextureID = Shader.PropertyToID("_HiZBuffer");
            _HiZBufferTexture_RTI = new RenderTargetIdentifier(_HiZBufferTextureID);

            hizCullingCS = hizCullingComputeShader;
            cellFrustumCullingCS = cellFrustumCullingComputeShader;
            createCheckRangesCS = createCheckRangesComputeShader;
            intersectedCS = intersectedCellsComputeShader;
            fullyVisibleCS = fullyVisibleCellsComputeShader;
            clearArgsCS = clearArgsBuffersComputeShader;
            divideArgsCS = divideArgsBuffersComputeShader;

            hiz_KernelID = hizCullingCS.FindKernel("CSMain");
            cellFrustum_KernelID = cellFrustumCullingCS.FindKernel("CSMain");
            checkRanges_KernelID = createCheckRangesCS.FindKernel("CSMain");
            intersected_KernelID = intersectedCS.FindKernel("CSMain");
            fullyVisible_KernelID = fullyVisibleCS.FindKernel("CSMain");
            clearArgs_KernelID = clearArgsCS.FindKernel("CSMain");
            divideArgs_KernelID = divideArgsCS.FindKernel("CSMain");
        }

        public void SetSource(ScriptableRenderer source)
        {
            renderer = source;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);

			InstancedTerrainManager itm = InstancedTerrainManager.instance;
            if (itm == null || itm.cellCulling.CellInf.cellData.Count == 0)
                return;
			
            _Source_RTI = renderer.cameraColorTarget;

            textSize = Mathf.Min(
                (int)Mathf.Pow(2f, Mathf.CeilToInt(Mathf.Log(Mathf.Max(cameraTextureDescriptor.width, cameraTextureDescriptor.height), 2f))), 
                MAX_TEXTURE_SIZE);
            mipCount = (int)Mathf.Floor(Mathf.Log(textSize, 2f));
            cmd.GetTemporaryRT(_HiZBufferTextureID, new RenderTextureDescriptor(textSize, textSize, RenderTextureFormat.RGHalf, 0, mipCount + 1)
            {
                useMipMap = true,
                autoGenerateMips = false
            });
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // In edit mode: cull for both the game and scene camera separately
            // In play mode: cull for only the game camera
            if (
                (Application.isPlaying && (renderingData.cameraData.cameraType != CameraType.Game))
                || (!Application.isPlaying && (renderingData.cameraData.cameraType & (CameraType.Game | CameraType.SceneView)) == 0)
                )
                return;

            Camera currentCam = renderingData.cameraData.camera;
            var frustPlanes = InstancedTerrainManager.GetFrustumPlanes(currentCam);

            InstancedTerrainManager itm = InstancedTerrainManager.instance;
            if (itm == null || itm.cellCulling.CellInf.cellData.Count == 0)
                return;

            CommandBuffer ccBuffer = CommandBufferPool.Get("InstancedTerrain_CULL");

            #region HiZ Depth Buffer Set

            int tmpSize = textSize;
            temporaryIDs = new int[mipCount];

            // Mip level 0 is getting the depth values from the screen to mip level 0 (blit pass -> get max resolution depth information)
            Blit(ccBuffer, _Source_RTI, _HiZBufferTexture_RTI, hizBufferMat, 0);

            for (int i = 0; i < mipCount; i++)
            {
                temporaryIDs[i] = Shader.PropertyToID("_HiZBufferTemporaries" + i.ToString());
                tmpSize >>= 1;
                // Create temporary rt for current level
                ccBuffer.GetTemporaryRT(temporaryIDs[i], tmpSize, tmpSize, 0, FilterMode.Point, RenderTextureFormat.RGHalf);

                if (i == 0)
                    // Reduce pass -> reduce previous to half resolution
                    Blit(ccBuffer, _HiZBufferTexture_RTI, temporaryIDs[i], hizBufferMat, 1);
                else
                    // Reduce pass -> reduce previous to half resolution
                    Blit(ccBuffer, temporaryIDs[i - 1], temporaryIDs[i], hizBufferMat, 1);

                ccBuffer.CopyTexture(temporaryIDs[i], 0, 0, _HiZBufferTexture_RTI, 0, i + 1);

            }

            ccBuffer.SetGlobalTexture(_HiZBufferTextureID, new RenderTargetIdentifier(_HiZBufferTextureID));

            #endregion


            // Cells are shader among itds
            #region Cell Frustum Culling
            ccBuffer.SetComputeVectorParam(cellFrustumCullingCS, "_CellSize_MaxTeSizeXZ", new Vector4(itm.cellCulling.CellInf.cellSize.x, itm.cellCulling.CellInf.cellSize.z, itm.cellCulling.CellInf.maxTeSizeXZ.x, itm.cellCulling.CellInf.maxTeSizeXZ.y));
            ccBuffer.SetComputeIntParam(cellFrustumCullingCS, "_CellCount", itm.cellCulling.CellInf.cellData.Count);
            ccBuffer.SetComputeVectorArrayParam(cellFrustumCullingCS, "_FrustumPlanes", frustPlanes);

            ccBuffer.SetComputeBufferParam(cellFrustumCullingCS, cellFrustum_KernelID, "_CellData", itm.cellCulling.cellDataComputeB);

            // TODO:
            // TODO PRIO: MEDIUM
            // No neet to set every frame, only when itm instance changed (like when changing scene)
            ccBuffer.SetComputeBufferParam(cellFrustumCullingCS, cellFrustum_KernelID, "_IntersectedCellIndexes", itm.cellCulling.intersectedCellIndexesComputeB);
            ccBuffer.SetComputeBufferParam(cellFrustumCullingCS, cellFrustum_KernelID, "_FullyVisibleCellIndexes", itm.cellCulling.fullyVisibleCellIndexesComputeB);
            ccBuffer.SetComputeBufferCounterValue(itm.cellCulling.intersectedCellIndexesComputeB, 0);
            ccBuffer.SetComputeBufferCounterValue(itm.cellCulling.fullyVisibleCellIndexesComputeB, 0);

            ccBuffer.DispatchCompute(cellFrustumCullingCS, cellFrustum_KernelID, Mathf.CeilToInt(itm.cellCulling.CellInf.CellCount / 64f), 1, 1);
            #endregion



            #region Create Check Ranges
            ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_IntersectedCellIndexes", itm.cellCulling.intersectedCellIndexesComputeB);
            ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_FullyVisibleCellIndexes", itm.cellCulling.fullyVisibleCellIndexesComputeB);

            // We can reuse the checkranges' counts buffer to pass the cellIndexes counts
            ccBuffer.CopyCounterValue(itm.cellCulling.intersectedCellIndexesComputeB, itm.cellCulling.checkRangesCounts_Intersected_FullyVisible_ComputeB, 0);
            ccBuffer.CopyCounterValue(itm.cellCulling.fullyVisibleCellIndexesComputeB, itm.cellCulling.checkRangesCounts_Intersected_FullyVisible_ComputeB, 4);

            ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_CellIndexesCounts_Intersected_FullyVisible", itm.cellCulling.checkRangesCounts_Intersected_FullyVisible_ComputeB);


            #endregion

            List<InstancedTerrainDrawer> itds = itm.instancedTerrainDrawers;
            for (int j = 0; j < itds.Count; j++)
            {
                InstancedTerrainDrawer itd = itds[j];

                #region Clear Args Buffers
                ccBuffer.SetComputeIntParam(clearArgsCS, "_ClearBuffer0", 1);
                ccBuffer.SetComputeIntParam(clearArgsCS, "_ClearBuffer1", 1);
                ccBuffer.SetComputeIntParam(clearArgsCS, "_ClearBuffer2", 1);

                ccBuffer.SetComputeBufferParam(clearArgsCS, clearArgs_KernelID, "_CSArgsBuffer0", itd.dispatchArgsBuffer0);
                ccBuffer.SetComputeBufferParam(clearArgsCS, clearArgs_KernelID, "_CSArgsBuffer1", itd.dispatchArgsBuffer1);
                ccBuffer.SetComputeBufferParam(clearArgsCS, clearArgs_KernelID, "_CSArgsBuffer2", itd.dispatchArgsBuffer2);

                ccBuffer.DispatchCompute(clearArgsCS, clearArgs_KernelID, 1, 1, 1);
                #endregion

                // ArgsBuffer0 -> fully visible
                // ArgsBuffer1 -> intersected
                #region Create Check Ranges (itd rendering)
                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_CellStartIndexes", itd.cellStartIndexesComputeBuffer);
                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_CellCounts", itd.cellCountsComputeBuffer);

                ccBuffer.SetComputeBufferCounterValue(itd.intersectedFrustumCheckRangesComputeBuffer, 0);
                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_CheckRangesIntersected", itd.intersectedFrustumCheckRangesComputeBuffer);

                ccBuffer.SetComputeBufferCounterValue(itd.fullyVisibleFrustumCheckRangesComputeBuffer, 0);
                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_CheckRangesFullyVisible", itd.fullyVisibleFrustumCheckRangesComputeBuffer);

                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_FullyVisibleDispatchArgs", itd.dispatchArgsBuffer0);
                ccBuffer.SetComputeBufferParam(createCheckRangesCS, checkRanges_KernelID, "_IntersectedDispatchArgs", itd.dispatchArgsBuffer1);

                ccBuffer.DispatchCompute(createCheckRangesCS, checkRanges_KernelID, Mathf.CeilToInt(itm.cellCulling.CellInf.CellCount / 64f), 1, 1);
                #endregion

                #region Set count to [3] and Divide ArgsBuffer0 and ArgsBuffer1
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer0", 1);
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer1", 1);
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer2", 0);

                ccBuffer.SetComputeBufferParam(divideArgsCS, divideArgs_KernelID, "_CSArgsBuffer0", itd.dispatchArgsBuffer0);
                ccBuffer.SetComputeBufferParam(divideArgsCS, divideArgs_KernelID, "_CSArgsBuffer1", itd.dispatchArgsBuffer1);
                ccBuffer.SetComputeBufferParam(divideArgsCS, divideArgs_KernelID, "_CSArgsBuffer2", itd.dispatchArgsBuffer2);

                ccBuffer.DispatchCompute(divideArgsCS, divideArgs_KernelID, 1, 1, 1);
                #endregion

                #region Fill up with fully visible cells (ArgsBuffer0)
                ccBuffer.SetComputeBufferParam(fullyVisibleCS, fullyVisible_KernelID, "_CheckRanges", itd.fullyVisibleFrustumCheckRangesComputeBuffer);
                ccBuffer.SetComputeBufferParam(fullyVisibleCS, fullyVisible_KernelID, "_CheckRangesCounts_Intersected_FullyVisible", itm.cellCulling.checkRangesCounts_Intersected_FullyVisible_ComputeB);
                ccBuffer.SetComputeBufferParam(fullyVisibleCS, fullyVisible_KernelID, "_DispatchArgs", itd.dispatchArgsBuffer0);

                ccBuffer.SetComputeBufferCounterValue(itd.intersectedFrustumResultComputeBuffer, 0);
                ccBuffer.SetComputeBufferParam(fullyVisibleCS, fullyVisible_KernelID, "_FrustumCullingResultIndexes", itd.intersectedFrustumResultComputeBuffer);

                // ArgsBuffer2
                ccBuffer.SetComputeBufferParam(fullyVisibleCS, fullyVisible_KernelID, "_OcclusionDispatchArgs", itd.dispatchArgsBuffer2);

                ccBuffer.DispatchCompute(fullyVisibleCS, fullyVisible_KernelID, itd.dispatchArgsBuffer0, 0);
                #endregion

                #region Intersected Frustum Culling (ArgsBuffer1)
                ccBuffer.SetComputeVectorArrayParam(intersectedCS, "_FrustumPlanes", frustPlanes);
                ccBuffer.SetComputeVectorParam(intersectedCS, "_TerrainElementBoundsExtents", itd.terrainElementData.MultipliedBoundingBoxExtents);

                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_CheckRangesCounts_Intersected_FullyVisible", itm.cellCulling.checkRangesCounts_Intersected_FullyVisible_ComputeB);
                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_CheckRanges", itd.intersectedFrustumCheckRangesComputeBuffer);
                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_TerrainElementMatricesInCellOrderFlat", itd.terrainElementMatricesInCellOrderFlatComputeBuffer);
                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_FrustumCullingResultIndexes", itd.intersectedFrustumResultComputeBuffer);
                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_DispatchArgs", itd.dispatchArgsBuffer1);

                // ArgsBuffer2
                ccBuffer.SetComputeBufferParam(intersectedCS, intersected_KernelID, "_OcclusionDispatchArgs", itd.dispatchArgsBuffer2);


                // ArgsBuffer1
                ccBuffer.DispatchCompute(intersectedCS, intersected_KernelID, itd.dispatchArgsBuffer1, 0);
                #endregion

                #region Divide ArgsBuffer2
                // Compute Buffer Params already set

                // Only clear ArgsBuffer0
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer0", 0);
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer1", 0);
                ccBuffer.SetComputeIntParam(divideArgsCS, "_DivideBuffer2", 1);

                ccBuffer.DispatchCompute(divideArgsCS, divideArgs_KernelID, 1, 1, 1);
                #endregion

                #region Occlusion Culling (ArgsBuffer2)
                ccBuffer.SetComputeBufferParam(hizCullingCS, hiz_KernelID, "_TerrainElementMatricesInCellOrderFlat", itd.terrainElementMatricesInCellOrderFlatComputeBuffer);
                ccBuffer.SetComputeBufferParam(hizCullingCS, hiz_KernelID, "_FrustumCullingResultIndexes", itd.intersectedFrustumResultComputeBuffer);

                ccBuffer.CopyCounterValue(itd.intersectedFrustumResultComputeBuffer, itd.hizFrustumResultCountComputeBuffer, 0);
                ccBuffer.SetComputeBufferParam(hizCullingCS, hiz_KernelID, "_FrustumResultCount", itd.hizFrustumResultCountComputeBuffer);

                ccBuffer.SetComputeBufferCounterValue(itd.hizResultComputeBuffer, 0);
                ccBuffer.SetComputeBufferParam(hizCullingCS, hiz_KernelID, "_HiZBufferCullingResultIndexes", itd.hizResultComputeBuffer);

                ccBuffer.SetComputeVectorParam(hizCullingCS, "_TerrainElementBoundsExtents", itd.terrainElementData.MultipliedBoundingBoxExtents);
                ccBuffer.SetComputeMatrixParam(hizCullingCS, "_VPMatrix", currentCam.projectionMatrix * currentCam.worldToCameraMatrix);
                //ccBuffer.SetComputeMatrixParam(hizCullingCS, "_VMatrix", itms[i].cam.worldToCameraMatrix);
                //ccBuffer.SetComputeFloatParam(hizCullingCS, "_NoCullDistanceBehindObject", itd.drawSettings.noCullDistanceBehindObject);

                ccBuffer.DispatchCompute(hizCullingCS, hiz_KernelID, itd.dispatchArgsBuffer2, 0);
                #endregion

                ccBuffer.CopyCounterValue(itd.hizResultComputeBuffer, itd.argsBuffer, 4);
            }


            context.ExecuteCommandBuffer(ccBuffer);
            CommandBufferPool.Release(ccBuffer);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);

			InstancedTerrainManager itm = InstancedTerrainManager.instance;
            if (itm == null || itm.cellCulling.CellInf.cellData.Count == 0)
                return;

            cmd.ReleaseTemporaryRT(_HiZBufferTextureID);
            //cmd.ReleaseTemporaryRT(rt2ID);
            for (int i = 0; i < temporaryIDs.Length; i++)
            {
                cmd.ReleaseTemporaryRT(temporaryIDs[i]);
            }
        }
    }


    private class CustomRenderPassDRAW : ScriptableRenderPass
    {
        private ScriptableRenderer renderer;

        public CustomRenderPassDRAW()
        {

        }

        public void SetSource(ScriptableRenderer source)
        {
            renderer = source;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);

            ConfigureTarget(renderer.cameraColorTarget, renderer.cameraDepthTarget);
            ConfigureClear(ClearFlag.None, clearColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Only render in scene and game cameras
            if((renderingData.cameraData.cameraType & (CameraType.SceneView | CameraType.Game)) == 0)
                return;


            CommandBuffer cBuffer = CommandBufferPool.Get("InstancedTerrain_DRAW");
            InstancedTerrainManager itm = InstancedTerrainManager.instance;

            #region Draw
            List<InstancedTerrainDrawer> itds = itm.instancedTerrainDrawers;
            for (int j = 0; j < itds.Count; j++)
            {
                InstancedTerrainDrawer itd = itds[j];

                // Neccessary if the same material is used in different itds
                itd.SetMaterialBuffers();

                // Add draw command
                cBuffer.DrawMeshInstancedIndirect(itd.terrainElementData.renderMesh, 0, itd.terrainElementData.material, 0, itd.argsBuffer, 0);
                // Since we can't set material params through command buffers
                // Schedule cbuffer and prepare for the next draw call
                context.ExecuteCommandBuffer(cBuffer);
                cBuffer.Clear();
            }

            #endregion

            CommandBufferPool.Release(cBuffer);
        }
    }

    public Shader hizBufferGeneration_Shader;
    public ComputeShader CELLFrustumCulling_CS;
    public ComputeShader intersectedCells_CS;
    public ComputeShader fullyVisibleCells_CS;
    public ComputeShader createCheckRanges_CS;

    public ComputeShader hiZCulling_CS;

    public ComputeShader clearArgsBuffers_CS;
    public ComputeShader divideArgsBuffers_CS;


    private CustomRenderPass renderPassCULL;
    private CustomRenderPassDRAW renderPassDRAW;

    public override void Create()
    {
        if (hizBufferGeneration_Shader == null)
            Debug.LogError("Missing default shader: HiZ buffer generation shader (Unlit/HiZBufferShader missing?)");
        if (CELLFrustumCulling_CS == null)
            Debug.LogError("Missing default shader: Cell frustum culling compute shader");
        if (createCheckRanges_CS == null)
            Debug.LogError("Missing default shader: Create check ranges compute shader");
        if (fullyVisibleCells_CS == null)
            Debug.LogError("Missing default shader: Fully visible cells' compute shader");
        if (intersectedCells_CS == null)
            Debug.LogError("Missing default shader: Intersected cells' compute shader");
        if (clearArgsBuffers_CS == null)
            Debug.LogError("Missing default shader: Clear argument buffers compute shader");
        if (clearArgsBuffers_CS == null)
            Debug.LogError("Missing default shader: Round argument buffers compute shader");
        if (hiZCulling_CS == null)
            Debug.LogError("Missing default shader: HiZ buffer culling compute shader");

        Material hizMaterial = new Material(hizBufferGeneration_Shader);

        renderPassCULL = new CustomRenderPass(hizMaterial, hiZCulling_CS, CELLFrustumCulling_CS, createCheckRanges_CS, fullyVisibleCells_CS, intersectedCells_CS, clearArgsBuffers_CS, divideArgsBuffers_CS);
        renderPassCULL.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

        renderPassDRAW = new CustomRenderPassDRAW();
        renderPassDRAW.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        renderPassCULL.SetSource(renderer);
        renderPassDRAW.SetSource(renderer);

        renderer.EnqueuePass(renderPassCULL);
        renderer.EnqueuePass(renderPassDRAW);
    }
}
