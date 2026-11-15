using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public static class VanillaSaveFilter
{
    private static readonly Assembly VanillaAssembly = typeof(ModelDb).Assembly;

    private static readonly HashSet<string> VanillaAchievementNames = Enum.GetValues<Achievement>()
        .Select(a => JsonNamingPolicy.SnakeCaseLower.ConvertName(a.ToString()))
        .ToHashSet();

    public static bool IsVanillaModel<TModel>(ModelId? id)
        where TModel : AbstractModel
    {
        if (id is null || id == ModelId.none)
            return false;

        TModel? model = ModelDb.GetByIdOrNull<TModel>(id);
        return model != null && model.GetType().Assembly == VanillaAssembly;
    }

    public static bool IsVanillaEpoch(string epochId)
    {
        if (!EpochModel.IsValid(epochId))
            return false;

        return EpochModel.Get(epochId).GetType().Assembly == VanillaAssembly;
    }

    public static SerializableProgress FilterProgress(
        SerializableProgress source,
        int schemaVersion
    )
    {
        static ModelId FilterCharacterId(ModelId id) =>
            IsVanillaModel<CharacterModel>(id) ? id : ModelId.none;

        static PendingDreadWinStreakResult? FilterPendingDread(PendingDreadWinStreakResult? pending)
        {
            if (pending == null || !IsVanillaModel<CharacterModel>(pending.CharacterId))
                return null;

            return new PendingDreadWinStreakResult
            {
                CharacterId = pending.CharacterId,
                WasReset = pending.WasReset,
                InitialRotatingCharacterBeforeReset = FilterCharacterId(
                    pending.InitialRotatingCharacterBeforeReset
                ),
                WinStreakBeforeReset = pending.WinStreakBeforeReset,
                WinStreakIncrease = pending.WinStreakIncrease,
            };
        }

        var filteredEpochs = source.Epochs.Where(e => IsVanillaEpoch(e.Id)).ToList();

        var obtainedEpochIds = filteredEpochs
            .Where(e => e.State >= EpochState.ObtainedNoSlot)
            .Select(e => e.Id)
            .ToHashSet();

        return new SerializableProgress
        {
            UniqueId = source.UniqueId,
            SchemaVersion = schemaVersion,
            EnableFtues = source.EnableFtues,
            TotalPlaytime = source.TotalPlaytime,
            TotalUnlocks = EpochModel.AgnosticUnlockOrder.Count(id =>
                IsVanillaEpoch(id) && obtainedEpochIds.Contains(id)
            ),
            CurrentScore = source.CurrentScore,
            FloorsClimbed = source.FloorsClimbed,
            ArchitectDamage = source.ArchitectDamage,
            WongoPoints = source.WongoPoints,
            PreferredMultiplayerAscension = source.PreferredMultiplayerAscension,
            MaxMultiplayerAscension = source.MaxMultiplayerAscension,
            PreferredBlitzAscension = source.PreferredBlitzAscension,
            MaxBlitzAscension = source.MaxBlitzAscension,
            TestSubjectKills = source.TestSubjectKills,

            PendingCharacterUnlock = FilterCharacterId(source.PendingCharacterUnlock),
            PreferredStandardCharacter = FilterCharacterId(source.PreferredStandardCharacter),
            PreferredMultiplayerStandardCharacter = FilterCharacterId(
                source.PreferredMultiplayerStandardCharacter
            ),
            PreferredBlitzCharacter = FilterCharacterId(source.PreferredBlitzCharacter),
            PreferredMultiplayerBlitzCharacter = FilterCharacterId(
                source.PreferredMultiplayerBlitzCharacter
            ),
            PreferredDreadCharacter = FilterCharacterId(source.PreferredDreadCharacter),
            PreferredMultiplayerDreadCharacter = FilterCharacterId(
                source.PreferredMultiplayerDreadCharacter
            ),
            CurrentSingleplayerDreadStartingCharacterForRotation = FilterCharacterId(
                source.CurrentSingleplayerDreadStartingCharacterForRotation
            ),
            CurrentMultiplayerDreadStartingCharacterForRotation = FilterCharacterId(
                source.CurrentMultiplayerDreadStartingCharacterForRotation
            ),
            PendingSingleplayerDreadWinStreakResult = FilterPendingDread(
                source.PendingSingleplayerDreadWinStreakResult
            ),
            PendingMultiplayerDreadWinStreakResult = FilterPendingDread(
                source.PendingMultiplayerDreadWinStreakResult
            ),

            CharStats = source.CharStats.Where(s => IsVanillaModel<CharacterModel>(s.Id)).ToList(),

            CardStats = source.CardStats.Where(s => IsVanillaModel<CardModel>(s.Id)).ToList(),

            EncounterStats = source
                .EncounterStats.Where(s => IsVanillaModel<EncounterModel>(s.Id))
                .Select(s => new EncounterStats
                {
                    Id = s.Id,
                    FightStats = s
                        .FightStats.Where(f => IsVanillaModel<CharacterModel>(f.Character))
                        .ToList(),
                })
                .ToList(),

            EnemyStats = source
                .EnemyStats.Where(s => IsVanillaModel<MonsterModel>(s.Id))
                .Select(s => new EnemyStats
                {
                    Id = s.Id,
                    FightStats = s
                        .FightStats.Where(f => IsVanillaModel<CharacterModel>(f.Character))
                        .ToList(),
                })
                .ToList(),

            AncientStats = source
                .AncientStats.Where(s => IsVanillaModel<EventModel>(s.Id))
                .Select(s => new AncientStats
                {
                    Id = s.Id,
                    CharStats = s
                        .CharStats.Where(c => IsVanillaModel<CharacterModel>(c.Character))
                        .ToList(),
                })
                .ToList(),

            DiscoveredCards = source.DiscoveredCards.Where(IsVanillaModel<CardModel>).ToList(),
            DiscoveredRelics = source.DiscoveredRelics.Where(IsVanillaModel<RelicModel>).ToList(),
            DiscoveredPotions = source
                .DiscoveredPotions.Where(IsVanillaModel<PotionModel>)
                .ToList(),
            DiscoveredEvents = source.DiscoveredEvents.Where(IsVanillaModel<EventModel>).ToList(),
            DiscoveredActs = source.DiscoveredActs.Where(IsVanillaModel<ActModel>).ToList(),

            Epochs = filteredEpochs,
            FtueCompleted = source.FtueCompleted.ToList(),
            UnlockedAchievements = source
                .UnlockedAchievements.Where(a => VanillaAchievementNames.Contains(a.Achievement))
                .ToList(),
        };
    }
}
