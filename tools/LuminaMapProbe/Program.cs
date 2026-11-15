using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LuminaMapProbe <Lumina.dll-directory> <sqpack-directory> [virtual-path]");
    return 2;
}

var luminaDirectory = Path.GetFullPath(args[0]);
var sqpackDirectory = Path.GetFullPath(args[1]);
var virtualPath = args.Length > 2 ? args[2] : null;
var luminaPath = Path.Combine(luminaDirectory, "Lumina.dll");
var excelPath = Path.Combine(luminaDirectory, "Lumina.Excel.dll");

if (!File.Exists(luminaPath) || !File.Exists(excelPath) || !Directory.Exists(sqpackDirectory))
{
    Console.Error.WriteLine("Lumina.dll, Lumina.Excel.dll, or SqPack directory was not found.");
    return 2;
}

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var candidate = Path.Combine(luminaDirectory, $"{name.Name}.dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};

var luminaAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(luminaPath);
AssemblyLoadContext.Default.LoadFromAssemblyPath(excelPath);
var version = luminaAssembly.GetName().Version?.ToString();
var gameDataType = luminaAssembly.GetType("Lumina.GameData", throwOnError: true)!;
var optionsType = luminaAssembly.GetType("Lumina.LuminaOptions", throwOnError: true)!;
var options = Activator.CreateInstance(optionsType)!;
var gameData = Activator.CreateInstance(gameDataType, sqpackDirectory, options)!;
var fileExists = gameDataType.GetMethod("FileExists", [typeof(string)])!;
var getFile = gameDataType.GetMethods().Single(method => method.Name == "GetFile" && method.IsGenericMethod);
var lgbType = luminaAssembly.GetType("Lumina.Data.Files.LgbFile", throwOnError: true)!;
var getLgb = getFile.MakeGenericMethod(lgbType);

var territoryPath = ReadTerritoryBackground(gameData, gameDataType);
var derivedPath = $"bg/{territoryPath[..territoryPath.LastIndexOf('/')]}/planmap.lgb";
virtualPath ??= derivedPath;

var present = (bool)fileExists.Invoke(gameData, [virtualPath])!;
if (!present)
{
    Console.WriteLine(JsonSerializer.Serialize(new { luminaAssemblyVersion = version, territoryId = 1099, territoryBackground = territoryPath, derivedCandidate = derivedPath, requestedVirtualPath = virtualPath, exists = false }));
    return 1;
}

var file = getLgb.Invoke(gameData, [virtualPath]);
if (file is null)
{
    Console.Error.WriteLine("Lumina FileExists returned true but GetFile<LgbFile> returned null.");
    return 1;
}

var data = (byte[])GetMember(file, "Data")!;
var layers = (IEnumerable)GetMember(file, "Layers")!;
var summaries = new List<object>();
var roomCandidates = new List<object>();
var countsByAssetType = new SortedDictionary<string, int>(StringComparer.Ordinal);
var eventRanges = 0;
var usableTransforms = 0;

foreach (var layer in layers)
{
    var layerName = (string)GetMember(layer!, "Name")!;
    var layerId = Convert.ToUInt32(GetMember(layer!, "LayerId"));
    var layerObjectCount = 0;
    var instances = (IEnumerable)GetMember(layer!, "InstanceObjects")!;

    foreach (var instance in instances)
    {
        layerObjectCount++;
        var assetType = GetMember(instance!, "AssetType")?.ToString() ?? "Unknown";
        countsByAssetType[assetType] = countsByAssetType.GetValueOrDefault(assetType) + 1;
        if (assetType != "EventRange")
            continue;

        eventRanges++;
        if (layerName is not ("LVD_Room_A" or "LVD_Room_B"))
            continue;

        var payload = GetMember(instance!, "Object")!;
        var parentData = GetMember(payload, "ParentData")!;
        var transform = GetMember(instance!, "Transform")!;
        var position = ReadVector(GetMember(transform, "Translation"));
        var scale = ReadVector(GetMember(transform, "Scale"));
        var rotation = ReadVector(GetMember(transform, "Rotation"));
        var finite = position.Concat(scale).Concat(rotation).All(float.IsFinite);
        if (finite)
            usableTransforms++;

        roomCandidates.Add(new
        {
            layer = layerName,
            priority = Convert.ToInt32(GetMember(parentData, "Priority")),
            shape = GetMember(parentData, "TriggerBoxShape")?.ToString(),
            position,
            scale,
            rotation,
            boundsUsingBossModConvention = new
            {
                minX = position[0] - MathF.Abs(scale[0]),
                maxX = position[0] + MathF.Abs(scale[0]),
                minZ = position[2] - MathF.Abs(scale[2]),
                maxZ = position[2] + MathF.Abs(scale[2]),
            },
            finite,
        });
    }

    summaries.Add(new { name = layerName, id = layerId, objectCount = layerObjectCount });
}

var canonical = JsonSerializer.Serialize(new { summaries, countsByAssetType, roomCandidates });
var canonicalHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
var digest = Convert.ToHexString(SHA256.HashData(data));
var result = new
{
    luminaAssemblyVersion = version,
    territoryId = 1099,
    territoryBackground = territoryPath,
    derivedCandidate = derivedPath,
    requestedVirtualPath = virtualPath,
    exists = present,
    assetByteCount = data.Length,
    assetSha256 = digest,
    layerCount = summaries.Count,
    layers = summaries,
    countsByAssetType,
    roomCandidateCount = roomCandidates.Count,
    usableTransformCount = usableTransforms,
    nonFiniteTransformCount = roomCandidates.Count - usableTransforms,
    canonicalSummarySha256 = canonicalHash,
    roomCandidates,
};

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return usableTransforms == roomCandidates.Count ? 0 : 1;

static object? GetMember(object? instance, string name)
{
    if (instance is null)
        return null;

    var type = instance.GetType();
    return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance)
        ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);
}

static float[] ReadVector(object? value) =>
[
    Convert.ToSingle(GetMember(value, "X")),
    Convert.ToSingle(GetMember(value, "Y")),
    Convert.ToSingle(GetMember(value, "Z")),
];

static string ReadTerritoryBackground(object gameData, Type gameDataType)
{
    var excelAssembly = AssemblyLoadContext.Default.Assemblies.Single(assembly => assembly.GetName().Name == "Lumina.Excel");
    var territoryType = excelAssembly.GetType("Lumina.Excel.Sheets.TerritoryType", throwOnError: true)!;
    var getSheet = gameDataType.GetMethods().Single(method => method.Name == "GetExcelSheet" && method.IsGenericMethod)
        .MakeGenericMethod(territoryType);
    var sheet = (IEnumerable)getSheet.Invoke(gameData, [null, null])!;
    foreach (var row in sheet)
    {
        if (Convert.ToUInt32(GetMember(row, "RowId")) == 1099)
            return GetMember(row, "Bg")!.ToString()!;
    }

    throw new InvalidDataException("TerritoryType row 1099 was not found in the installed game data.");
}
