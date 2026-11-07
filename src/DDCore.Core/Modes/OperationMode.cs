namespace DDCore.Core.Modes;

public enum OperationMode
{
    Observer = 0,
    Advisor = 1,
    Simulation = 2,
    Assisted = 3,
    Autonomous = 4,
}

public static class OperationModeExtensions
{
    public static bool RequiresExecution(this OperationMode mode) =>
        mode is OperationMode.Assisted or OperationMode.Autonomous;

    public static bool IsDefined(this OperationMode mode) =>
        Enum.IsDefined(mode);

    public static string JapaneseName(this OperationMode mode) => mode switch
    {
        OperationMode.Observer => "観測",
        OperationMode.Advisor => "助言",
        OperationMode.Simulation => "シミュレーション",
        OperationMode.Assisted => "補助操作",
        OperationMode.Autonomous => "自律操作",
        _ => "不明",
    };
}
