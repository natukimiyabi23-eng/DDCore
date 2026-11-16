using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using ActionRow = Lumina.Excel.Sheets.Action;
using StatusRow = Lumina.Excel.Sheets.Status;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudCombatNameResolver(IDataManager dataManager)
{
    private readonly Dictionary<uint, string?> actionNames = [];
    private readonly Dictionary<uint, string?> statusNames = [];
    private ExcelSheet<ActionRow>? actions;
    private ExcelSheet<StatusRow>? statuses;
    private bool actionSheetUnavailable;
    private bool statusSheetUnavailable;

    public string FormatAction(uint actionId) => Format(actionId, ResolveAction(actionId));

    public string FormatStatus(uint statusId) => Format(statusId, ResolveStatus(statusId));

    private string? ResolveAction(uint actionId) => Resolve(
        actionId,
        actionNames,
        ref actions,
        ref actionSheetUnavailable,
        static row => row.Name.ExtractText(),
        static (manager, language) => manager.GetExcelSheet<ActionRow>(language));

    private string? ResolveStatus(uint statusId) => Resolve(
        statusId,
        statusNames,
        ref statuses,
        ref statusSheetUnavailable,
        static row => row.Name.ExtractText(),
        static (manager, language) => manager.GetExcelSheet<StatusRow>(language));

    private string? Resolve<TRow>(
        uint rowId,
        Dictionary<uint, string?> cache,
        ref ExcelSheet<TRow>? sheet,
        ref bool sheetUnavailable,
        Func<TRow, string> getName,
        Func<IDataManager, ClientLanguage?, ExcelSheet<TRow>> getSheet)
        where TRow : struct, IExcelRow<TRow>
    {
        if (cache.TryGetValue(rowId, out var cached))
        {
            return cached;
        }

        string? name = null;
        try
        {
            if (!sheetUnavailable)
            {
                sheet ??= getSheet(dataManager, ClientLanguage.Japanese);
                var row = sheet.GetRowOrDefault(rowId);
                if (row is { } value)
                {
                    var extracted = getName(value).Trim();
                    name = string.IsNullOrWhiteSpace(extracted) ? null : extracted;
                }
            }
        }
        catch (Exception)
        {
            sheetUnavailable = true;
        }

        cache[rowId] = name;
        return name;
    }

    private static string Format(uint id, string? name) => string.IsNullOrWhiteSpace(name)
        ? id.ToString()
        : $"{id} {name}";
}
