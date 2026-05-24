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

    private TextureRect? _icon;

    private MegaLabel? _label;

    private Tween? _tween;

    private int _associatedDeleteButtonIndex;

    private int _profileId;

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

    public override void _Ready()
    {
        ConnectSignals();
        _label = GetNode<MegaLabel>("%MegaLabel");
        _label.AddThemeFontOverride(
            "font",
            GD.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres")
        );
        _label.SetTextAutoSize(_buttonMesssage.GetFormattedText());
        _icon = GetNode<TextureRect>("Icon");
    }

    protected override void OnRelease()
    {
        TaskHelper.RunSafely(ConfirmImport());
    }

    private async Task ConfirmImport()
    {
        NGenericPopup nGenericPopup = NGenericPopup.Create()!;
        NModalContainer.Instance!.Add(nGenericPopup);
        _title.Add("Id", _profileId);
        _description.Add("Id", _profileId);
        if (
            await nGenericPopup.WaitForConfirmation(
                _description,
                _title,
                new LocString("main_menu_ui", "PROFILE_SCREEN.DELETE_CONFIRM_POPUP.cancel"),
                new LocString("main_menu_ui", "IMPORT_VANILLA_SAVES-IMPORT_CONFIRM_POPUP.confirm")
            )
        )
        {
            var profileSaveManager = ProfileSaveManagerRef(SaveManager.Instance);
            var saveStore = SaveStoreRef(SaveManager.Instance);
            var runHistoryManager = RunHistorySaveManagerRef(SaveManager.Instance);

            var historyContents = new Dictionary<string, string>();

            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = true;
            try
            {
                MainFile.Logger.Info($"Loading vanilla save data for profile {_profileId}");

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
                        {
                            historyContents[fileName] = content;
                        }
                    }
                }
            }
            finally
            {
                LoadVanillaSavesPatch.ShouldLoadVanillaSaves = false;
            }

            MainFile.Logger.Info($"Saving vanilla data to modded for profile {_profileId}");

            SaveManager.Instance.SaveProfile();
            SaveManager.Instance.SaveProgressFile();
            SaveManager.Instance.SavePrefsFile();

            runHistoryManager.CreateRunHistoryDirectory();
            string moddedHistoryDir = RunHistorySaveManager.GetHistoryPath(_profileId);

            foreach (var entry in historyContents)
            {
                string destinationPath = Path.Combine(moddedHistoryDir, entry.Key);
                saveStore.WriteFile(destinationPath, entry.Value);
            }

            NGame.Instance!.ReloadMainMenu();
            Callable.From(NGame.Instance.MainMenu!.OpenProfileScreen).CallDeferred();
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
