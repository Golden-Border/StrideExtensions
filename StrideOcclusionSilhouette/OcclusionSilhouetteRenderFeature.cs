using OcclusionSilhouette;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using System.ComponentModel;

namespace StrideOcclusionSilhouette;

/// <summary>
/// A <see cref="SubRenderFeature"/> that renders a flat-colored silhouette of meshes wherever they are
/// occluded by other geometry (X-ray / see-through effect). Self-occlusion is avoided using the stencil buffer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Setup in Graphics Compositor:</b>
/// <list type="number">
///   <item>Add a render stage (e.g. "OcclusionSilhouette") with effect name <c>OcclusionSilhouetteEffect</c>.</item>
///   <item>Add a <see cref="SimpleGroupToRenderStageSelector"/> for that stage (use <see cref="RenderGroupMask.All"/> to cover all entities).</item>
///   <item>Add this feature to <see cref="MeshRenderFeature.RenderFeatures"/>.</item>
///   <item>Set <see cref="OpaqueRenderStage"/> to the existing opaque stage and <see cref="SilhouetteRenderStage"/> to the new stage.</item>
/// </list>
/// </para>
/// <para>
/// <b>Per-entity setup:</b>
/// Attach an <see cref="OcclusionSilhouetteComponent"/> to entities. No specific <see cref="RenderGroup"/> is required by default.
/// </para>
/// <para>
/// <b>How it works:</b>
/// During the opaque pass, stencil is written for matching entities so their own pixels are marked.
/// During the silhouette pass, the depth test is set to <see cref="CompareFunction.Greater"/> (draw only behind other geometry)
/// and a stencil test rejects pixels belonging to the entity itself, preventing self-occlusion.
/// Skinned/animated objects are supported via the <c>OcclusionSilhouetteEffect.sdfx</c> which conditionally includes
/// the <c>TransformationSkinning</c> mixin.
/// </para>
/// </remarks>
[Display("Occlusion Silhouette")]
public class OcclusionSilhouetteRenderFeature : SubRenderFeature
{
    private ConstantBufferOffsetReference colorCBufferSlot;
    private ObjectPropertyKey<Color4> silhouetteColorKey;

    /// <summary>
    /// Gets or sets the render group mask used to filter which entities receive the silhouette effect.
    /// Defaults to <see cref="RenderGroupMask.All"/> so that any entity with an <see cref="OcclusionSilhouetteComponent"/>
    /// is processed regardless of its render group.
    /// </summary>
    [DataMember(10)]
    [DefaultValue(RenderGroupMask.All)]
    public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;

    /// <summary>
    /// Gets or sets the opaque render stage. During this stage, stencil is written for matching entities
    /// to prevent self-occlusion in the silhouette pass.
    /// </summary>
    [DataMember(20)]
    public RenderStage? OpaqueRenderStage { get; set; }

    /// <summary>
    /// Gets or sets the silhouette render stage. This stage must use <c>OcclusionSilhouetteEffect</c> as its effect name.
    /// </summary>
    [DataMember(30)]
    public RenderStage? SilhouetteRenderStage { get; set; }

    /// <inheritdoc/>
    public override void Extract()
    {
        var silhouetteColors = RootRenderFeature.RenderData.GetData(silhouetteColorKey);

        foreach (var objectNodeReference in RootRenderFeature.ObjectNodeReferences)
        {
            var objectNode = RootRenderFeature.GetObjectNode(objectNodeReference);
            var renderMesh = (RenderMesh)objectNode.RenderObject;

            var color = new Color4(0);

            // Filter by render group
            if (((RenderGroupMask)(1U << (int)renderMesh.RenderGroup) & RenderGroup) != 0)
            {
                // Retrieve the OcclusionSilhouetteComponent from the source entity
                if (renderMesh.Source is ModelComponent modelComponent)
                {
                    var silhouetteComponent = modelComponent.Entity?.Get<OcclusionSilhouetteComponent>();
                    if (silhouetteComponent != null && silhouetteComponent.Enabled)
                    {
                        color = silhouetteComponent.SilhouetteColor;
                    }
                }
            }

            silhouetteColors[objectNodeReference] = color;
        }
    }

    /// <inheritdoc/>
    public override unsafe void Prepare(RenderDrawContext context)
    {
        var silhouetteColors = RootRenderFeature.RenderData.GetData(silhouetteColorKey);

        foreach (var renderNode in ((RootEffectRenderFeature)RootRenderFeature).RenderNodes)
        {
            var perDrawLayout = renderNode.RenderEffect.Reflection?.PerDrawLayout;
            if (perDrawLayout == null)
                continue;

            var colorOffset = perDrawLayout.GetConstantBufferOffset(colorCBufferSlot);
            if (colorOffset == -1)
                continue;

            var storedColor = silhouetteColors[renderNode.RenderObject.ObjectNode];

            var mappedCB = renderNode.Resources.ConstantBuffer.Data;
            var perDraw = (Color4*)((byte*)mappedCB + colorOffset);
            *perDraw = storedColor;
        }
    }

    /// <inheritdoc/>
    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        // Set stencil reference value for stages that use stencil-based self-occlusion avoidance.
        // This must be set before MeshRenderFeature issues draw calls.
        if (OpaqueRenderStage != null && renderViewStage.Index == OpaqueRenderStage.Index)
        {
            context.CommandList.SetStencilReference(1);
        }
        else if (SilhouetteRenderStage != null && renderViewStage.Index == SilhouetteRenderStage.Index)
        {
            context.CommandList.SetStencilReference(1);
        }
    }

    /// <inheritdoc/>
    public override void ProcessPipelineState(
        RenderContext context,
        RenderNodeReference renderNodeReference,
        ref RenderNode renderNode,
        RenderObject renderObject,
        PipelineStateDescription pipelineState)
    {
        base.ProcessPipelineState(context, renderNodeReference, ref renderNode, renderObject, pipelineState);

        // Only process objects in the matching render group that have the component
        if (((RenderGroupMask)(1U << (int)renderObject.RenderGroup) & RenderGroup) == 0)
            return;

        var silhouetteColors = RootRenderFeature.RenderData.GetData(silhouetteColorKey);
        var color = silhouetteColors[renderObject.ObjectNode];

        // No silhouette color set (component absent or disabled).
        // If this is the silhouette stage, disable color writes so the entity
        // doesn't render black over the framebuffer.
        if (color == new Color4(0))
        {
            if (SilhouetteRenderStage != null && renderNode.RenderStage == SilhouetteRenderStage)
            {
                pipelineState.BlendState.RenderTargets[0].ColorWriteChannels = ColorWriteChannels.None;
            }
            return;
        }

        var stencilOps = new DepthStencilStencilOpDescription
        {
            StencilFail = StencilOperation.Keep,
            StencilDepthBufferFail = StencilOperation.Keep,
        };

        if (OpaqueRenderStage != null && renderNode.RenderStage == OpaqueRenderStage)
        {
            // OPAQUE PASS: write stencil where the entity renders so we can reject these
            // pixels during the silhouette pass (self-occlusion avoidance).
            stencilOps.StencilFunction = CompareFunction.Always;
            stencilOps.StencilPass = StencilOperation.Replace;

            pipelineState.DepthStencilState.StencilEnable = true;
            pipelineState.DepthStencilState.StencilMask = 0x01;
            pipelineState.DepthStencilState.StencilWriteMask = 0x01;
            pipelineState.DepthStencilState.FrontFace = stencilOps;
            pipelineState.DepthStencilState.BackFace = stencilOps;
        }
        else if (SilhouetteRenderStage != null && renderNode.RenderStage == SilhouetteRenderStage)
        {
            // SILHOUETTE PASS: draw only where the entity is behind OTHER geometry.
            // - Depth Greater: only where the entity is farther than what's in the depth buffer
            // - Stencil NotEqual: reject the entity's own pixels (marked in the opaque pass)
            // - No depth write: don't corrupt the depth buffer
            // - Alpha blend: semi-transparent silhouette
            stencilOps.StencilFunction = CompareFunction.NotEqual;
            stencilOps.StencilPass = StencilOperation.Keep;

            pipelineState.DepthStencilState.DepthBufferEnable = true;
            pipelineState.DepthStencilState.DepthBufferWriteEnable = false;
            pipelineState.DepthStencilState.DepthBufferFunction = CompareFunction.Greater;
            pipelineState.DepthStencilState.StencilEnable = true;
            pipelineState.DepthStencilState.StencilMask = 0x01;
            pipelineState.DepthStencilState.StencilWriteMask = 0x00;
            pipelineState.DepthStencilState.FrontFace = stencilOps;
            pipelineState.DepthStencilState.BackFace = stencilOps;

            pipelineState.BlendState = BlendStates.AlphaBlend;
        }
    }

    /// <inheritdoc/>
    protected override void InitializeCore()
    {
        base.InitializeCore();

        silhouetteColorKey = RootRenderFeature.RenderData.CreateObjectKey<Color4>();
        colorCBufferSlot = ((RootEffectRenderFeature)RootRenderFeature)
            .CreateDrawCBufferOffsetSlot(OcclusionSilhouetteShaderKeys.SilhouetteColor.Name);
    }
}
