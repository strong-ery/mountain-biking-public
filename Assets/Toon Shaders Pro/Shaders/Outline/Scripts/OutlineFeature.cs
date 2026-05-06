using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ToonShadersPro.URP
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        List<OutlineRenderPass> passes = new List<OutlineRenderPass>();

        public override void Create()
        {
            name = "Outlines";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Clear previous passes
            foreach (var pass in passes)
            {
                pass?.Dispose();
            }
            passes.Clear();

            // Get all active outline volumes in the scene
            var activeOutlineVolumes = GetActiveOutlineVolumes();
            
            // Create a pass for each active outline volume
            for (int i = 0; i < activeOutlineVolumes.Count; i++)
            {
                var settings = activeOutlineVolumes[i];
                if (settings != null && settings.IsActive() && settings.outlineType.value != OutlineType.NoOutlines)
                {
                    var pass = new OutlineRenderPass(i, settings);
                    
                    if (settings.outlineType.value != OutlineType.NoOutlines)
                    {
                        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
                    }

                    if (settings.outlineType.value == OutlineType.DepthNormalOutlines)
                    {
                        pass.ConfigureInput(ScriptableRenderPassInput.Normal);
                    }

                    passes.Add(pass);
                    renderer.EnqueuePass(pass);
                }
            }
        }

        // Method to find all active outline volumes in the scene
        private List<OutlineSettings> GetActiveOutlineVolumes()
        {
            var outlineVolumes = new List<OutlineSettings>();
            
            // Find all Volume components in the scene
#if UNITY_2023_1_OR_NEWER
            var allVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            var allVolumes = FindObjectsOfType<Volume>();
#endif
            var triggerTransform = Camera.main?.transform; // You might want to use the current camera instead
            
            foreach (var volume in allVolumes)
            {
                if (volume.isActiveAndEnabled)
                {
                    // Check if this volume affects the current position
                    bool volumeAffects = false;
                    
                    if (volume.isGlobal)
                    {
                        volumeAffects = true;
                    }
                    else if (triggerTransform != null)
                    {
                        // Check if the camera/trigger is within the volume's bounds
                        var colliders = volume.GetComponents<Collider>();
                        foreach (var collider in colliders)
                        {
                            if (collider.bounds.Contains(triggerTransform.position))
                            {
                                volumeAffects = true;
                                break;
                            }
                        }
                    }
                    
                    if (volumeAffects)
                    {
                        // Check if this volume has outline settings
                        if (volume.profile != null && volume.profile.TryGet<OutlineSettings>(out var outlineSettings))
                        {
                            if (outlineSettings != null && outlineSettings.IsActive())
                            {
                                outlineVolumes.Add(outlineSettings);
                            }
                        }
                    }
                }
            }
            
            return outlineVolumes;
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var pass in passes)
            {
                pass?.Dispose();
            }
            passes.Clear();
            base.Dispose(disposing);
        }

        class OutlineRenderPass : ScriptableRenderPass
        {
            private Material material;
            private Material maskMaterial;
            private Material hullMaterial;

            private RTHandle tempTexHandle;
            private RTHandle maskedObjectsHandle;
            private RTHandle depthNormalMaskHandle;

            private ProfilingSampler maskProfilingSampler;
            private ProfilingSampler hullProfilingSampler;
            private ProfilingSampler outlineProfilingSampler;
            private ProfilingSampler depthNormalMaskProfilingSampler;

            private OutlineSettings volumeSettings; // Store the specific volume settings
            private int passIndex; // For unique naming

            public OutlineRenderPass(int index, OutlineSettings settings)
            {
                passIndex = index;
                volumeSettings = settings;
                
                profilingSampler = new ProfilingSampler($"Toon Shaders Pro - Outlines {index}");
                maskProfilingSampler = new ProfilingSampler($"TSP - Object Mask Pass {index}");
                hullProfilingSampler = new ProfilingSampler($"TSP - Hull Outline Pass {index}");
                outlineProfilingSampler = new ProfilingSampler($"TSP - Post Process Outline Pass {index}");
                depthNormalMaskProfilingSampler = new ProfilingSampler($"TSP - Depth Normal Mask Pass {index}");

#if UNITY_6000_0_OR_NEWER
                requiresIntermediateTexture = true;
#endif
            }

            private void CreateMaterial()
            {
                var shader = Shader.Find("Hidden/ToonShadersPro/URP/Outlines");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/Outlines\".");
                    return;
                }

                material = new Material(shader);

                shader = Shader.Find("Hidden/ToonShadersPro/URP/MaskObject");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/MaskObject\".");
                    return;
                }

                maskMaterial = new Material(shader);

                shader = Shader.Find("Hidden/ToonShadersPro/URP/HullOutlines");

                if (shader == null)
                {
                    Debug.LogError("Cannot find shader: \"Hidden/ToonShadersPro/URP/HullOutlines\".");
                    return;
                }

                hullMaterial = new Material(shader);
            }

            private static RenderTextureDescriptor GetCopyPassDescriptor(RenderTextureDescriptor descriptor)
            {
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = (int)DepthBits.None;

                return descriptor;
            }

#if UNITY_6000_0_OR_NEWER
            [System.Obsolete]
#endif
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ResetTarget();

                var descriptor = GetCopyPassDescriptor(cameraTextureDescriptor);
                RenderingUtils.ReAllocateIfNeeded(ref tempTexHandle, descriptor);

                descriptor.colorFormat = RenderTextureFormat.R16;
                RenderingUtils.ReAllocateIfNeeded(ref maskedObjectsHandle, descriptor);

                // Allocate depth normal mask texture if needed
                if (volumeSettings.outlineType.value == OutlineType.DepthNormalOutlines && volumeSettings.useDepthNormalLayerMasking.value)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref depthNormalMaskHandle, descriptor);
                }

                base.Configure(cmd, cameraTextureDescriptor);
            }

#if UNITY_6000_0_OR_NEWER
            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.isPreviewCamera)
                {
                    return;
                }

                if (material == null || maskMaterial == null || hullMaterial == null)
                {
                    CreateMaterial();
                }

                CommandBuffer cmd = CommandBufferPool.Get();

                // Set Outline effect properties using the specific volume settings
                var settings = volumeSettings;
                renderPassEvent = settings.renderPassEvent.value.Convert();

                RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // For additive blending, we need to handle the blend modes appropriately
                bool isFirstPass = passIndex == 0;

                // Perform the Blit operations for the Outline effect.
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    if (settings.outlineType.value != OutlineType.NoOutlines)
                    {
                        Blitter.BlitCameraTexture(cmd, cameraTargetHandle, tempTexHandle);
                        material.SetColor("_OutlineColor", settings.outlineColor.value);
                    }

                    if (settings.maskIgnoreDepth.value)
                    {
                        maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                    }
                    else
                    {
                        maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                    }

                    RenderQueueRange range = settings.renderQueue.value.Convert();
                    int shaderPassIndex = 0;

                    switch (settings.outlineType.value)
                    {
                        case OutlineType.DepthNormalOutlines:
                            {
                                material.SetFloat("_ColorSensitivity", settings.colorSensitivity.value);
                                material.SetFloat("_ColorStrength", settings.colorStrength.value);
                                material.SetFloat("_DepthSensitivity", settings.depthSensitivity.value);
                                material.SetFloat("_DepthStrength", settings.depthStrength.value);
                                material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                                material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                                material.SetFloat("_DepthThreshold", settings.depthThreshold.value);

                                // Handle depth normal layer masking
                                if (settings.useDepthNormalLayerMasking.value)
                                {
                                    if (settings.depthNormalMaskIgnoreDepth.value)
                                    {
                                        maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                                    }
                                    else
                                    {
                                        maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                                    }

                                    CoreUtils.SetRenderTarget(cmd, depthNormalMaskHandle);

                                    // FIXED: When not inverting, clear to black (objects draw white = included)
                                    // When inverting, clear to white (objects draw black = excluded, everything else included)
                                    Color clearColor = settings.invertDepthNormalLayerMask.value ? Color.white : Color.black;
                                    CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, clearColor);

                                    var tempSettings = new TempDepthNormalMaskSettings
                                    {
                                        renderQueue = settings.depthNormalRenderQueue,
                                        objectMask = settings.depthNormalLayerMask,
                                        lightModes = settings.depthNormalLightModes,
                                        maskDrawingMode = new MaskDrawingParameter(MaskDrawingMode.PerObject)
                                    };

                                    // FIXED: When not inverting, draw white (pass 0). When inverting, draw black (pass 1)
                                    int maskPassIndex = settings.invertDepthNormalLayerMask.value ? 1 : 0;
                                    DrawObjectsForDepthNormalMask(context, ref renderingData, cmd, tempSettings, maskMaterial, maskPassIndex, depthNormalMaskProfilingSampler);

                                    material.EnableKeyword("_USE_DEPTH_NORMAL_LAYER_MASK");
                                    material.SetTexture("_DepthNormalMask", depthNormalMaskHandle);
                                }
                                else
                                {
                                    material.DisableKeyword("_USE_DEPTH_NORMAL_LAYER_MASK");
                                }

                                // For additive outlines, use additive blend mode for subsequent passes
                                if (!isFirstPass)
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.One);
                                }
                                else
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                                }

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 0);
                                }
                                break;
                            }
                        case OutlineType.HighQualityObjectMaskOutlines:
                            {
                                material.SetTexture("_MaskedObjects", maskedObjectsHandle);
                                material.SetInteger("_OutlineWidth", settings.maskedOutlineThickness.value);
                                float drawInside = (settings.outlineDrawSides.value != DrawSides.Outside ? 1.0f : 0.0f);
                                float drawOutside = (settings.outlineDrawSides.value != DrawSides.Inside ? 1.0f : 0.0f);
                                material.SetVector("_DrawSides", new Vector2(drawInside, drawOutside));
                                material.SetFloat("_OutlineFadeStart", settings.outlineFadeStart.value);
                                material.SetFloat("_OutlineFadeEnd", settings.outlineFadeEnd.value);
                                material.SetFloat("_Spread", 1.0f / (settings.maskedOutlineSmoothing.value * 32.0f * Mathf.Pow(settings.maskedOutlineThickness.value / 6.0f, 2)));

                                if (settings.useDepthNormals.value)
                                {
                                    material.EnableKeyword("_USE_DEPTH_NORMALS");
                                    material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                                    material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                                }
                                else
                                {
                                    material.DisableKeyword("_USE_DEPTH_NORMALS");
                                }

                                // For additive outlines, use additive blend mode for subsequent passes
                                if (!isFirstPass)
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.One);
                                }
                                else
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                                }

                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                shaderPassIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, shaderPassIndex, maskProfilingSampler);

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 1);
                                }
                                break;
                            }
                        case OutlineType.PixelWidthObjectMaskOutlines:
                            {
                                material.SetTexture("_MaskedObjects", maskedObjectsHandle);

                                // For additive outlines, use additive blend mode for subsequent passes
                                if (!isFirstPass)
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.One);
                                }
                                else
                                {
                                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                                }

                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                shaderPassIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, shaderPassIndex, maskProfilingSampler);

                                using (new ProfilingScope(cmd, outlineProfilingSampler))
                                {
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle, material, 2);
                                }
                                break;
                            }
                        case OutlineType.HullOutlines:
                            {
                                hullMaterial.SetColor("_OutlineColor", settings.outlineColor.value);
                                hullMaterial.SetFloat("_OutlineThickness", settings.outlineThickness.value);

                                // For additive hull outlines
                                if (settings.outlineTransparency.value || !isFirstPass)
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                                }
                                else
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                                }

                                if (settings.outlineLighting.value)
                                {
                                    hullMaterial.EnableKeyword("_HULL_LIGHTING_ON");
                                    hullMaterial.SetFloat("_OutlineDirection", settings.flipOutlineDirection.value ? 1.0f : -1.0f);
                                    hullMaterial.SetFloat("_OutlineMinLighting", settings.outlineMinLighting.value);
                                }
                                else
                                {
                                    hullMaterial.DisableKeyword("_HULL_LIGHTING_ON");
                                }

                                if (isFirstPass)
                                {
                                    CoreUtils.SetRenderTarget(cmd, tempTexHandle);
                                    DrawObjects(context, ref renderingData, cmd, settings, hullMaterial, 0, hullProfilingSampler);
                                    Blitter.BlitCameraTexture(cmd, tempTexHandle, cameraTargetHandle);
                                }
                                else
                                {
                                    CoreUtils.SetRenderTarget(cmd, cameraTargetHandle);
                                    DrawObjects(context, ref renderingData, cmd, settings, hullMaterial, 0, hullProfilingSampler);
                                }
                                break;
                            }
                        case OutlineType.DebugOutlineMask:
                            {
                                // For debug, we probably don't want additive behavior
                                CoreUtils.SetRenderTarget(cmd, maskedObjectsHandle);
                                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
                                shaderPassIndex = settings.maskDrawingMode.value.Convert();
                                DrawObjects(context, ref renderingData, cmd, settings, maskMaterial, shaderPassIndex, maskProfilingSampler);

                                Blitter.BlitCameraTexture(cmd, maskedObjectsHandle, cameraTargetHandle);
                                break;
                            }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            // Helper struct for temporary depth normal mask settings
            private struct TempDepthNormalMaskSettings
            {
                public RenderQueueParameter renderQueue;
                public LayerMaskParameter objectMask;
                public LightModeTypeListParameter lightModes;
                public MaskDrawingParameter maskDrawingMode;
            }

            // Specialized method for drawing depth normal mask
            private void DrawObjectsForDepthNormalMask(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, TempDepthNormalMaskSettings settings, Material drawMaterial, int passIndex, ProfilingSampler profilingSampler)
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var camera = renderingData.cameraData.camera;
                    var cullingResults = renderingData.cullResults;

                    FilteringSettings filteringSettings =
                        new FilteringSettings(settings.renderQueue.value.Convert(), settings.objectMask.value);

                    DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(settings.lightModes.value.Convert(), ref renderingData, SortingCriteria.RenderQueue);
                    drawingSettings.overrideMaterial = drawMaterial;
                    drawingSettings.overrideMaterialPassIndex = passIndex;

                    RendererListParams rendererParams = new RendererListParams(cullingResults, drawingSettings, filteringSettings);
                    RendererList rendererList = context.CreateRendererList(ref rendererParams);

                    cmd.DrawRendererList(rendererList);
                }
            }

            private void DrawObjects(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, OutlineSettings settings, Material drawMaterial, int passIndex, ProfilingSampler profilingSampler)
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var camera = renderingData.cameraData.camera;
                    var cullingResults = renderingData.cullResults;

                    FilteringSettings filteringSettings =
                        new FilteringSettings(settings.renderQueue.value.Convert(), settings.objectMask.value);

                    DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(settings.lightModes.value.Convert(), ref renderingData, SortingCriteria.RenderQueue);
                    drawingSettings.overrideMaterial = drawMaterial;
                    drawingSettings.overrideMaterialPassIndex = passIndex;

                    RendererListParams rendererParams = new RendererListParams(cullingResults, drawingSettings, filteringSettings);
                    RendererList rendererList = context.CreateRendererList(ref rendererParams);

                    cmd.DrawRendererList(rendererList);
                }
            }

            public void Dispose()
            {
                tempTexHandle?.Release();
                maskedObjectsHandle?.Release();
                depthNormalMaskHandle?.Release();
            }

#if UNITY_6000_0_OR_NEWER

            private class CopyPassData
            {
                public TextureHandle inputTexture;
            }

            private class DepthNormalData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
                public TextureHandle depthNormalMaskTexture;
                public bool useLayerMasking;
                public bool invertLayerMask;
                public bool isFirstPass;
            }

            private class MaskData
            {
                public RendererListHandle rendererList;
            }

            private class HQOutlineData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
                public TextureHandle maskTexture;
                public bool isFirstPass;
            }

            private class LQOutlineData
            {
                public Material outlineMaterial;
                public TextureHandle tempTexture;
                public TextureHandle maskTexture;
                public bool isFirstPass;
            }

            private class HullOutlineData
            {
                public RendererListHandle rendererList;
                public bool isFirstPass;
                public TextureHandle tempTexture;
            }

            private static void ExecuteCopyPass(RasterCommandBuffer cmd, RTHandle source)
            {
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            private static void DepthNormalOutlines(RasterCommandBuffer cmd, RTHandle source, RTHandle depthNormalMask, Material material, bool useLayerMasking, bool invertLayerMask, bool isFirstPass, OutlineSettings settings)
            {
                material.SetFloat("_ColorSensitivity", settings.colorSensitivity.value);
                material.SetFloat("_ColorStrength", settings.colorStrength.value);
                material.SetFloat("_DepthSensitivity", settings.depthSensitivity.value);
                material.SetFloat("_DepthStrength", settings.depthStrength.value);
                material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                material.SetFloat("_DepthThreshold", settings.depthThreshold.value);

                // Handle layer masking
                if (useLayerMasking)
                {
                    material.EnableKeyword("_USE_DEPTH_NORMAL_LAYER_MASK");
                    material.SetTexture("_DepthNormalMask", depthNormalMask);
                    
                    // FIXED: Don't use the invert keyword since we handle it in mask generation
                    material.DisableKeyword("_INVERT_DEPTH_NORMAL_LAYER_MASK");
                }
                else
                {
                    material.DisableKeyword("_USE_DEPTH_NORMAL_LAYER_MASK");
                    material.DisableKeyword("_INVERT_DEPTH_NORMAL_LAYER_MASK");
                }

                // For additive outlines, use additive blend mode for subsequent passes
                if (!isFirstPass)
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.One);
                }
                else
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                }

                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 0);
            }

            private static void DrawObjects(MaskData data, RasterGraphContext context, bool clear)
            {
                if(clear)
                {
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                }

                context.cmd.DrawRendererList(data.rendererList);
            }

            private static void DrawHullObjects(HullOutlineData data, RasterGraphContext context, bool clear)
            {
                if(clear)
                {
                    context.cmd.ClearRenderTarget(true, true, Color.clear);
                }

                context.cmd.DrawRendererList(data.rendererList);
            }

            private static void HighQualityMaskOutlinesPass(RasterCommandBuffer cmd, RTHandle source, RTHandle maskedObjectsHandle, Material material, bool isFirstPass, OutlineSettings settings)
            {
                material.SetTexture("_MaskedObjects", maskedObjectsHandle);
                material.SetInteger("_OutlineWidth", settings.maskedOutlineThickness.value);
                float drawInside = (settings.outlineDrawSides.value != DrawSides.Outside ? 1.0f : 0.0f);
                float drawOutside = (settings.outlineDrawSides.value != DrawSides.Inside ? 1.0f : 0.0f);
                material.SetVector("_DrawSides", new Vector2(drawInside, drawOutside));
                material.SetFloat("_OutlineFadeStart", settings.outlineFadeStart.value);
                material.SetFloat("_OutlineFadeEnd", settings.outlineFadeEnd.value);
                material.SetFloat("_Spread", 1.0f / (settings.maskedOutlineSmoothing.value * 32.0f * Mathf.Pow(settings.maskedOutlineThickness.value / 6.0f, 2)));

                if (settings.useDepthNormals.value)
                {
                    material.EnableKeyword("_USE_DEPTH_NORMALS");
                    material.SetFloat("_NormalsSensitivity", settings.normalSensitivity.value);
                    material.SetFloat("_NormalsStrength", settings.normalStrength.value);
                }
                else
                {
                    material.DisableKeyword("_USE_DEPTH_NORMALS");
                }

                // For additive outlines, use additive blend mode for subsequent passes
                if (!isFirstPass)
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.One);
                }
                else
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                }

                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 1);
            }

            private static void LowQualityMaskOutlinesPass(RasterCommandBuffer cmd, RTHandle source, RTHandle maskedObjectsHandle, Material material, bool isFirstPass, OutlineSettings settings)
            {
                material.SetTexture("_MaskedObjects", maskedObjectsHandle);

                // For additive outlines, use additive blend mode for subsequent passes
                if (!isFirstPass)
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.One);
                }
                else
                {
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                }
                
                Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), material, 2);
            }

            public RendererListHandle GetRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, Camera camera, OutlineSettings settings, List<ShaderTagId> shaderTagIDs, Material material)
            {
                var cullingResults = renderingData.cullResults;

                RenderQueueRange renderQueueRange = settings.renderQueue.value.Convert();

                int passIndex = (settings.maskDrawingMode.value.Convert());
                SortingCriteria sortingCriteria = 
                    (settings.renderQueue.value == RenderQueueType.Transparent) ? SortingCriteria.CommonTransparent : SortingCriteria.CommonOpaque;

                var rendererListDesc = new RendererListDesc(shaderTagIDs.ToArray(), cullingResults, camera)
                {
                    renderQueueRange = renderQueueRange,
                    layerMask = settings.objectMask.value,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = passIndex,
                    sortingCriteria = sortingCriteria
                };

                return renderGraph.CreateRendererList(rendererListDesc);
            }

            // Helper method for depth normal layer masking renderer list
            public RendererListHandle GetDepthNormalRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, Camera camera, OutlineSettings settings, List<ShaderTagId> shaderTagIDs, Material material)
            {
                var cullingResults = renderingData.cullResults;

                RenderQueueRange renderQueueRange = settings.depthNormalRenderQueue.value.Convert();
                SortingCriteria sortingCriteria =
                    (settings.depthNormalRenderQueue.value == RenderQueueType.Transparent) ? SortingCriteria.CommonTransparent : SortingCriteria.CommonOpaque;

                // FIXED: Use correct pass based on invert setting
                // Pass 0 draws white (normal mode), Pass 1 draws black (invert mode)
                int passIndex = settings.invertDepthNormalLayerMask.value ? 1 : 0;

                var rendererListDesc = new RendererListDesc(shaderTagIDs.ToArray(), cullingResults, camera)
                {
                    renderQueueRange = renderQueueRange,
                    layerMask = settings.depthNormalLayerMask.value,
                    overrideMaterial = material,
                    overrideMaterialPassIndex = passIndex,
                    sortingCriteria = sortingCriteria
                };

                return renderGraph.CreateRendererList(rendererListDesc);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

                if (cameraData.isPreviewCamera)
                {
                    return;
                }

                if (material == null || maskMaterial == null || hullMaterial == null)
                {
                    CreateMaterial();
                }

                var settings = volumeSettings;
                renderPassEvent = settings.renderPassEvent.value.Convert();

                UniversalRenderer renderer = (UniversalRenderer)cameraData.renderer;
                Camera camera = cameraData.camera;

                var descriptor = GetCopyPassDescriptor(cameraData.cameraTargetDescriptor);
                TextureHandle tempTexHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, $"_OutlineColorTexture_{passIndex}", false);

                descriptor.colorFormat = RenderTextureFormat.R16;
                TextureHandle maskedObjectsHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, $"_MaskedObjects_{passIndex}", false);

                // Create depth normal mask texture if needed
                TextureHandle depthNormalMaskHandle = TextureHandle.nullHandle;
                bool useDepthNormalLayerMasking = settings.outlineType.value == OutlineType.DepthNormalOutlines && settings.useDepthNormalLayerMasking.value;
                if (useDepthNormalLayerMasking)
                {
                    depthNormalMaskHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, $"_DepthNormalMask_{passIndex}", false);
                }

                bool isFirstPass = passIndex == 0;

                if(settings.outlineType.value != OutlineType.NoOutlines &&
                    settings.outlineType.value != OutlineType.HullOutlines)
                {
                    material.SetColor("_OutlineColor", settings.outlineColor.value);

                    using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>($"Outline_CopyColorTexture_{passIndex}", out var passData, profilingSampler))
                    {
                        passData.inputTexture = resourceData.activeColorTexture;

                        builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                        builder.SetRenderAttachment(tempTexHandle, 0, AccessFlags.Write);
                        builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                    }
                }

                if (settings.maskIgnoreDepth.value)
                {
                    maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                }
                else
                {
                    maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                }

                switch (settings.outlineType.value)
                {
                    case OutlineType.DepthNormalOutlines:
                        {
                            if (useDepthNormalLayerMasking)
                            {
                                // Configure mask material for depth normal masking
                                if (settings.depthNormalMaskIgnoreDepth.value)
                                {
                                    maskMaterial.EnableKeyword("_IGNORE_DEPTH");
                                }
                                else
                                {
                                    maskMaterial.DisableKeyword("_IGNORE_DEPTH");
                                }

                                using (var builder = renderGraph.AddRasterRenderPass<MaskData>($"Outline_DepthNormalMask_{passIndex}", out var passData, depthNormalMaskProfilingSampler))
                                {
                                    var lightModes = settings.depthNormalLightModes.value;
                                    passData.rendererList = GetDepthNormalRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                    builder.UseRendererList(passData.rendererList);
                                    builder.SetRenderAttachment(depthNormalMaskHandle, 0, AccessFlags.Write);
                                    builder.SetGlobalTextureAfterPass(in depthNormalMaskHandle, Shader.PropertyToID($"_DepthNormalMask_{passIndex}"));

                                    // FIXED: Proper clear color and draw logic
                                    bool clearWithWhite = settings.invertDepthNormalLayerMask.value;
                                    builder.SetRenderFunc((MaskData data, RasterGraphContext context) =>
                                    {
                                        Color clearColor = clearWithWhite ? Color.white : Color.black;
                                        context.cmd.ClearRenderTarget(true, true, clearColor);
                                        context.cmd.DrawRendererList(data.rendererList);
                                    });
                                }
                            }

                            using (var builder = renderGraph.AddRasterRenderPass<DepthNormalData>($"Outline_DepthNormalOutlines_{passIndex}", out var passData, profilingSampler))
                            {
                                passData.tempTexture = tempTexHandle;
                                passData.outlineMaterial = material;
                                passData.depthNormalMaskTexture = depthNormalMaskHandle;
                                passData.useLayerMasking = useDepthNormalLayerMasking;
                                passData.invertLayerMask = settings.invertDepthNormalLayerMask.value;
                                passData.isFirstPass = isFirstPass;

                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                if (useDepthNormalLayerMasking)
                                {
                                    builder.UseTexture(depthNormalMaskHandle, AccessFlags.Read);
                                }
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((DepthNormalData data, RasterGraphContext context) => 
                                    DepthNormalOutlines(context.cmd, data.tempTexture, data.depthNormalMaskTexture, data.outlineMaterial, data.useLayerMasking, data.invertLayerMask, data.isFirstPass, settings));
                            }
                            break;
                        }

                    case OutlineType.HighQualityObjectMaskOutlines:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>($"Outline_DrawMask_{passIndex}", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetGlobalTextureAfterPass(in maskedObjectsHandle, Shader.PropertyToID($"_MaskedObjects_{passIndex}"));
                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Render the outlines in high-quality mode.
                            using (var builder = renderGraph.AddRasterRenderPass<HQOutlineData>($"Outline_DrawHQOutlines_{passIndex}", out var passData, outlineProfilingSampler))
                            {
                                passData.outlineMaterial = material;
                                passData.tempTexture = tempTexHandle;
                                passData.maskTexture = maskedObjectsHandle;
                                passData.isFirstPass = isFirstPass;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((HQOutlineData data, RasterGraphContext context) => HighQualityMaskOutlinesPass(context.cmd, data.tempTexture, data.maskTexture, data.outlineMaterial, data.isFirstPass, settings));
                            }
                            break;
                        }

                    case OutlineType.PixelWidthObjectMaskOutlines:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>($"Outline_DrawMask_{passIndex}", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetGlobalTextureAfterPass(in maskedObjectsHandle, Shader.PropertyToID($"_MaskedObjects_{passIndex}"));
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Render the outlines in low-quality mode.
                            using (var builder = renderGraph.AddRasterRenderPass<LQOutlineData>($"Outline_DrawLQOutlines_{passIndex}", out var passData, outlineProfilingSampler))
                            {
                                passData.outlineMaterial = material;
                                passData.tempTexture = tempTexHandle;
                                passData.maskTexture = maskedObjectsHandle;
                                passData.isFirstPass = isFirstPass;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.UseTexture(tempTexHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((LQOutlineData data, RasterGraphContext context) => LowQualityMaskOutlinesPass(context.cmd, data.tempTexture, data.maskTexture, data.outlineMaterial, data.isFirstPass, settings));
                            }
                        }
                        break;

                    case OutlineType.HullOutlines:
                        {
                            // Render the hull outlines.
                            using (var builder = renderGraph.AddRasterRenderPass<HullOutlineData>($"Outline_DrawHullOutlines_{passIndex}", out var passData, maskProfilingSampler))
                            {
                                hullMaterial.SetColor("_OutlineColor", settings.outlineColor.value);
                                hullMaterial.SetFloat("_OutlineThickness", settings.outlineThickness.value);

                                // For additive hull outlines
                                if (settings.outlineTransparency.value || !isFirstPass)
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                                }
                                else
                                {
                                    hullMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                                    hullMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
                                }

                                if (settings.outlineLighting.value)
                                {
                                    hullMaterial.EnableKeyword("_HULL_LIGHTING_ON");
                                    hullMaterial.SetFloat("_OutlineDirection", settings.flipOutlineDirection.value ? 1.0f : -1.0f);
                                    hullMaterial.SetFloat("_OutlineMinLighting", settings.outlineMinLighting.value);
                                }
                                else
                                {
                                    hullMaterial.DisableKeyword("_HULL_LIGHTING_ON");
                                }

                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), hullMaterial);
                                passData.isFirstPass = isFirstPass;
                                passData.tempTexture = tempTexHandle;

                                builder.UseRendererList(passData.rendererList);
                                
                                if (isFirstPass)
                                {
                                    // For first pass, render to temp texture then blit to camera
                                    builder.SetRenderAttachment(tempTexHandle, 0, AccessFlags.Write);
                                    builder.SetRenderFunc((HullOutlineData data, RasterGraphContext context) => DrawHullObjects(data, context, true));
                                    
                                    // Then copy to camera target
                                    using (var copyBuilder = renderGraph.AddRasterRenderPass<CopyPassData>($"Outline_CopyHullResult_{passIndex}", out var copyPassData, profilingSampler))
                                    {
                                        copyPassData.inputTexture = tempTexHandle;
                                        copyBuilder.UseTexture(tempTexHandle, AccessFlags.Read);
                                        copyBuilder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                        copyBuilder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                                    }
                                }
                                else
                                {
                                    // For subsequent passes, render directly to camera target with blending
                                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                    builder.SetRenderFunc((HullOutlineData data, RasterGraphContext context) => DrawHullObjects(data, context, false));
                                }
                            }
                        }
                        break;

                    case OutlineType.DebugOutlineMask:
                        {
                            // Render the object mask.
                            using (var builder = renderGraph.AddRasterRenderPass<MaskData>($"Outline_DrawMask_{passIndex}", out var passData, maskProfilingSampler))
                            {
                                var lightModes = settings.lightModes.value;

                                passData.rendererList = GetRendererList(renderGraph, renderingData, camera, settings, lightModes.Convert(), maskMaterial);

                                builder.UseRendererList(passData.rendererList);
                                builder.SetRenderAttachment(maskedObjectsHandle, 0, AccessFlags.Write);
                                builder.SetRenderFunc((MaskData data, RasterGraphContext context) => DrawObjects(data, context, true));
                            }

                            // Copy the mask texture to the camera output.
                            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>($"Outline_DebugDraw_{passIndex}", out var passData, profilingSampler))
                            {
                                passData.inputTexture = maskedObjectsHandle;

                                builder.UseTexture(maskedObjectsHandle, AccessFlags.Read);
                                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(context.cmd, data.inputTexture));
                            }
                            break;
                        }
                }
            }

#endif
        }
    }
}