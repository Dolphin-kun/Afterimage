using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace Afterimage
{
    [VideoEffect("残像", ["アニメーション"], [], isAviUtlSupported: false)]
    internal class AfterimageEffect : VideoEffectBase
    {
        public override string Label => "残像";

        [Display(GroupName = "残像", Name = "フレーム数", Description = "保存するフレームの数")]
        [AnimationSlider("F0", "", 1, 50)]
        public Animation FrameCount { get; } = new Animation(50, 1, 1000);

        [Display(GroupName = "残像", Name = "持続フレーム", Description = "残像が表示されてから、完全に消えるまでの時間")]
        [AnimationSlider("F0", "", 1, 50)]
        public Animation Duration { get; } = new Animation(50, 1, 1000);

        [Display(GroupName = "残像", Name = "減衰", Description = "残像の減衰")]
        [ToggleSlider]
        public bool IsDecay { get => isDecay; set => Set(ref isDecay, value); }
        bool isDecay = true;

        [Display(GroupName = "残像", Name = "不透明度", Description = "残像の不透明度")]
        [ShowPropertyEditorWhen(nameof(IsDecay), true)]
        [AnimationSlider("F1", "%", 0, 50)]
        public Animation Opacity { get; } = new Animation(50, 0, 100);


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AfterimageEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [FrameCount, Opacity, Duration];
    }
}
