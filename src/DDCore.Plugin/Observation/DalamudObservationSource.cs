using System.Collections.Immutable;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DDCore.Observation;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudObservationSource(
    IClientState clientState,
    IObjectTable objectTable,
    IDutyState dutyState,
    ICondition condition) : ILiveObservationSource
{
    public RawObservationFrame Capture()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        if (!clientState.IsLoggedIn)
        {
            return new RawObservationFrame(
                capturedAt,
                false,
                null,
                null,
                false,
                null,
                [],
                ["dalamud.client.not-logged-in"],
                RawPlayerPoseObservation.Missing("dalamud.client.not-logged-in"));
        }

        var actors = ImmutableArray.CreateBuilder<RawActorObservation>();
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        uint? localPlayerEntityId = null;
        RawPlayerPoseObservation playerPose;
        try
        {
            var localPlayer = objectTable.LocalPlayer;
            localPlayerEntityId = localPlayer?.EntityId;
            if (localPlayer is null)
            {
                playerPose = RawPlayerPoseObservation.Missing("dalamud.player.not-created");
                diagnostics.Add("dalamud.player.not-created");
            }
            else
            {
                var position = localPlayer.Position;
                var rotation = localPlayer.Rotation;
                if (!float.IsFinite(position.X)
                    || !float.IsFinite(position.Y)
                    || !float.IsFinite(position.Z)
                    || !float.IsFinite(rotation))
                {
                    playerPose = RawPlayerPoseObservation.Missing("dalamud.player.pose-invalid");
                    diagnostics.Add("dalamud.player.pose-invalid");
                }
                else
                {
                    playerPose = RawPlayerPoseObservation.Direct(
                        new RawPosition(position.X, position.Y, position.Z),
                        rotation);
                }
            }
        }
        catch (Exception exception)
        {
            playerPose = RawPlayerPoseObservation.Missing($"dalamud.player.read-failed:{exception.GetType().Name}");
            diagnostics.Add($"dalamud.player.read-failed:{exception.GetType().Name}");
        }

        foreach (var gameObject in objectTable)
        {
            if (gameObject is not ICharacter character)
            {
                continue;
            }

            var kind = gameObject.ObjectKind switch
            {
                ObjectKind.Pc when gameObject.EntityId == localPlayerEntityId => RawActorKind.Player,
                ObjectKind.Pc when character.StatusFlags.HasFlag(StatusFlags.PartyMember) => RawActorKind.PartyMember,
                ObjectKind.BattleNpc when gameObject is IBattleNpc { BattleNpcKind: BattleNpcSubKind.Combatant } => RawActorKind.Enemy,
                _ => RawActorKind.Unknown,
            };
            if (kind == RawActorKind.Unknown)
            {
                continue;
            }

            try
            {
                actors.Add(ConvertActor(gameObject, character, kind));
            }
            catch (Exception exception)
            {
                diagnostics.Add($"dalamud.actor.read-failed:{gameObject.EntityId:X8}:{exception.GetType().Name}");
            }
        }

        uint? contentId = null;
        RawContentFinderConditionObservation? contentFinderCondition = null;
        try
        {
            var conditionRow = dutyState.ContentFinderCondition;
            if (conditionRow.RowId != 0)
            {
                contentId = conditionRow.RowId;
                if (conditionRow.ValueNullable is { } row)
                {
                    contentFinderCondition = new RawContentFinderConditionObservation(
                        row.RowId,
                        row.TerritoryType.RowId,
                        row.Content.RowId,
                        row.ShortCode.ToString());
                }
                else
                {
                    diagnostics.Add("dalamud.content-finder-condition.row-unavailable");
                }
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"dalamud.content-finder-condition.read-failed:{exception.GetType().Name}");
        }

        var isInDeepDungeon = false;
        try
        {
            isInDeepDungeon = condition[ConditionFlag.InDeepDungeon];
        }
        catch (Exception exception)
        {
            diagnostics.Add($"dalamud.deep-dungeon.condition-read-failed:{exception.GetType().Name}");
        }

        diagnostics.Add("dalamud.cast.location.unavailable-public-api");
        diagnostics.Add("dalamud.cast.rotation.unavailable-public-api");
        diagnostics.Add("dalamud.deep-dungeon.floor.unavailable-public-api");
        diagnostics.Add("dalamud.status.stacks.unavailable-public-api");
        return new RawObservationFrame(
            capturedAt,
            true,
            clientState.TerritoryType == 0 ? null : clientState.TerritoryType,
            contentId,
            dutyState.IsDutyStarted,
            localPlayerEntityId,
            actors.ToImmutable(),
            diagnostics.ToImmutable(),
            playerPose,
            isInDeepDungeon,
            contentFinderCondition);
    }

    private static RawActorObservation ConvertActor(IGameObject gameObject, ICharacter character, RawActorKind kind)
    {
        var battleChara = gameObject as IBattleChara;
        var cast = battleChara is { IsCasting: true, CastActionId: not 0 }
            ? new RawCastObservation(
                battleChara.CastActionId,
                battleChara.CurrentCastTime,
                battleChara.TotalCastTime,
                battleChara.CastTargetObjectId == 0 ? null : battleChara.CastTargetObjectId)
            : null;
        var statuses = battleChara?.StatusList
            .Where(status => status.StatusId != 0)
            .Select(status => new RawStatusObservation(
                status.StatusId,
                status.SourceId == 0 ? null : status.SourceId,
                null,
                float.IsFinite(status.RemainingTime) && status.RemainingTime > 0 ? status.RemainingTime : null))
            .ToImmutableArray() ?? [];
        var position = gameObject.Position;
        return new RawActorObservation(
            gameObject.EntityId,
            gameObject.GameObjectId,
            gameObject.ObjectIndex,
            gameObject.BaseId,
            kind,
            new RawPosition(position.X, position.Y, position.Z),
            gameObject.Rotation,
            gameObject.HitboxRadius,
            character.CurrentHp,
            character.MaxHp,
            gameObject.IsDead,
            gameObject.IsValid(),
            character.StatusFlags.HasFlag(StatusFlags.InCombat),
            gameObject.TargetObjectId == 0 ? null : gameObject.TargetObjectId,
            cast,
            statuses);
    }
}
