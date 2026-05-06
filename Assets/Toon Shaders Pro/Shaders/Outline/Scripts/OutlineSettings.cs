using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ToonShadersPro.URP
{
    [System.Serializable, VolumeComponentMenu("Toon Shaders Pro/Outlines")]
    public sealed class OutlineSettings : VolumeComponent, IPostProcessComponent
    {
        public OutlineSettings()
        {
            displayName = "Outlines";
        }

        [Tooltip("Choose where to insert this pass in URP's render loop.\n" +
            "\nURP's internal post processing includes effects like bloom and color-correction, which may impact the appearance of the outlines.\n" +
            "\nFor example, with the Before setting, high-intensity HDR colors will be impacted by Bloom.")]
        public RenderPassEventParameter renderPassEvent = new RenderPassEventParameter(PostProcessRenderPassEvent.AfterURPPostProcessing);

        [Tooltip("Which outline-drawing algorithm to use.\n" + 
            "\n<b>No Outlines</b>" +
            "\n  Draws no outlines.\n" +
            "\n<b>Depth Normal Outlines</b>" +
            "\n  Detects small gradients in the color and depth-normal textures.\n" +
            "\n<b>High Quality Mask Outlines</b>" +
            "\n  Renders objects in specific layers to a mask and draws outlines along mask boundaries with error correction and thickness options.\n" +
            "\n<b>Pixel Width Mask Outlines</b>" +
            "\n  Also masks objects as before, but with more error cases and only pixel-width outlines.\n" +
            "\n<b>Hull Outlines</b>" +
            "\n  Renders all objects in specific layers with an inverted hull shader.\n" +
            "\n<b>Debug Outline Mask</b>" +
            "\n  Renders the mask texture used for outline detection.")]
        public OutlineTypeParameter outlineType = new OutlineTypeParameter(OutlineType.NoOutlines);

        [Tooltip("Color of the outlines.")]
        public ColorParameter outlineColor = new ColorParameter(Color.white, true, true, true);

        // DEPTH NORMAL OUTLINE SETTINGS
        [Header("Depth Normal Outline Settings")]
        [Tooltip("Threshold for color-based edge detection.")]
        public ClampedFloatParameter colorSensitivity = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);

        [Tooltip("Strength of color-based edges.")]
        public ClampedFloatParameter colorStrength = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        [Tooltip("Threshold for depth-based edge detection.")]
        public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(0.01f, 0.0f, 1.0f);

        [Tooltip("Strength of depth-based edges.")]
        public ClampedFloatParameter depthStrength = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

        [Tooltip("Threshold for normal-based edge detection.")]
        public ClampedFloatParameter normalSensitivity = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);

        [Tooltip("Strength of normal-based edges.")]
        public ClampedFloatParameter normalStrength = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

        [Tooltip("Pixels past this depth threshold will not be edge-detected.")]
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.99f, 0.0f, 1.0f);

        // DEPTH NORMAL LAYER MASKING
        [Space(10)]
        [Header("Depth Normal Layer Masking")]
        [Tooltip("Enable layer-based masking for depth normal outlines. When enabled, only objects on specified layers will contribute to outline detection.")]
        public BoolParameter useDepthNormalLayerMasking = new BoolParameter(false);

        [Tooltip("Layers to include in depth normal outline detection. Only objects on these layers will generate outlines.")]
        public LayerMaskParameter depthNormalLayerMask = new LayerMaskParameter(-1);

        [Tooltip("Invert the layer mask selection. When enabled, outlines will be applied to everything EXCEPT objects on the specified layers.")]
        public BoolParameter invertDepthNormalLayerMask = new BoolParameter(false);

        [Tooltip("LightMode tags for depth normal mask rendering.")]
        public LightModeTypeListParameter depthNormalLightModes = new LightModeTypeListParameter(new List<LightModeType>() { LightModeType.UniversalForwardOnly });

        [Tooltip("Render queue for depth normal mask objects.")]
        public RenderQueueParameter depthNormalRenderQueue = new RenderQueueParameter(RenderQueueType.Opaque);

        [Tooltip("Ignore depth when rendering depth normal mask (outlines visible through walls).")]
        public BoolParameter depthNormalMaskIgnoreDepth = new BoolParameter(false);

        // OBJECT MASK OUTLINE SETTINGS
        [Space(10)]
        [Header("Object Mask Outline Settings")]
        [Tooltip("Apply to the following regular layers.")]
        public LayerMaskParameter objectMask = new LayerMaskParameter(0);

        [Tooltip("Should all masked pixels use the same IDs, or have unique IDs per object, or per triangle?")]
        public MaskDrawingParameter maskDrawingMode = new MaskDrawingParameter(MaskDrawingMode.PerObject);

        [Tooltip("Which LightMode tags should be included in the mask?\n" +
            "\n<b>UniversalForwardOnly</b> includes the base Toon shader." + 
            "\n<b>UniversalForward</b> includes most lit shaders, including Shader Graphs." +
            "\n<b>SRPDefaultUnlit</b> includes most unlit shaders, including Shader Graphs." +
            "\nMost other settings will capture almost all shaders.\n" +
            "\n<b>Warning</b>: Duplicated entries will increase resource usage with no benefit.")]
        public LightModeTypeListParameter lightModes = new LightModeTypeListParameter(new List<LightModeType>() { LightModeType.UniversalForwardOnly });

        [Tooltip("Should outlines be applied to opaque or transparent objects?" +
            "\nCurrently, the outlines only function with opaque objects.")]
        public RenderQueueParameter renderQueue = new RenderQueueParameter(RenderQueueType.Opaque);

        [Tooltip("If ticked, objects are drawn to the mask texture without considering depth (outlines will be visible through walls)." +
            "\nOpaque objects are drawn front-to back, and transparents are drawn back-to-front.\n" +
            "\nUsing this mode to draw several objects will likely result in strange artefacts.")]
        public BoolParameter maskIgnoreDepth = new BoolParameter(false);

        [Tooltip("Should masked outlines also use normal-based edge detection?")]
        public BoolParameter useDepthNormals = new BoolParameter(false);

        [Tooltip("Thickness of masked outlines.")]
        public ClampedIntParameter maskedOutlineThickness = new ClampedIntParameter(1, 1, 5);

        [Tooltip("How much additional smoothing to apply to outlines.")]
        public ClampedFloatParameter maskedOutlineSmoothing = new ClampedFloatParameter(1.0f, 0.1f, 5.0f);

        [Tooltip("Should outlines be drawn only inside, only outside, or on both sides of mask boundaries?")]
        public DrawSidesParameter outlineDrawSides = new DrawSidesParameter(DrawSides.Both);

        [Tooltip("Start to fade outlines out at this distance.")]
        public FloatParameter outlineFadeStart = new FloatParameter(25.0f);

        [Tooltip("End fading outlines out at this distance.")]
        public FloatParameter outlineFadeEnd = new FloatParameter(50.0f);

        // HULL OUTLINE SETTINGS
        [Space(10)]
        [Header("Hull Outline Settings")]
        [Tooltip("Thickness of hull outlines.")]
        public ClampedFloatParameter outlineThickness = new ClampedFloatParameter(0.02f, 0.0f, 0.2f);

        [Tooltip("Should hull outlines use transparency?")]
        public BoolParameter outlineTransparency = new BoolParameter(false);

        [Tooltip("Should hull outlines use diffuse lighting from the main light?")]
        public BoolParameter outlineLighting = new BoolParameter(false);

        [Tooltip("Should hull outline normal direction be flipped?")]
        public BoolParameter flipOutlineDirection = new BoolParameter(true);

        [Tooltip("Minimum lighting amount applied to hull outlines.")]
        public ClampedFloatParameter outlineMinLighting = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        /*
        [Tooltip("A custom list of renderers to include in outline rendering.")]
        public RendererListParameter overrideIncludeRenderers = new RendererListParameter(new List<Renderer>());

        [Tooltip("A custom list of renderers to exclude from outline rendering.")]
        public RendererListParameter overrideExcludeRenderers = new RendererListParameter(new List<Renderer>());
        */

        public bool IsActive()
        {
            return outlineType.value != OutlineType.NoOutlines && active;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}