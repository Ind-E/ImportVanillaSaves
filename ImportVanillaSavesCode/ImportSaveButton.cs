using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public partial class ImportSaveButton : NButton
{
    public void Initialize(int profileId)
    {
        _profileId = profileId;
    }

    private static readonly LocString _title = new(
        "main_menu_ui",
        "IMPORT_VANILLA_SAVES-IMPORT_CONFIRM_POPUP.title"
    );

    private static readonly LocString _description = new(
        "main_menu_ui",
        "IMPORT_VANILLA_SAVES-IMPORT_CONFIRM_POPUP.description"
    );

    private static readonly LocString _buttonMesssage = new(
        "main_menu_ui",
        "IMPORT_VANILLA_SAVES-IMPORT_BUTTON.label"
    );

    private static readonly LocString _importingMessage = new(
        "main_menu_ui",
        "IMPORT_VANILLA_SAVES-IMPORT_CONFIRM_POPUP.importing"
    );

    private static readonly LocString _popupCancel = new(
        "main_menu_ui",
        "PROFILE_SCREEN.DELETE_CONFIRM_POPUP.cancel"
    );

    private static readonly LocString _popupConfirm = new(
        "main_menu_ui",
        "IMPORT_VANILLA_SAVES-IMPORT_CONFIRM_POPUP.confirm"
    );

    private TextureRect? _icon;

    private MegaLabel? _label;

    private Tween? _tween;

    private int _associatedDeleteButtonIndex;

    private int _profileId;

    private bool _isImporting;

    private static readonly AccessTools.FieldRef<
        SaveManager,
        ProfileSaveManager
    > ProfileSaveManagerRef = AccessTools.FieldRefAccess<SaveManager, ProfileSaveManager>(
        "_profileSaveManager"
    );

    private static readonly AccessTools.FieldRef<SaveManager, ISaveStore> SaveStoreRef =
        AccessTools.FieldRefAccess<SaveManager, ISaveStore>("_saveStore");

    private static readonly AccessTools.FieldRef<
        SaveManager,
        RunHistorySaveManager
    > RunHistorySaveManagerRef = AccessTools.FieldRefAccess<SaveManager, RunHistorySaveManager>(
        "_runHistorySaveManager"
    );

    private static readonly Func<NVerticalPopup, MegaRichTextLabel> GetBodyLabel =
        (Func<NVerticalPopup, MegaRichTextLabel>)
            Delegate.CreateDelegate(
                typeof(Func<NVerticalPopup, MegaRichTextLabel>),
                AccessTools.PropertyGetter(typeof(NVerticalPopup), "BodyLabel")
            );

    public override void _Ready()
    {
        ConnectSignals();
        _label = GetNode<MegaLabel>("%MegaLabel");
        _label.SetTextAutoSize(_buttonMesssage.GetFormattedText());
        _icon = GetNode<TextureRect>("Icon");
    }

    protected override void OnRelease()
    {
        if (_isImporting)
            return;
        TaskHelper.RunSafely(ConfirmImport());
    }

    private async Task ConfirmImport()
    {
        NGenericPopup nGenericPopup = NGenericPopup.Create()!;
        NModalContainer.Instance!.Add(nGenericPopup);
        _title.Add("Id", _profileId);
        _description.Add("Id", _profileId);

        var vPopup = nGenericPopup.GetNode<NVerticalPopup>("VerticalPopup");
        vPopup.SetText(_title, _description);

        var confirmationTask = new TaskCompletionSource<bool>();

        vPopup.InitNoButton(_popupCancel, (_) => { });
        vPopup.InitYesButton(_popupConfirm, (_) => { });

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

                    vPopup.YesButton.Visible = false;
                    vPopup.NoButton.Visible = false;

                    TaskHelper.RunSafely(
                        RunImportAndClose(nGenericPopup, vPopup, confirmationTask)
                    );
                }
            )
        );

        await confirmationTask.Task;
    }

    private async Task RunImportAndClose(
        NGenericPopup popup,
        NVerticalPopup vPopup,
        TaskCompletionSource<bool> tcs
    )
    {
        using var cts = new CancellationTokenSource();
        Task animationTask = AnimatePopupText(vPopup, cts.Token);

        try
        {
            await Task.Run(PerformImportIO);
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

    private async Task AnimatePopupText(NVerticalPopup vPopup, CancellationToken token)
    {
        var bodyLabel = GetBodyLabel(vPopup);
        if (!IsInstanceValid(bodyLabel))
            return;

        string baseDesc = _description.GetFormattedText();
        string importingSuffix = "\n\n" + _importingMessage.GetFormattedText();

        int dots = 1;
        while (!token.IsCancellationRequested)
        {
            if (!IsInstanceValid(bodyLabel))
                break;
            bodyLabel.SetTextAutoSize(baseDesc + importingSuffix + new string('.', dots));

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

    private void PerformImportIO()
    {
        var profileSaveManager = ProfileSaveManagerRef(SaveManager.Instance);
        var saveStore = SaveStoreRef(SaveManager.Instance);
        var runHistoryManager = RunHistorySaveManagerRef(SaveManager.Instance);
        var historyContents = new Dictionary<string, string>();

        LoadVanillaSavesPatch.ShouldLoadVanillaSaves = true;
        try
        {
            SaveManager.Instance.SwitchProfileId(_profileId);
            profileSaveManager.LoadProfile();
            SaveManager.Instance.InitProgressData();
            SaveManager.Instance.InitPrefsData();

            string vanillaHistoryDir = RunHistorySaveManager.GetHistoryPath(_profileId);
            if (saveStore.DirectoryExists(vanillaHistoryDir))
            {
                foreach (string fileName in saveStore.GetFilesInDirectory(vanillaHistoryDir))
                {
                    string filePath = Path.Combine(vanillaHistoryDir, fileName);
                    string? content = saveStore.ReadFile(filePath);
                    if (!string.IsNullOrEmpty(content))
                        historyContents[fileName] = content;
                }
            }
        }
        finally
        {
            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = false;
        }

        SaveManager.Instance.SaveProfile();
        SaveManager.Instance.SaveProgressFile();
        SaveManager.Instance.SavePrefsFile();

        runHistoryManager.CreateRunHistoryDirectory();
        string moddedHistoryDir = RunHistorySaveManager.GetHistoryPath(_profileId);
        foreach (var entry in historyContents)
        {
            saveStore.WriteFile(Path.Combine(moddedHistoryDir, entry.Key), entry.Value);
        }
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", Vector2.One * 1.1f, 0.05);
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
        _tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        _tween.TweenProperty(_label, "modulate:a", 0f, 0.05);
    }
}
