// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Rendering;

namespace StrideOcclusionSilhouette
{
    /// <summary>
    /// Parameter keys for the <c>OcclusionSilhouetteShader</c>.
    /// </summary>
    public static partial class OcclusionSilhouetteShaderKeys
    {
        public static readonly ValueParameterKey<Vector4> SilhouetteColor = ParameterKeys.NewValue<Vector4>();
    }
}
