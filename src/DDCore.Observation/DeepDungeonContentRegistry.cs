using System.Collections.Immutable;
using DDCore.Core.World;

namespace DDCore.Observation;

public sealed record DeepDungeonContentDefinition(
    uint ContentFinderConditionId,
    uint TerritoryId,
    uint DataContentId,
    string ShortCode,
    DeepDungeonKind Kind,
    DeepDungeonFloorRange FloorRange);

/// <summary>
/// Checked game-data manifest for public-api Deep Dungeon classification.
/// Source: xivapi/ffxiv-datamining commit 44948e4cda5846eab0baa9872122a888fcc4f1b1,
/// csv/en/ContentFinderCondition.csv. Runtime classification requires every public row value to match.
/// </summary>
public static class DeepDungeonContentRegistry
{
    public const string DataRevision = "44948e4cda5846eab0baa9872122a888fcc4f1b1";

    public static ImmutableArray<DeepDungeonContentDefinition> Definitions { get; } =
    [
        D(174, 561, 60001, "f1c1_dd001", DeepDungeonKind.PalaceOfTheDead, 1),
        D(175, 562, 60002, "f1c2_dd002", DeepDungeonKind.PalaceOfTheDead, 11),
        D(176, 563, 60003, "f1c3_dd003", DeepDungeonKind.PalaceOfTheDead, 21),
        D(177, 564, 60004, "f1c4_dd004", DeepDungeonKind.PalaceOfTheDead, 31),
        D(178, 565, 60005, "f1c4_dd005", DeepDungeonKind.PalaceOfTheDead, 41),
        D(204, 593, 60006, "f1c5_dd006", DeepDungeonKind.PalaceOfTheDead, 51),
        D(205, 594, 60007, "f1c5_dd007", DeepDungeonKind.PalaceOfTheDead, 61),
        D(206, 595, 60008, "f1c5_dd008", DeepDungeonKind.PalaceOfTheDead, 71),
        D(207, 596, 60009, "f1c6_dd009", DeepDungeonKind.PalaceOfTheDead, 81),
        D(208, 597, 60010, "f1c6_dd010", DeepDungeonKind.PalaceOfTheDead, 91),
        D(209, 598, 60011, "f1c6_dd011", DeepDungeonKind.PalaceOfTheDead, 101),
        D(210, 599, 60012, "f1c8_dd012", DeepDungeonKind.PalaceOfTheDead, 111),
        D(211, 600, 60013, "f1c8_dd013", DeepDungeonKind.PalaceOfTheDead, 121),
        D(212, 601, 60014, "f1c9_dd014", DeepDungeonKind.PalaceOfTheDead, 131),
        D(213, 602, 60015, "f1c9_dd015", DeepDungeonKind.PalaceOfTheDead, 141),
        D(214, 603, 60016, "f1c7_dd016", DeepDungeonKind.PalaceOfTheDead, 151),
        D(215, 604, 60017, "f1c7_dd017", DeepDungeonKind.PalaceOfTheDead, 161),
        D(216, 605, 60018, "f1c7_dd018", DeepDungeonKind.PalaceOfTheDead, 171),
        D(217, 606, 60019, "f1c7_dd019", DeepDungeonKind.PalaceOfTheDead, 181),
        D(218, 607, 60020, "f1c7_dd020", DeepDungeonKind.PalaceOfTheDead, 191),
        D(540, 770, 60021, "dd002_001", DeepDungeonKind.HeavenOnHigh, 1),
        D(541, 771, 60022, "dd002_002", DeepDungeonKind.HeavenOnHigh, 11),
        D(542, 772, 60023, "dd002_003", DeepDungeonKind.HeavenOnHigh, 21),
        D(543, 782, 60024, "dd002_004", DeepDungeonKind.HeavenOnHigh, 31),
        D(544, 773, 60025, "dd002_005", DeepDungeonKind.HeavenOnHigh, 41),
        D(545, 783, 60026, "dd002_006", DeepDungeonKind.HeavenOnHigh, 51),
        D(546, 774, 60027, "dd002_007", DeepDungeonKind.HeavenOnHigh, 61),
        D(547, 784, 60028, "dd002_008", DeepDungeonKind.HeavenOnHigh, 71),
        D(548, 775, 60029, "dd002_009", DeepDungeonKind.HeavenOnHigh, 81),
        D(549, 785, 60030, "dd002_010", DeepDungeonKind.HeavenOnHigh, 91),
        D(897, 1099, 60031, "dd003_001", DeepDungeonKind.EurekaOrthos, 1),
        D(898, 1100, 60032, "dd003_002", DeepDungeonKind.EurekaOrthos, 11),
        D(899, 1101, 60033, "dd003_003", DeepDungeonKind.EurekaOrthos, 21),
        D(900, 1102, 60034, "dd003_004", DeepDungeonKind.EurekaOrthos, 31),
        D(901, 1103, 60035, "dd003_005", DeepDungeonKind.EurekaOrthos, 41),
        D(902, 1104, 60036, "dd003_006", DeepDungeonKind.EurekaOrthos, 51),
        D(903, 1105, 60037, "dd003_007", DeepDungeonKind.EurekaOrthos, 61),
        D(904, 1106, 60038, "dd003_008", DeepDungeonKind.EurekaOrthos, 71),
        D(905, 1107, 60039, "dd003_009", DeepDungeonKind.EurekaOrthos, 81),
        D(906, 1108, 60040, "dd003_010", DeepDungeonKind.EurekaOrthos, 91),
    ];

    private static readonly IReadOnlyDictionary<uint, DeepDungeonContentDefinition> ByContentFinderCondition =
        Definitions.ToDictionary(item => item.ContentFinderConditionId);

    public static bool TryResolve(
        uint? observedTerritoryId,
        uint? contentFinderConditionId,
        RawContentFinderConditionObservation? row,
        out DeepDungeonContentDefinition? definition,
        out string reasonCode)
    {
        definition = null;
        if (contentFinderConditionId is not { } conditionId || conditionId == 0)
        {
            reasonCode = "observation.dungeon.content-finder-condition-missing";
            return false;
        }

        if (!ByContentFinderCondition.TryGetValue(conditionId, out var expected))
        {
            reasonCode = "observation.dungeon.content-finder-condition-unknown";
            return false;
        }

        if (observedTerritoryId != expected.TerritoryId)
        {
            reasonCode = "observation.dungeon.territory-mismatch";
            return false;
        }

        if (row is null)
        {
            reasonCode = "observation.dungeon.game-data-row-missing";
            return false;
        }

        if (row.RowId != expected.ContentFinderConditionId
            || row.ReferencedTerritoryId != expected.TerritoryId
            || row.DataContentId != expected.DataContentId
            || !string.Equals(row.ShortCode, expected.ShortCode, StringComparison.Ordinal))
        {
            reasonCode = "observation.dungeon.game-data-row-mismatch";
            return false;
        }

        definition = expected;
        reasonCode = "observation.dungeon.public-identified";
        return true;
    }

    private static DeepDungeonContentDefinition D(
        uint contentFinderConditionId,
        uint territoryId,
        uint dataContentId,
        string shortCode,
        DeepDungeonKind kind,
        int floorStart) =>
        new(
            contentFinderConditionId,
            territoryId,
            dataContentId,
            shortCode,
            kind,
            new DeepDungeonFloorRange(floorStart, floorStart + 9));
}
