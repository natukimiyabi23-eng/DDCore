using DDCore.Core.Modes;

namespace DDCore.Core.Safety;

public sealed record ModeTransitionResult(
    bool Accepted,
    OperationMode PreviousMode,
    OperationMode CurrentMode,
    string Reason);

public interface ISafetyController
{
    OperationMode CurrentMode { get; }
    bool EmergencyStopLatched { get; }
    bool ExecutionAvailable { get; }
    bool SessionArmed { get; }

    ModeTransitionResult RequestMode(OperationMode requestedMode);
    bool TryArmSession(out string reason);
    void EmergencyStop(string reason);
    bool TryResetEmergencyStop(out string reason);
    void RestoreEmergencyStop(bool latched, string? reason = null);
}

public sealed class SafetyController(bool executionAvailable) : ISafetyController
{
    public OperationMode CurrentMode { get; private set; } = OperationMode.Observer;
    public bool EmergencyStopLatched { get; private set; }
    public bool ExecutionAvailable { get; } = executionAvailable;
    public bool SessionArmed { get; private set; }
    public string LastStopReason { get; private set; } = string.Empty;

    public ModeTransitionResult RequestMode(OperationMode requestedMode)
    {
        var previous = CurrentMode;

        if (!requestedMode.IsDefined())
        {
            return Reject(previous, "未定義のモードです。");
        }

        if (requestedMode == OperationMode.Observer)
        {
            CurrentMode = OperationMode.Observer;
            SessionArmed = false;
            return Accept(previous, "観測モードへ移行しました。");
        }

        if (EmergencyStopLatched)
        {
            return Reject(previous, "緊急停止が有効なため、観測以外のモードへ移行できません。");
        }

        if (requestedMode.RequiresExecution())
        {
            if (!ExecutionAvailable)
            {
                return Reject(previous, "Phase 2には実行機能がないため、このモードは利用できません。");
            }

            if (!SessionArmed)
            {
                return Reject(previous, "このセッションでは実行機能が承認されていません。");
            }
        }

        CurrentMode = requestedMode;
        return Accept(previous, $"{requestedMode.JapaneseName()}モードへ移行しました。");
    }

    public bool TryArmSession(out string reason)
    {
        if (EmergencyStopLatched)
        {
            reason = "緊急停止が有効です。";
            return false;
        }

        if (!ExecutionAvailable)
        {
            reason = "実行機能はこのビルドに含まれていません。";
            return false;
        }

        SessionArmed = true;
        reason = "このセッションの実行機能を承認しました。";
        return true;
    }

    public void EmergencyStop(string reason)
    {
        CurrentMode = OperationMode.Observer;
        SessionArmed = false;
        EmergencyStopLatched = true;
        LastStopReason = string.IsNullOrWhiteSpace(reason) ? "理由は記録されていません。" : reason.Trim();
    }

    public bool TryResetEmergencyStop(out string reason)
    {
        if (CurrentMode != OperationMode.Observer)
        {
            reason = "緊急停止の解除は観測モードでのみ行えます。";
            return false;
        }

        EmergencyStopLatched = false;
        SessionArmed = false;
        LastStopReason = string.Empty;
        reason = "緊急停止を解除しました。実行機能は承認されていません。";
        return true;
    }

    public void RestoreEmergencyStop(bool latched, string? reason = null)
    {
        CurrentMode = OperationMode.Observer;
        SessionArmed = false;
        EmergencyStopLatched = latched;
        LastStopReason = latched ? reason ?? "前回の緊急停止を復元しました。" : string.Empty;
    }

    private ModeTransitionResult Accept(OperationMode previous, string reason) =>
        new(true, previous, CurrentMode, reason);

    private ModeTransitionResult Reject(OperationMode previous, string reason) =>
        new(false, previous, CurrentMode, reason);
}
