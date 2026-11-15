using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public abstract partial class AbstractSaveButton : NButton
{
    public void Initialize(int profileId)
    {
        _profileId = profileId;
    }

    public static LocString MkLocStr(string key)
    {
        return new("main_menu_ui", $"IMPORT_VANILLA_SAVES-{key}");
    }

    private static readonly LocString PopupCancelLoc = new(
        "main_menu_ui",
        "PROFILE_SCREEN.DELETE_CONFIRM_POPUP.cancel"
    );

    private static readonly PackedScene ProfileSelectButtonScene = PreloadManager.Cache.GetScene(
        "res://ImportVanillaSaves/profile_button.tscn"
    );

    private static readonly Texture2D SelectedButtonOutline =
        PreloadManager.Cache.GetAsset<CompressedTexture2D>(
            "res://ImportVanillaSaves/selected_button_outline.png"
        );

    protected abstract LocString TitleLoc { get; }
    protected abstract LocString DescLoc { get; }
    protected abstract LocString ButtonLabelLoc { get; }
    protected abstract LocString WorkingLoc { get; }
    protected abstract LocString ConfirmLoc { get; }

    private TextureRect? _icon;
    private MegaLabel? _label;
    private Tween? _tween;
    private int _profileId;
    private bool _isImporting;

    public override void _Ready()
    {
        ConnectSignals();
        _label = GetNode<MegaLabel>("%MegaLabel");
        _label.SetTextAutoSize(ButtonLabelLoc.GetFormattedText());
        _icon = GetNode<TextureRect>("Icon");
    }

    protected override void OnRelease()
    {
        if (_isImporting)
            return;
        TaskHelper.RunSafely(ConfirmPopup());
    }

    private void UpdateLoc(int vanillaProfileId, int moddedProfileId)
    {
        TitleLoc.Add("VanillaProfile", vanillaProfileId);
        TitleLoc.Add("ModdedProfile", moddedProfileId);
        DescLoc.Add("VanillaProfile", vanillaProfileId);
        DescLoc.Add("ModdedProfile", moddedProfileId);
    }

    protected abstract void PerformTransferIO(int vanillaProfileId, int moddedProfileId);

    private async Task ConfirmPopup()
    {
        NGenericPopup nGenericPopup = NGenericPopup.Create()!;
        NModalContainer.Instance!.Add(nGenericPopup);

        int selectedProfileId = _profileId;
        UpdateLoc(selectedProfileId, _profileId);

        var vPopup = nGenericPopup.GetNode<NVerticalPopup>("VerticalPopup");

        var profileSelectContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        profileSelectContainer.AddThemeConstantOverride("separation", 20);

        var selectedButtonOutline = new TextureRect { Texture = SelectedButtonOutline };
        vPopup.AddChild(selectedButtonOutline);

        ProfileSelectButton? initiallyOutlinedButton = null;
        void PositionOutline(ProfileSelectButton btn)
        {
            selectedButtonOutline.GlobalPosition =
                btn.GetGlobalRect().GetCenter() - (selectedButtonOutline.Size / 2);
        }

        for (int i = 1; i <= 3; i++)
        {
            int profileId = i;
            var btn = ProfileSelectButtonScene.Instantiate<ProfileSelectButton>();
            btn.Initialize(
                profileId,
                () =>
                {
                    selectedProfileId = profileId;
                    UpdateLoc(selectedProfileId, _profileId);
                    vPopup.SetText(TitleLoc, DescLoc);

                    PositionOutline(btn);
                }
            );
            if (_profileId == profileId)
            {
                initiallyOutlinedButton = btn;
            }
            profileSelectContainer.AddChild(btn);
        }

        vPopup.AddChild(profileSelectContainer);
        profileSelectContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        profileSelectContainer.Position += 218 * Vector2.Down;

        vPopup.SetText(TitleLoc, DescLoc);

        profileSelectContainer.OnReady(() =>
            Callable.From(() => PositionOutline(initiallyOutlinedButton!)).CallDeferred()
        );

        var confirmationTask = new TaskCompletionSource<bool>();

        vPopup.InitNoButton(PopupCancelLoc, (_) => { });
        vPopup.InitYesButton(ConfirmLoc, (_) => { });
        vPopup.DisconnectSignals();

        vPopup.NoButton.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(
                (_) =>
                {
                    if (_isImporting)
                        return;
                    nGenericPopup.QueueFree();
                    confirmationTask.TrySetResult(false);
                    NModalContainer.Instance?.Clear();
                }
            )
        );

        vPopup.YesButton.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(
                (_) =>
                {
                    if (_isImporting)
                        return;
                    _isImporting = true;

                    profileSelectContainer.Visible = false;
                    selectedButtonOutline.Visible = false;
                    vPopup.YesButton.Visible = false;
                    vPopup.NoButton.Visible = false;

                    TaskHelper.RunSafely(
                        ClosePopup(
                            nGenericPopup,
                            vPopup,
                            confirmationTask,
                            selectedProfileId,
                            _profileId
                        )
                    );
                }
            )
        );

        await confirmationTask.Task;
    }

    private async Task ClosePopup(
        NGenericPopup popup,
        NVerticalPopup vPopup,
        TaskCompletionSource<bool> tcs,
        int vanillaProfileId,
        int moddedProfileId
    )
    {
        using var cts = new CancellationTokenSource();
        Task animationTask = AnimatePopupText(vPopup, WorkingLoc, cts.Token);

        try
        {
            await Task.Run(() => PerformTransferIO(vanillaProfileId, moddedProfileId));
        }
        finally
        {
            cts.Cancel();
            await animationTask;
            _isImporting = false;
        }

        popup.QueueFree();
        NModalContainer.Instance?.Clear();
        tcs.TrySetResult(true);

        NGame.Instance!.ReloadMainMenu();
        Callable.From(NGame.Instance.MainMenu!.OpenProfileScreen).CallDeferred();
    }

    private static async Task AnimatePopupText(
        NVerticalPopup vPopup,
        LocString workingMessage,
        CancellationToken token
    )
    {
        if (!IsInstanceValid(vPopup.BodyLabel))
            return;

        string baseText = workingMessage.GetFormattedText();

        int dots = 1;
        while (!token.IsCancellationRequested)
        {
            if (!IsInstanceValid(vPopup.BodyLabel))
                break;
            vPopup.BodyLabel.SetTextAutoSize(baseText + new string('.', dots));

            dots = (dots % 3) + 1;
            try
            {
                await Task.Delay(400, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static readonly MethodInfo? GetHistoryPathMethod =
        AccessTools.Method(
            typeof(RunHistorySaveManager),
            nameof(RunHistorySaveManager.GetHistoryPath),
            [typeof(int), typeof(bool?)]
        )
        ?? AccessTools.Method(
            typeof(RunHistorySaveManager),
            nameof(RunHistorySaveManager.GetHistoryPath),
            [typeof(int)]
        );

    public static string GetHistoryPath(int profileId, bool? forceModState = null)
    {
        if (GetHistoryPathMethod == null)
            throw new MissingMethodException();

        if (GetHistoryPathMethod.GetParameters().Length == 2)
            return (string)GetHistoryPathMethod.Invoke(null, [profileId, forceModState])!;

        if (!forceModState.HasValue)
            return (string)GetHistoryPathMethod.Invoke(null, [profileId])!;

        bool prev = LoadVanillaSavesPatch.ShouldLoadVanillaSaves;
        LoadVanillaSavesPatch.ShouldLoadVanillaSaves = !forceModState.Value;
        try
        {
            return (string)GetHistoryPathMethod.Invoke(null, [profileId])!;
        }
        finally
        {
            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = prev;
        }
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", Vector2.One * 1.1f, 0.05);
        _tween.TweenProperty(this, "modulate", new Color(1.25f, 1.25f, 1.25f, 1), 0.05);
        _tween.TweenProperty(_label, "modulate:a", 1f, 0.2);
        _tween
            .TweenProperty(_label, "position:y", 78f, 0.2)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic)
            .From(48f);
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
        _tween
            .TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1), 0.05)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _tween.TweenProperty(_label, "modulate:a", 0f, 0.05);
    }
}
