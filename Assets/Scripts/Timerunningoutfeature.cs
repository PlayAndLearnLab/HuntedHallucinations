using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP renderer feature that draws the "time running out" blur + red effect
/// (shader: Assets/Shader/Resources/TimeRunningOut.shader).
///
/// ONE-TIME SETUP: select Assets/Settings/PC_Renderer (and Mobile_Renderer),
/// click "Add Renderer Feature" > "Time Running Out Feature". Nothing to assign.
///
/// The pass is only added while TimeRunningOutEffect is active, so it costs
/// nothing in menus or early in a run.
/// </summary>
public class TimeRunningOutFeature : ScriptableRendererFeature
{
    [Tooltip("After post-processing so tonemapping doesn't wash out the red. UI overlays are still drawn on top, sharp.")]
    [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    private Material _material;
    private TimeRunningOutPass _pass;

    /// <summary>Frame on which the blur pass last actually drew. Used by TimeRunningOutEffect to detect a missing setup.</summary>
    public static int LastDrawnFrame { get; private set; } = -1;
    /// <summary>Why the pass last skipped drawing (for the Console / Inspector status).</summary>
    public static string LastSkipReason { get; private set; } = "";
    /// <summary>True once this feature has been created on the active renderer.</summary>
    public static bool IsInstalled { get; private set; }

    public override void Create()
    {
        Shader shader = Resources.Load<Shader>("TimeRunningOut");
        if (shader == null)
        {
            Debug.LogError("TimeRunningOutFeature: could not find 'TimeRunningOut' shader in a Resources folder.");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(shader);
        _pass = new TimeRunningOutPass(_material) { renderPassEvent = _injectionPoint };
        IsInstalled = true;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || !TimeRunningOutEffect.IsActive) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
        _pass = null;
    }

    private class TimeRunningOutPass : ScriptableRenderPass
    {
        private readonly Material _material;

        public TimeRunningOutPass(Material material)
        {
            _material = material;
            // We read the camera colour and write a new one, so we can't work on the backbuffer directly.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return; // not in Scene view / previews

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                LastSkipReason = "camera renders straight to the screen (no intermediate texture)";
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_TimeRunningOutColor";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
            renderGraph.AddBlitPass(blit, "Time Running Out");

            // Everything after this pass now reads our blurred, red image.
            resourceData.cameraColor = destination;

            LastDrawnFrame = Time.frameCount;
            LastSkipReason = "";
        }
    }
}

// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.RenderGraphModule;
// using UnityEngine.Rendering.RenderGraphModule.Util;
// using UnityEngine.Rendering.Universal;

// /// <summary>
// /// URP renderer feature that draws the "time running out" blur + red effect
// /// (shader: Assets/Shader/Resources/TimeRunningOut.shader).
// ///
// /// ONE-TIME SETUP: select Assets/Settings/PC_Renderer (and Mobile_Renderer),
// /// click "Add Renderer Feature" > "Time Running Out Feature". Nothing to assign.
// ///
// /// The pass is only added while TimeRunningOutEffect is active, so it costs
// /// nothing in menus or early in a run.
// /// </summary>
// public class TimeRunningOutFeature : ScriptableRendererFeature
// {
//     [Tooltip("After post-processing so tonemapping doesn't wash out the red. UI overlays are still drawn on top, sharp.")]
//     [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

//     private Material _material;
//     private TimeRunningOutPass _pass;

//     public override void Create()
//     {
//         Shader shader = Resources.Load<Shader>("TimeRunningOut");
//         if (shader == null)
//         {
//             Debug.LogError("TimeRunningOutFeature: could not find 'TimeRunningOut' shader in a Resources folder.");
//             return;
//         }

//         _material = CoreUtils.CreateEngineMaterial(shader);
//         _pass = new TimeRunningOutPass(_material) { renderPassEvent = _injectionPoint };
//     }

//     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//     {
//         if (_pass == null || !TimeRunningOutEffect.IsActive) return;
//         renderer.EnqueuePass(_pass);
//     }

//     protected override void Dispose(bool disposing)
//     {
//         CoreUtils.Destroy(_material);
//         _material = null;
//         _pass = null;
//     }

//     private class TimeRunningOutPass : ScriptableRenderPass
//     {
//         private readonly Material _material;

//         public TimeRunningOutPass(Material material)
//         {
//             _material = material;
//             // We read the camera colour and write a new one, so we can't work on the backbuffer directly.
//             requiresIntermediateTexture = true;
//         }

//         public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//         {
//             UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
//             if (cameraData.cameraType != CameraType.Game) return; // not in Scene view / previews

//             UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
//             if (resourceData.isActiveTargetBackBuffer) return;

//             TextureHandle source = resourceData.activeColorTexture;
//             TextureDesc desc = renderGraph.GetTextureDesc(source);
//             desc.name = "_TimeRunningOutColor";
//             desc.clearBuffer = false;
//             TextureHandle destination = renderGraph.CreateTexture(desc);

//             var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
//             renderGraph.AddBlitPass(blit, "Time Running Out");

//             // Everything after this pass now reads our blurred, red image.
//             resourceData.cameraColor = destination;
//         }
//     }
// }