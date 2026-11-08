using DDCore.Core.Geometry;

namespace DDCore.Core.Navigation;

public sealed record EntryTravelProgressPolicy(
    TimeSpan AuthorizationLifetime,
    TimeSpan StallTimeout,
    double MinimumProgressDistance)
{
    public void Validate()
    {
        if (AuthorizationLifetime <= TimeSpan.Zero
            || StallTimeout <= TimeSpan.Zero
            || !double.IsFinite(MinimumProgressDistance)
            || MinimumProgressDistance <= 0)
        {
            throw new InvalidDataException("Entry-travel progress policy contains an invalid limit.");
        }
    }
}

public sealed class EntryTravelProgressTracker
{
    private readonly EntryTravelProgressPolicy policy;
    private EntryTravelPlan? plan;
    private Coordinate2D? lastProgressPosition;
    private DateTimeOffset? lastProgressAt;
    private int activeStepIndex;

    public EntryTravelProgressTracker(EntryTravelProgressPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        this.policy = policy;
    }

    public EntryTravelStatus Status { get; private set; } = EntryTravelStatus.Idle;

    public bool TryStart(
        EntryTravelPlan candidate,
        EntryTravelStartAuthorization authorization,
        DateTimeOffset now,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(authorization);
        candidate.Validate();
        authorization.Validate();

        if (now == default || authorization.ConfirmedAt > now || now - authorization.ConfirmedAt > policy.AuthorizationLifetime)
        {
            reason = "明示開始の確認が期限切れか、時刻が不正です。";
            Status = new(EntryTravelRunState.Blocked, 0, candidate.Steps.Length, null, reason);
            return false;
        }

        if (!candidate.IsExecutable)
        {
            reason = candidate.StatusReason;
            Status = new(EntryTravelRunState.Blocked, 0, candidate.Steps.Length, null, reason);
            return false;
        }

        plan = candidate;
        activeStepIndex = 0;
        lastProgressAt = now;
        lastProgressPosition = null;
        reason = "ユーザーの明示操作により入口移動を開始しました。";
        Status = new(EntryTravelRunState.Running, 0, candidate.Steps.Length, now, reason);
        return true;
    }

    public EntryTravelStatus Observe(EntryTravelProgressObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Status.State != EntryTravelRunState.Running || plan is null)
        {
            return Status;
        }

        if (observation.ObservedAt == default || observation.ObservedAt < lastProgressAt)
        {
            return Stop(EntryTravelRunState.Faulted, "移動観測の時刻が不正または逆行しました。");
        }

        if (!string.IsNullOrWhiteSpace(observation.FailureReason))
        {
            return Stop(EntryTravelRunState.Blocked, observation.FailureReason.Trim());
        }

        if (observation.ActiveStepIndex != activeStepIndex
            || observation.ActiveStepIndex < 0
            || observation.ActiveStepIndex >= plan.Steps.Length)
        {
            return Stop(EntryTravelRunState.Faulted, "移動手順の進行状態が一致しません。");
        }

        if (observation.Position is not { } position)
        {
            return Stop(EntryTravelRunState.Blocked, "現在位置を取得できないため停止しました。");
        }

        if (!position.IsFinite)
        {
            return Stop(EntryTravelRunState.Faulted, "現在位置に有限でない値が含まれるため停止しました。");
        }

        if (observation.DestinationReached)
        {
            activeStepIndex = plan.Steps.Length;
            return Stop(EntryTravelRunState.Arrived, "オルト・エウレカ入口への到着を確認しました。");
        }

        var progressed = lastProgressPosition is null
            || Distance(lastProgressPosition.Value, position) >= policy.MinimumProgressDistance;
        if (progressed || observation.StepCompleted)
        {
            lastProgressAt = observation.ObservedAt;
            lastProgressPosition = position;
        }

        if (observation.StepCompleted)
        {
            activeStepIndex++;
            if (activeStepIndex >= plan.Steps.Length)
            {
                return Stop(EntryTravelRunState.Arrived, "すべての移動手順の完了を確認しました。");
            }
        }
        else if (observation.ObservedAt - lastProgressAt >= policy.StallTimeout)
        {
            return Stop(EntryTravelRunState.Blocked, "位置の進展がない状態が上限時間を超えたため停止しました。");
        }

        Status = new(
            EntryTravelRunState.Running,
            activeStepIndex,
            plan.Steps.Length,
            lastProgressAt,
            "入口への移動状況を監視しています。");
        return Status;
    }

    public EntryTravelStatus EmergencyStop(string reason)
    {
        var detail = string.IsNullOrWhiteSpace(reason) ? "ユーザーが緊急停止しました。" : reason.Trim();
        return Stop(EntryTravelRunState.Stopped, detail);
    }

    private EntryTravelStatus Stop(EntryTravelRunState state, string reason)
    {
        Status = new(state, activeStepIndex, plan?.Steps.Length ?? 0, lastProgressAt, reason);
        plan = null;
        lastProgressPosition = null;
        return Status;
    }

    private static double Distance(Coordinate2D first, Coordinate2D second)
    {
        return Coordinate2D.Distance(first, second);
    }
}
