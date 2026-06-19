using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace ImportVanillaSaves.ImportVanillaSavesCode
{
    public partial class ProfileSelectButton : NButton
    {
        private TextureRect? _icon;

        private Tween? _tween;

        public int _profileId;

        private Action? _callback;

        public void Initialize(int profileId, Action callback)
        {
            _profileId = profileId;
            _icon = GetNode<TextureRect>("Icon");
            _icon.Texture = PreloadManager.Cache.GetAsset<Texture2D>(
                $"res://images/ui/profile/profile_icon_{_profileId}.png"
            );
            _callback = callback;
        }

        protected override void OnRelease()
        {
            _callback!();
        }

        public override void _Ready()
        {
            ConnectSignals();
        }

        protected override void OnFocus()
        {
            base.OnFocus();

            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(this, "scale", Vector2.One * 1.1f, 0.05);
        }

        protected override void OnUnfocus()
        {
            base.OnUnfocus();
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween
                .TweenProperty(this, "scale", Vector2.One, 0.5)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Expo);
            _tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        }
    }
}
