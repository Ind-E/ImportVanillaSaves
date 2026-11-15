using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public partial class ImportVanillaSaveButton : AbstractSaveButton
{
    protected override LocString TitleLoc { get; } = MkLocStr("IMPORT_CONFIRM_POPUP.title");
    protected override LocString DescLoc { get; } = MkLocStr("IMPORT_CONFIRM_POPUP.description");
    protected override LocString ButtonLabelLoc { get; } = MkLocStr("IMPORT_BUTTON.label");
    protected override LocString WorkingLoc { get; } = MkLocStr("IMPORT_CONFIRM_POPUP.importing");
    protected override LocString ConfirmLoc { get; } = MkLocStr("IMPORT_CONFIRM_POPUP.confirm");

    protected override void PerformTransferIO(int vanillaProfileId, int moddedProfileId)
    {
        var profileSaveManager = SaveManager.Instance._profileSaveManager;
        var saveStore = SaveManager.Instance._saveStore;
        var runHistoryManager = SaveManager.Instance._runHistorySaveManager;
        var historyContents = new Dictionary<string, string>();

        LoadVanillaSavesPatch.ShouldLoadVanillaSaves = true;
        try
        {
            SaveManager.Instance.SwitchProfileId(vanillaProfileId);
            profileSaveManager.LoadProfile();
            SaveManager.Instance.InitProgressData();
            SaveManager.Instance.InitPrefsData();

            string vanillaHistoryDir = GetHistoryPath(vanillaProfileId);
            if (saveStore.DirectoryExists(vanillaHistoryDir))
            {
                foreach (string fileName in saveStore.GetFilesInDirectory(vanillaHistoryDir))
                {
                    string filePath = Path.Combine(vanillaHistoryDir, fileName);
                    if (!filePath.EndsWith("run"))
                        continue;

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

        SaveManager.Instance.SwitchProfileId(moddedProfileId);

        SaveManager.Instance.SaveProfile();
        SaveManager.Instance.SaveProgressFile();
        SaveManager.Instance.SavePrefsFile();

        runHistoryManager.CreateRunHistoryDirectory();
        string moddedHistoryDir = GetHistoryPath(moddedProfileId);
        foreach (var entry in historyContents)
        {
            saveStore.WriteFile(Path.Combine(moddedHistoryDir, entry.Key), entry.Value);
        }
    }
}
