using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public partial class ExportModdedSaveButton : AbstractSaveButton
{
    protected override LocString TitleLoc { get; } = MkLocStr("EXPORT_CONFIRM_POPUP.title");
    protected override LocString DescLoc { get; } = MkLocStr("EXPORT_CONFIRM_POPUP.description");
    protected override LocString ButtonLabelLoc { get; } = MkLocStr("EXPORT_BUTTON.label");
    protected override LocString WorkingLoc { get; } = MkLocStr("EXPORT_CONFIRM_POPUP.exporting");
    protected override LocString ConfirmLoc { get; } = MkLocStr("EXPORT_CONFIRM_POPUP.confirm");

    protected override void PerformTransferIO(int vanillaProfileId, int moddedProfileId)
    {
        var profileSaveManager = SaveManager.Instance._profileSaveManager;
        var progressSaveManager = SaveManager.Instance._progressSaveManager;
        var migrationManager = progressSaveManager._migrationManager;
        var profileIdProvider = progressSaveManager._profileIdProvider;
        var saveStore = SaveManager.Instance._saveStore;

        int previousProfileId = profileIdProvider.CurrentProfileId;

        SerializableProgress sourceProgress;
        if (moddedProfileId == previousProfileId)
        {
            sourceProgress = progressSaveManager.Progress.ToSerializable();
        }
        else
        {
            string moddedProgressPath = ProgressSaveManager.GetProgressPathForProfile(
                moddedProfileId,
                forceModState: true
            );
            var readProgress = migrationManager.LoadSave<SerializableProgress>(moddedProgressPath);
            sourceProgress = readProgress is { Success: true, SaveData: not null }
                ? readProgress.SaveData
                : ProgressState.CreateDefault().ToSerializable();
        }

        SerializableProgress filteredProgress = VanillaSaveFilter.FilterProgress(
            sourceProgress,
            migrationManager.GetLatestVersion<SerializableProgress>()
        );
        string filteredProgressJson = JsonSerializationUtility.ToJson(filteredProgress);

        try
        {
            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = false;
            SaveManager.Instance.SwitchProfileId(moddedProfileId);
            profileSaveManager.LoadProfile();
            SaveManager.Instance.InitPrefsData();

            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = true;
            SaveManager.Instance.SwitchProfileId(vanillaProfileId);
            SaveManager.Instance.SaveProfile();
            SaveManager.Instance.SavePrefsFile();

            string vanillaProgressPath = ProgressSaveManager.GetProgressPathForProfile(
                vanillaProfileId,
                forceModState: false
            );
            saveStore.WriteFile(vanillaProgressPath, filteredProgressJson);
        }
        finally
        {
            LoadVanillaSavesPatch.ShouldLoadVanillaSaves = false;
            SaveManager.Instance.SwitchProfileId(previousProfileId);
            profileSaveManager.LoadProfile();
            SaveManager.Instance.InitProgressData();
            SaveManager.Instance.InitPrefsData();
        }
    }
}
