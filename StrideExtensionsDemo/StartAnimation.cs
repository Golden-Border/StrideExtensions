using Stride.Engine;

namespace StrideExtensionsDemo;

public class StartAnimation : StartupScript
{
    public AnimationComponent AnimationComponent { get; set; }

    public string AnimationName { get; set; }

    public override void Start()
    {
        AnimationComponent.Play(AnimationName);
    }
}
