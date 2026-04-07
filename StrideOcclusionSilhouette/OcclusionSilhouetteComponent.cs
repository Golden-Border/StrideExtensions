using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace StrideOcclusionSilhouette;

/// <summary>
/// Marks an entity for occlusion silhouette rendering. When the entity is occluded by other geometry,
/// a flat-colored silhouette is drawn to indicate its position behind obstacles (X-ray / see-through effect).
/// </summary>
/// <remarks>
/// <para>
/// This component is a pure data component — it carries no logic. The rendering is handled by
/// <c>OcclusionSilhouetteRenderFeature</c>, which must be added to the <c>MeshRenderFeature.RenderFeatures</c>
/// list in the Graphics Compositor.
/// </para>
/// <para>
/// The entity's <see cref="Stride.Rendering.RenderGroup"/> must match the <c>RenderGroup</c> configured on the
/// <c>OcclusionSilhouetteRenderFeature</c> for the silhouette to be rendered.
/// </para>
/// </remarks>
[DataContract("OcclusionSilhouetteComponent")]
[Display("Occlusion Silhouette", Expand = ExpandRule.Once)]
[ComponentCategory("Rendering")]
public sealed class OcclusionSilhouetteComponent : ActivableEntityComponent
{
    /// <summary>
    /// Gets or sets the color of the silhouette when the entity is occluded.
    /// </summary>
    /// <userdoc>The color and transparency of the silhouette rendered where the entity is hidden behind other geometry.</userdoc>
    [DataMember(10)]
    [Display("Silhouette Color")]
    public Color4 SilhouetteColor { get; set; } = new Color4(0f, 0.75f, 1f, 0.6f);
}
