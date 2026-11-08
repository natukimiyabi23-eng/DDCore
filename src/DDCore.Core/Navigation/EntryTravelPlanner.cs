using System.Collections.Immutable;

namespace DDCore.Core.Navigation;

public static class EurekaOrthosEntryCatalog
{
    public static EntryTravelLandmark Burnel { get; } = new(
        "eureka-orthos.burnel",
        "バーネル",
        "モードゥナ：レヴナンツトール",
        new MapCoordinate(21.8m, 8.1m));

    public static EntryTravelLandmark Khatun { get; } = new(
        "eureka-orthos.khatun",
        "カトゥン",
        "モードゥナ：八剣士の前庭",
        new MapCoordinate(34.8m, 19.2m));
}

public sealed class EntryTravelPlanner
{
    private const string PreviewOnlyReason =
        "表示用マップ座標を確認済みですが、ワールド座標変換と移動アダプターは未接続です。";

    public EntryTravelPlan Plan(DeepDungeonEntryDestination destination, EntryTravelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(destination)
            || !Enum.IsDefined(context.CurrentArea)
            || !Enum.IsDefined(context.Access)
            || !Enum.IsDefined(context.Teleport))
        {
            return Blocked(destination, "現在地または移動条件に未定義の値があります。");
        }

        if (destination != DeepDungeonEntryDestination.EurekaOrthos)
        {
            return Blocked(destination, "この段階ではオルト・エウレカ入口だけが設計対象です。");
        }

        if (context.Access == EntryAccessState.Unknown)
        {
            return Blocked(destination, "オルト・エウレカの解放状態を確認できません。");
        }

        if (context.Access == EntryAccessState.Locked)
        {
            return Blocked(destination, "オルト・エウレカが未解放です。");
        }

        if (context.CurrentArea == EntryTravelArea.Unknown)
        {
            return Blocked(destination, "現在地を確認できません。");
        }

        var steps = ImmutableArray.CreateBuilder<EntryTravelStep>();
        if (context.CurrentArea == EntryTravelArea.Other)
        {
            if (context.Teleport != TeleportAvailability.Available)
            {
                return Blocked(destination, context.Teleport == TeleportAvailability.Unknown
                    ? "レヴナンツトールへのテレポ可否を確認できません。"
                    : "レヴナンツトールへのテレポ手段がありません。");
            }

            steps.Add(new(
                EntryTravelStepKind.TeleportToHub,
                "レヴナンツトールへ移動する。",
                null,
                RequiresExternalSideEffect: true));
        }

        if (context.CurrentArea != EntryTravelArea.MorDhonaEightSentinels)
        {
            steps.Add(new(
                EntryTravelStepKind.NavigateToLandmark,
                "レヴナンツトールのバーネルへ向かう。",
                EurekaOrthosEntryCatalog.Burnel,
                RequiresExternalSideEffect: true));
            steps.Add(new(
                EntryTravelStepKind.UseAreaTransition,
                "解放後の近道を使って八剣士の前庭へ移動する。",
                EurekaOrthosEntryCatalog.Burnel,
                RequiresExternalSideEffect: true));
        }

        steps.Add(new(
            EntryTravelStepKind.NavigateToLandmark,
            "オルト・エウレカ受付のカトゥンへ向かう。",
            EurekaOrthosEntryCatalog.Khatun,
            RequiresExternalSideEffect: true));
        steps.Add(new(
            EntryTravelStepKind.VerifyArrival,
            "カトゥン付近への到着を観測情報で確認する。",
            EurekaOrthosEntryCatalog.Khatun,
            RequiresExternalSideEffect: false));

        return Build(destination, steps.ToImmutable(), false, PreviewOnlyReason);
    }

    private static EntryTravelPlan Blocked(DeepDungeonEntryDestination destination, string reason) =>
        Build(destination, ImmutableArray<EntryTravelStep>.Empty, false, reason);

    private static EntryTravelPlan Build(
        DeepDungeonEntryDestination destination,
        ImmutableArray<EntryTravelStep> steps,
        bool executable,
        string reason)
    {
        var plan = new EntryTravelPlan(destination, steps, executable, reason);
        plan.Validate();
        return plan;
    }
}
