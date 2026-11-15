using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LuminaMapProbe <Lumina.dll-directory> <sqpack-directory> [virtual-path|--public-api-surface|--deep-dungeon-grids|--replay-room-candidates <export-directory>]");
    return 2;
}

var luminaDirectory = Path.GetFullPath(args[0]);
var sqpackDirectory = Path.GetFullPath(args[1]);
var apiSurfaceOnly = args.Length > 2 && args[2] == "--public-api-surface";
var deepDungeonGridsOnly = args.Length > 2 && args[2] == "--deep-dungeon-grids";
var replayRoomCandidates = args.Length > 2 && args[2] == "--replay-room-candidates";
var virtualPath = args.Length > 2 && !apiSurfaceOnly && !deepDungeonGridsOnly && !replayRoomCandidates ? args[2] : null;
if (replayRoomCandidates && args.Length < 4)
{
    Console.Error.WriteLine("--replay-room-candidates requires an export-directory argument.");
    return 2;
}
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
var excelAssembly = AssemblyLoadContext.Default.Assemblies.Single(assembly => assembly.GetName().Name == "Lumina.Excel");
var excelModule = GetMember(gameData, "Excel");
if (deepDungeonGridsOnly)
{
    var gridReport = ReadEurekaOrthosGrids(gameData, gameDataType, excelAssembly);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        dungeonRowId = 3,
        floorSetCount = gridReport.FloorSetCount,
        floorSetCoverageComplete = gridReport.FloorSetCoverageComplete,
        invalidGridCount = gridReport.InvalidGridCount,
        priorityIndexMismatchCount = gridReport.PriorityIndexMismatchCount,
        planmapVirtualPath = gridReport.PlanmapVirtualPath,
        planmapExists = gridReport.PlanmapExists,
        planmapPriorityIndices = gridReport.PlanmapPriorityIndices,
        summarySha256 = gridReport.SummarySha256,
        floorSets = gridReport.FloorSets,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return gridReport.FloorSetCoverageComplete && gridReport.InvalidGridCount == 0 && gridReport.PlanmapExists && gridReport.PriorityIndexMismatchCount == 0 ? 0 : 1;
}

if (apiSurfaceOnly)
{
    var gameDataPropertyTypes = gameDataType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Select(static property => property.PropertyType)
        .SelectMany(static type => type.IsGenericType ? type.GetGenericArguments().Append(type) : [type])
        .ToArray();
    var repositoryType = luminaAssembly.GetType("Lumina.Data.Repository");
    var repositoryPropertyTypes = repositoryType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Select(static property => property.PropertyType)
        .SelectMany(static type => type.IsGenericType ? type.GetGenericArguments().Append(type) : [type])
        .ToArray() ?? [];
    var relevantTypes = luminaAssembly.GetExportedTypes()
        .Where(static type => type.Name.Contains("File", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("SqPack", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Index", StringComparison.OrdinalIgnoreCase))
        .Concat(gameDataPropertyTypes)
        .Concat(repositoryPropertyTypes)
        .Append(luminaAssembly.GetType("Lumina.Data.Category", throwOnError: true)!)
        .Append(gameDataType)
        .Distinct()
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .Select(static type => new
        {
            type = type.FullName,
            methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.ToString())
                .Order(StringComparer.Ordinal)
                .ToArray(),
            properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static property => $"{property.PropertyType.FullName} {property.Name}")
                .Order(StringComparer.Ordinal)
                .ToArray(),
        })
        .ToArray();
    var repositoryCatalog = EnumeratePairs(GetPublicProperty(gameData, "Repositories")!)
        .Select(entry => new
        {
            name = entry.Key?.ToString(),
            expansion = GetPublicProperty(entry.Value, "ExpansionId"),
            categories = EnumeratePairs(GetPublicProperty(entry.Value, "Categories")!)
                .SelectMany(categoryEntry => ((IEnumerable)categoryEntry.Value!).Cast<object>())
                .Select(category => new
                {
                    categoryId = GetPublicProperty(category, "CategoryId"),
                    expansion = GetPublicProperty(category, "Expansion"),
                    chunk = GetPublicProperty(category, "Chunk"),
                    indexHashCount = GetDictionaryCount(GetPublicProperty(category, "IndexHashTableEntries")),
                    index2HashCount = GetDictionaryCount(GetPublicProperty(category, "Index2HashTableEntries")),
                })
                .OrderBy(static category => category.expansion)
                .ThenBy(static category => category.categoryId)
                .ThenBy(static category => category.chunk)
                .ToArray(),
        })
        .OrderBy(static repository => repository.name, StringComparer.Ordinal)
        .ToArray();

    Console.WriteLine(JsonSerializer.Serialize(new { luminaAssemblyVersion = version, publicApi = relevantTypes, repositoryCatalog }));
    return 0;
}

var excelApiMethods = excelModule?.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
    .Where(static method => method.Name.Contains("Sheet", StringComparison.OrdinalIgnoreCase))
    .Select(static method => method.ToString())
    .Order(StringComparer.Ordinal)
    .ToArray() ?? [];
var collisionExcelTypes = excelAssembly.GetTypes()
    .Where(static type => type.Namespace == "Lumina.Excel.Sheets" && type.Name.Contains("Collision", StringComparison.OrdinalIgnoreCase))
    .Select(static type => new
    {
        fullName = type.FullName,
        baseType = type.BaseType?.FullName,
        properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static property => new { property.Name, property.PropertyType.FullName })
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray(),
        fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static field => new { field.Name, field.FieldType.FullName })
            .OrderBy(static field => field.Name, StringComparer.Ordinal)
            .ToArray(),
    })
    .OrderBy(static type => type.fullName, StringComparer.Ordinal)
    .ToArray();
var collisionIdPalletRow9 = ReadCollisionIdPalletRow9(excelModule, excelAssembly);
var fileExists = gameDataType.GetMethod("FileExists", [typeof(string)])!;
var getFile = gameDataType.GetMethods().Single(method => method.Name == "GetFile" && method.IsGenericMethod);
var lgbType = luminaAssembly.GetType("Lumina.Data.Files.LgbFile", throwOnError: true)!;
var getLgb = getFile.MakeGenericMethod(lgbType);
var sgbType = luminaAssembly.GetType("Lumina.Data.Files.SgbFile", throwOnError: false);
var getSgb = sgbType is null ? null : getFile.MakeGenericMethod(sgbType);

var territory = ReadTerritory(gameData, gameDataType);
var territoryPath = territory.Background;
var derivedPath = $"bg/{territoryPath[..territoryPath.LastIndexOf('/')]}/planmap.lgb";
virtualPath ??= derivedPath;

var present = (bool)fileExists.Invoke(gameData, [virtualPath])!;
if (!present)
{
    Console.WriteLine(JsonSerializer.Serialize(new { luminaAssemblyVersion = version, territoryId = 1099, territoryBackground = territoryPath, territoryMapRowId = territory.MapRowId, derivedCandidate = derivedPath, requestedVirtualPath = virtualPath, exists = false }));
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
var sharedGroupSummaries = new List<object>();
var referencedSgbSummaries = new List<object>();
var sharedGroupAssetPaths = new SortedSet<string>(StringComparer.Ordinal);
var countsByAssetType = new SortedDictionary<string, int>(StringComparer.Ordinal);
var objectTypeSummaries = new SortedDictionary<string, object>(StringComparer.Ordinal);
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
        if (!objectTypeSummaries.ContainsKey(assetType))
        {
            var payloadSummary = GetMember(instance, "Object");
            objectTypeSummaries[assetType] = new
            {
                layer = layerName,
                wrapperType = instance!.GetType().FullName,
                payloadType = payloadSummary?.GetType().FullName,
                payloadMembers = GetMemberNames(payloadSummary),
            };
        }

        if (assetType == "SharedGroup")
        {
            var sharedGroupPayload = GetMember(instance, "Object");
            var sharedGroupTransform = GetMember(instance, "Transform");
            var sharedGroupAssetPath = GetMember(sharedGroupPayload, "AssetPath")?.ToString();
            if (!string.IsNullOrWhiteSpace(sharedGroupAssetPath))
                sharedGroupAssetPaths.Add(sharedGroupAssetPath);

            sharedGroupSummaries.Add(new
            {
                layer = layerName,
                assetPath = sharedGroupAssetPath,
                translation = ReadVector(GetMember(sharedGroupTransform, "Translation")),
                scale = ReadVector(GetMember(sharedGroupTransform, "Scale")),
                rotation = ReadVector(GetMember(sharedGroupTransform, "Rotation")),
                initialDoorState = GetMember(sharedGroupPayload, "InitialDoorState")?.ToString(),
                notCreateNavimeshDoor = GetMember(sharedGroupPayload, "NotCreateNavimeshDoor"),
                collisionControllableWithoutEventObject = GetMember(sharedGroupPayload, "IsCollisionControllableWithoutEObj"),
            });
        }

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

foreach (var assetPath in sharedGroupAssetPaths)
{
    var exists = (bool)fileExists.Invoke(gameData, [assetPath])!;
    if (!exists || getSgb is null)
    {
        referencedSgbSummaries.Add(new { assetPath, exists, sgbTypeAvailable = sgbType is not null });
        continue;
    }

    var referencedFile = getSgb.Invoke(gameData, [assetPath]);
    if (referencedFile is null)
    {
        referencedSgbSummaries.Add(new { assetPath, exists, sgbTypeAvailable = true, parsed = false });
        continue;
    }

    var referencedBytes = GetMember(referencedFile, "Data") as byte[];
    var layerGroupsValue = GetMember(referencedFile, "LayerGroups");
    if (layerGroupsValue is not IEnumerable layerGroups)
    {
        referencedSgbSummaries.Add(new
        {
            assetPath,
            exists,
            sgbTypeAvailable = true,
            parsed = false,
            rootType = referencedFile.GetType().FullName,
            rootMembers = GetMemberNames(referencedFile),
            byteCount = referencedBytes?.Length,
        });
        continue;
    }

    var groupSummaries = new List<object>();
    foreach (var group in layerGroups)
    {
        var nestedLayers = GetMember(group, "Layers") as IEnumerable;
        var layerSummaries = new List<object>();
        if (nestedLayers is not null)
        {
            foreach (var layer in nestedLayers)
            {
                var objects = GetMember(layer, "InstanceObjects") as IEnumerable;
                var typeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var payloadTypes = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
                var collisionBoxes = new List<object>();
                if (objects is not null)
                {
                    foreach (var item in objects)
                    {
                        var type = GetMember(item, "AssetType")?.ToString() ?? "Unknown";
                        typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;
                        var payload = GetMember(item, "Object");
                        var payloadType = payload?.GetType().FullName ?? "Unknown";
                        if (!payloadTypes.TryGetValue(type, out var names))
                        {
                            names = new SortedSet<string>(StringComparer.Ordinal);
                            payloadTypes.Add(type, names);
                        }

                        names.Add(payloadType);
                        if (type == "CollisionBox")
                        {
                            var transform = GetMember(item, "Transform");
                            var parentData = GetMember(payload, "ParentData");
                            var collisionAssetPath = GetMember(payload, "CollisionAssetPath");
                            collisionBoxes.Add(new
                            {
                                payloadType,
                                collisionAssetPath = collisionAssetPath?.ToString(),
                                collisionAssetPathType = collisionAssetPath?.GetType().FullName,
                                attribute = GetMember(payload, "Attribute")?.ToString(),
                                attributeMask = GetMember(payload, "AttributeMask")?.ToString(),
                                pushPlayerOut = GetMember(payload, "PushPlayerOut"),
                                enabled = GetMember(parentData, "Enabled"),
                                priority = GetMember(parentData, "Priority"),
                                shape = GetMember(parentData, "TriggerBoxShape")?.ToString(),
                                translation = ReadVector(GetMember(transform, "Translation")),
                                scale = ReadVector(GetMember(transform, "Scale")),
                                rotation = ReadVector(GetMember(transform, "Rotation")),
                            });
                        }
                    }
                }

                layerSummaries.Add(new
                {
                    name = GetMember(layer, "Name")?.ToString() ?? "Unknown",
                    layerType = layer?.GetType().FullName,
                    objectCount = typeCounts.Values.Sum(),
                    typeCounts,
                    payloadTypes,
                    collisionBoxes,
                });
            }
        }

        groupSummaries.Add(new
        {
            groupType = group?.GetType().FullName,
            groupMembers = GetMemberNames(group),
            layerCount = layerSummaries.Count,
            layers = layerSummaries,
        });
    }

    referencedSgbSummaries.Add(new
    {
        assetPath,
        exists,
        sgbTypeAvailable = true,
        parsed = true,
        exposedStructureOnly = true,
        byteCount = referencedBytes?.Length,
        dataSha256 = referencedBytes is null ? null : Convert.ToHexString(SHA256.HashData(referencedBytes)),
        layerGroupCount = groupSummaries.Count,
        layerGroups = groupSummaries,
    });
}

if (replayRoomCandidates)
{
    var gridReport = ReadEurekaOrthosGrids(gameData, gameDataType, excelAssembly);
    (object Report, int SelectedReplayCount) replayReport;
    try
    {
        replayReport = ReadReplayRoomCandidateCollection(args[3], roomCandidates, gridReport.FloorSets);
    }
    catch (DirectoryNotFoundException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
    Console.WriteLine(JsonSerializer.Serialize(replayReport.Report, new JsonSerializerOptions { WriteIndented = true }));
    return replayReport.SelectedReplayCount > 0 ? 0 : 1;
}

var canonical = JsonSerializer.Serialize(new { summaries, countsByAssetType, objectTypeSummaries, roomCandidates, sharedGroupSummaries, referencedSgbSummaries });
var canonicalHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
var digest = Convert.ToHexString(SHA256.HashData(data));
var result = new
{
    luminaAssemblyVersion = version,
    territoryId = 1099,
    territoryBackground = territoryPath,
    territoryMapRowId = territory.MapRowId,
    derivedCandidate = derivedPath,
    requestedVirtualPath = virtualPath,
    exists = present,
    assetByteCount = data.Length,
    assetSha256 = digest,
    layerCount = summaries.Count,
    layers = summaries,
    countsByAssetType,
    excelApiMethods,
    collisionExcelTypes,
    collisionIdPalletRow9,
    objectTypeSummaries,
    sharedGroupCount = sharedGroupSummaries.Count,
    sharedGroupSummaries,
    referencedSgbSummaries,
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

static object? GetPublicProperty(object? instance, string name) => instance?.GetType()
    .GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)?.GetValue(instance);

static IEnumerable<(object? Key, object? Value)> EnumeratePairs(object dictionary) => ((IEnumerable)dictionary)
    .Cast<object>()
    .Select(static pair => (
        Key: pair.GetType().GetProperty("Key", BindingFlags.Public | BindingFlags.Instance)?.GetValue(pair),
        Value: pair.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(pair)));

static int? GetDictionaryCount(object? value) => value is IDictionary dictionary ? dictionary.Count : null;

static string[] GetMemberNames(object? instance)
{
    if (instance is null)
        return [];

    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    return instance.GetType()
        .GetMembers(flags)
        .Where(static member => member.MemberType is MemberTypes.Field or MemberTypes.Property)
        .Select(static member => member.Name)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}

static object ReadCollisionIdPalletRow9(object? excelModule, Assembly excelAssembly)
{
    var rowType = excelAssembly.GetType("Lumina.Excel.Sheets.CollisionIdPallet", throwOnError: false);
    if (excelModule is null || rowType is null)
        return new { supported = false, reason = "sheet type unavailable" };

    try
    {
        var getSheet = excelModule.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == "GetSheet" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2);
        var sheet = getSheet.MakeGenericMethod(rowType).Invoke(excelModule, [null, "CollisionIdPallet"]);
        if (sheet is null)
            return new { supported = false, reason = "sheet unavailable" };

        var getRow = sheet.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method => method.Name == "GetRow" && method.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(uint));
        if (getRow is null)
            return new { supported = false, reason = "row reader unavailable" };

        var row = getRow.Invoke(sheet, [9u]);
        if (row is null)
            return new { supported = true, rowId = 9, rowFound = false };

        var values = rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => property.GetMethod is not null)
            .ToDictionary(property => property.Name, property => property.GetValue(row)?.ToString(), StringComparer.Ordinal);
        return new { supported = true, rowId = 9, rowFound = true, values };
    }
    catch (Exception exception)
    {
        return new { supported = false, reason = exception.GetType().Name };
    }
}

static float[] ReadVector(object? value) =>
[
    Convert.ToSingle(GetMember(value, "X")),
    Convert.ToSingle(GetMember(value, "Y")),
    Convert.ToSingle(GetMember(value, "Z")),
];

static (string Background, uint MapRowId) ReadTerritory(object gameData, Type gameDataType)
{
    var excelAssembly = AssemblyLoadContext.Default.Assemblies.Single(assembly => assembly.GetName().Name == "Lumina.Excel");
    var territoryType = excelAssembly.GetType("Lumina.Excel.Sheets.TerritoryType", throwOnError: true)!;
    var getSheet = gameDataType.GetMethods().Single(method => method.Name == "GetExcelSheet" && method.IsGenericMethod)
        .MakeGenericMethod(territoryType);
    var sheet = (IEnumerable)getSheet.Invoke(gameData, [null, null])!;
    foreach (var row in sheet)
    {
        if (Convert.ToUInt32(GetMember(row, "RowId")) == 1099)
        {
            var map = GetMember(row, "Map");
            return (GetMember(row, "Bg")!.ToString()!, Convert.ToUInt32(GetMember(map, "RowId")));
        }
    }

    throw new InvalidDataException("TerritoryType row 1099 was not found in the installed game data.");
}

static (int FloorSetCount, bool FloorSetCoverageComplete, int InvalidGridCount, int PriorityIndexMismatchCount, string PlanmapVirtualPath, bool PlanmapExists, object PlanmapPriorityIndices, string SummarySha256, object[] FloorSets) ReadEurekaOrthosGrids(object gameData, Type gameDataType, Assembly excelAssembly)
{
    var layerType = excelAssembly.GetType("Lumina.Excel.Sheets.DeepDungeonLayer", throwOnError: true)!;
    var getSheet = gameDataType.GetMethods().Single(method => method.Name == "GetExcelSheet" && method.IsGenericMethod)
        .MakeGenericMethod(layerType);
    var sheet = (IEnumerable)getSheet.Invoke(gameData, [null, null])!;
    var floorSets = new List<object>();
    var invalidGridCount = 0;
    var priorityIndexMismatchCount = 0;
    var territory = ReadTerritory(gameData, gameDataType);
    var planmapPath = $"bg/{territory.Background[..territory.Background.LastIndexOf('/')]}/planmap.lgb";
    var fileExists = gameDataType.GetMethod("FileExists", [typeof(string)])!;
    var planmapExists = (bool)fileExists.Invoke(gameData, [planmapPath])!;
    var planmapIndices = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

    if (planmapExists)
    {
        var lgbType = gameDataType.Assembly.GetType("Lumina.Data.Files.LgbFile", throwOnError: true)!;
        var getLgb = gameDataType.GetMethods().Single(method => method.Name == "GetFile" && method.IsGenericMethod)
            .MakeGenericMethod(lgbType);
        var lgb = getLgb.Invoke(gameData, [planmapPath])!;
        var layers = (IEnumerable)GetMember(lgb, "Layers")!;
        foreach (var layer in layers)
        {
            var layerName = GetMember(layer, "Name")?.ToString();
            if (layerName is not ("LVD_Room_A" or "LVD_Room_B"))
                continue;

            var indices = new HashSet<int>();
            foreach (var instance in (IEnumerable)GetMember(layer, "InstanceObjects")!)
            {
                if (GetMember(instance, "AssetType")?.ToString() != "EventRange")
                    continue;
                var payload = GetMember(instance, "Object");
                var parentData = GetMember(payload, "ParentData");
                var priority = Convert.ToInt32(GetMember(parentData, "Priority"));
                if (priority is > 100 and < 126)
                    indices.Add(priority - 100);
            }

            planmapIndices[layerName[^1].ToString()] = indices;
        }
    }

    foreach (var layer in sheet)
    {
        var dungeon = GetMember(layer, "DeepDungeon");
        if (Convert.ToUInt32(GetMember(dungeon, "RowId")) != 3)
            continue;

        var variants = new List<object>();
        foreach (var variantName in new[] { "RoomA", "RoomB" })
        {
            var mapReference = GetMember(layer, variantName);
            var mapRowId = Convert.ToUInt32(GetMember(mapReference, "RowId"));
            var mapRows = (IEnumerable)GetMember(mapReference, "Value")!;
            var grid = new List<object>();
            var nonzeroRoomCells = 0;
            var adjacentOccupiedCellPairs = 0;
            var rows = new List<uint[]>();

            foreach (var mapRow in mapRows)
            {
                var roomReferences = (IEnumerable)GetMember(mapRow, "DeepDungeonRoom")!;
                var cellIds = roomReferences.Cast<object>()
                    .Select(reference => Convert.ToUInt32(GetMember(reference, "RowId")))
                    .ToArray();
                rows.Add(cellIds);
                nonzeroRoomCells += cellIds.Count(static rowId => rowId != 0);
                grid.Add(new { subrowId = Convert.ToUInt16(GetMember(mapRow, "SubrowId")), roomRowIds = cellIds });
            }

            var rectangular = rows.Count == 5 && rows.All(static row => row.Length == 5);
            if (!rectangular)
                invalidGridCount++;
            else
            {
                for (var row = 0; row < 5; row++)
                for (var column = 0; column < 5; column++)
                {
                    if (rows[row][column] == 0)
                        continue;
                    if (column < 4 && rows[row][column + 1] != 0)
                        adjacentOccupiedCellPairs++;
                    if (row < 4 && rows[row + 1][column] != 0)
                        adjacentOccupiedCellPairs++;
                }
            }

            var occupiedIndices = rows.SelectMany((row, rowIndex) => row
                .Select((roomRowId, columnIndex) => (roomRowId, index: rowIndex * 5 + columnIndex)))
                .Where(static cell => cell.roomRowId != 0)
                .Select(static cell => cell.index)
                .ToHashSet();
            var variantKey = variantName[^1].ToString();
            var priorityIndexMatch = rectangular
                && planmapIndices.TryGetValue(variantKey, out var layerIndices)
                && occupiedIndices.SetEquals(layerIndices);
            if (!priorityIndexMatch)
                priorityIndexMismatchCount++;

            variants.Add(new
            {
                variant = variantName,
                mapRowId,
                rowCount = rows.Count,
                columnCounts = rows.Select(static row => row.Length).ToArray(),
                rectangular5By5 = rectangular,
                nonzeroRoomCells,
                distinctNonzeroRoomRows = rows.SelectMany(static row => row).Where(static rowId => rowId != 0).Distinct().Count(),
                orthogonallyAdjacentOccupiedCellPairs = adjacentOccupiedCellPairs,
                roomGridIndices = occupiedIndices.Order().ToArray(),
                planmapPriorityIndicesMatchOccupiedCells = priorityIndexMatch,
                adjacencyMeaning = "spatial candidates only; not confirmed open connections",
                grid,
            });
        }

        floorSets.Add(new
        {
            layerRowId = Convert.ToUInt32(GetMember(layer, "RowId")),
            floorSet = Convert.ToByte(GetMember(layer, "FloorSet")),
            variants,
        });
    }

    var orderedFloorSets = floorSets.OrderBy(static floorSet => GetAnonymousProperty<byte>(floorSet, "floorSet")).ToArray();
    var observedFloorSets = orderedFloorSets.Select(static floorSet => GetAnonymousProperty<byte>(floorSet, "floorSet")).ToHashSet();
    var floorSetCoverageComplete = orderedFloorSets.Length == 10 && observedFloorSets.SetEquals(Enumerable.Range(0, 10).Select(static value => (byte)value));
    var canonical = JsonSerializer.Serialize(orderedFloorSets);
    var summaryHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    var planmapIndexSummary = planmapIndices.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(static pair => pair.Key, static pair => pair.Value.Order().ToArray(), StringComparer.Ordinal);
    return (orderedFloorSets.Length, floorSetCoverageComplete, invalidGridCount, priorityIndexMismatchCount, planmapPath, planmapExists, planmapIndexSummary, summaryHash, orderedFloorSets);
}

static T GetAnonymousProperty<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;

static (object Report, int SelectedReplayCount) ReadReplayRoomCandidateCollection(string exportDirectory, List<object> roomCandidates, object[] floorSets)
{
    if (!Directory.Exists(exportDirectory))
        throw new DirectoryNotFoundException("The replay export directory does not exist.");

    var firstFloorSet = floorSets.FirstOrDefault() ?? throw new InvalidDataException("No EO floor-set grid was available.");
    var variants = GetAnonymousProperty<IEnumerable>(firstFloorSet, "variants").Cast<object>().ToArray();
    var roomGridIndices = GetAnonymousProperty<int[]>(variants[0], "roomGridIndices").ToHashSet();
    var boxes = roomCandidates.Select(candidate =>
    {
        var bounds = GetAnonymousProperty<object>(candidate, "boundsUsingBossModConvention");
        return new CandidateRoomBox(
            GetAnonymousProperty<string>(candidate, "layer"),
            GetAnonymousProperty<int>(candidate, "priority") - 100,
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minX")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxX")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minZ")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxZ")));
    }).Where(static box => box.Index is >= 0 and < 25).ToArray();

    var validReplays = new List<(string Name, DateTimeOffset CreatedAt, DateTime LastWriteUtc, JsonDocument Document)>();
    var invalidSchemaFiveFiles = 0;
    var nonEurekaOrthosSchemaFiveFiles = 0;
    foreach (var path in Directory.EnumerateFiles(exportDirectory, "*.replay.json"))
    {
        try
        {
            var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 5)
            {
                document.Dispose();
                continue;
            }

            if (!root.TryGetProperty("snapshots", out var snapshots)
                || snapshots.ValueKind != JsonValueKind.Array
                || snapshots.GetArrayLength() == 0
                || !root.TryGetProperty("createdAtUtc", out var createdAtElement)
                || !DateTimeOffset.TryParse(createdAtElement.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var createdAt))
            {
                invalidSchemaFiveFiles++;
                document.Dispose();
                continue;
            }

            if (!TryGetEurekaOrthosBand(snapshots[0], out _))
            {
                nonEurekaOrthosSchemaFiveFiles++;
                document.Dispose();
                continue;
            }

            validReplays.Add((Path.GetFileName(path), createdAt, File.GetLastWriteTimeUtc(path), document));
        }
        catch (JsonException)
        {
            invalidSchemaFiveFiles++;
        }
        catch (IOException)
        {
            invalidSchemaFiveFiles++;
        }
        catch (InvalidOperationException)
        {
            invalidSchemaFiveFiles++;
        }
        catch (KeyNotFoundException)
        {
            invalidSchemaFiveFiles++;
        }
        catch (FormatException)
        {
            invalidSchemaFiveFiles++;
        }
    }

    var selected = validReplays.OrderByDescending(static replay => replay.CreatedAt)
        .ThenByDescending(static replay => replay.LastWriteUtc)
        .Take(6)
        .ToArray();
    var results = new List<object>();
    for (var ordinal = 0; ordinal < selected.Length; ordinal++)
    {
        var replay = selected[ordinal];
        try
        {
            results.Add(AnalyzeReplay(ordinal + 1, replay.CreatedAt, replay.LastWriteUtc, replay.Document.RootElement, boxes, roomGridIndices));
        }
        finally
        {
            replay.Document.Dispose();
        }
    }

    foreach (var replay in validReplays.Except(selected))
        replay.Document.Dispose();

    var totalSnapshots = results.Sum(static result => GetAnonymousProperty<int>(result, "snapshotCount"));
    var canonical = JsonSerializer.Serialize(results);
    var report = new
    {
        selectionRule = "six newest schema-5 exports whose first snapshot identifies Eureka Orthos and a floor band, ordered by internal createdAtUtc with filesystem modification time as tie-break",
        selectedReplayCount = selected.Length,
        invalidSchemaFiveFileCount = invalidSchemaFiveFiles,
        nonEurekaOrthosSchemaFiveFileCount = nonEurekaOrthosSchemaFiveFiles,
        totalSnapshots,
        replayResults = results,
        canonicalSummarySha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))),
    };
    return (report, selected.Length);
}

static object AnalyzeReplay(int recordingOrdinal, DateTimeOffset createdAt, DateTime lastWriteUtc, JsonElement root, CandidateRoomBox[] boxes, HashSet<int> roomGridIndices)
{
    var snapshots = root.GetProperty("snapshots");
    var snapshotCount = snapshots.GetArrayLength();
    var finitePoseCount = 0;
    var uniqueContainmentCount = 0;
    var multipleBoxCount = 0;
    var noCandidateCount = 0;
    var exactFloorKnownCount = 0;
    var minPlanarX = double.PositiveInfinity;
    var maxPlanarX = double.NegativeInfinity;
    var minPlanarZ = double.PositiveInfinity;
    var maxPlanarZ = double.NegativeInfinity;
    var bandCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var layerCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var indexCounts = new SortedDictionary<int, int>();
    var sequence = new List<CandidateCell?>(snapshotCount);

    foreach (var snapshot in snapshots.EnumerateArray())
    {
        if (!TryGetEurekaOrthosBand(snapshot, out var band))
        {
            sequence.Add(null);
            continue;
        }
        AddCount(bandCounts, band);

        var dungeon = snapshot.TryGetProperty("dungeon", out var dungeonElement) ? dungeonElement : default;
        if (dungeon.ValueKind == JsonValueKind.Object
            && dungeon.TryGetProperty("floor", out var floor)
            && floor.ValueKind == JsonValueKind.Number)
            exactFloorKnownCount++;

        if (!snapshot.TryGetProperty("playerPose", out var pose)
            || !pose.TryGetProperty("position", out var position)
            || position.ValueKind != JsonValueKind.Object
            || !position.TryGetProperty("isFinite", out var isFinite)
            || !isFinite.GetBoolean()
            || !position.TryGetProperty("x", out var xElement)
            || !position.TryGetProperty("y", out var zElement)
            || !xElement.TryGetDouble(out var x)
            || !zElement.TryGetDouble(out var z)
            || !double.IsFinite(x)
            || !double.IsFinite(z))
        {
            sequence.Add(null);
            continue;
        }

        finitePoseCount++;
        minPlanarX = Math.Min(minPlanarX, x);
        maxPlanarX = Math.Max(maxPlanarX, x);
        minPlanarZ = Math.Min(minPlanarZ, z);
        maxPlanarZ = Math.Max(maxPlanarZ, z);
        var hits = boxes.Where(box => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ).ToArray();
        if (hits.Length == 1)
        {
            var hit = hits[0];
            uniqueContainmentCount++;
            AddCount(layerCounts, hit.Layer);
            AddCount(indexCounts, hit.Index);
            sequence.Add(new CandidateCell(band, hit.Layer, hit.Index));
        }
        else if (hits.Length > 1)
        {
            multipleBoxCount++;
            sequence.Add(null);
        }
        else
        {
            noCandidateCount++;
            sequence.Add(null);
        }
    }

    var sameCellTransitionCount = 0;
    var orthogonalNeighborIndexTransitionCount = 0;
    var nonNeighborIndexTransitionCount = 0;
    var crossVariantTransitionCount = 0;
    var crossFloorBandTransitionCount = 0;
    var unclassifiedTransitionCount = 0;
    for (var index = 1; index < sequence.Count; index++)
    {
        var from = sequence[index - 1];
        var to = sequence[index];
        if (from is null || to is null)
        {
            unclassifiedTransitionCount++;
            continue;
        }

        var fromCell = from.Value;
        var toCell = to.Value;
        if (!string.Equals(fromCell.FloorBand, toCell.FloorBand, StringComparison.Ordinal))
        {
            crossFloorBandTransitionCount++;
            continue;
        }

        if (!string.Equals(fromCell.Layer, toCell.Layer, StringComparison.Ordinal))
        {
            crossVariantTransitionCount++;
            continue;
        }

        if (fromCell.Index == toCell.Index)
        {
            sameCellTransitionCount++;
            continue;
        }

        var rowDelta = Math.Abs(fromCell.Index / 5 - toCell.Index / 5);
        var columnDelta = Math.Abs(fromCell.Index % 5 - toCell.Index % 5);
        if (rowDelta + columnDelta == 1 && roomGridIndices.Contains(fromCell.Index) && roomGridIndices.Contains(toCell.Index))
            orthogonalNeighborIndexTransitionCount++;
        else
            nonNeighborIndexTransitionCount++;
    }

    return new
    {
        recordingOrdinal,
        createdAtUtc = createdAt,
        filesystemLastWriteTimeUtc = lastWriteUtc,
        schemaVersion = 5,
        producerVersion = root.TryGetProperty("producerVersion", out var producer) ? producer.GetString() : null,
        snapshotCount,
        finitePoseCount,
        exactFloorKnownCount,
        floorBands = bandCounts,
        playerPlanarBounds = finitePoseCount == 0 ? null : new { minX = minPlanarX, maxX = maxPlanarX, minZ = minPlanarZ, maxZ = maxPlanarZ },
        uniqueContainmentCount,
        multipleBoxCount,
        noCandidateCount,
        uniqueCandidateLayers = layerCounts,
        uniqueCandidateRoomIndices = indexCounts,
        consecutiveTransitionsWithTwoUniqueMatches = sameCellTransitionCount + orthogonalNeighborIndexTransitionCount + nonNeighborIndexTransitionCount + crossVariantTransitionCount + crossFloorBandTransitionCount,
        sameCellTransitionCount,
        orthogonalNeighborIndexTransitionCount,
        nonNeighborIndexTransitionCount,
        crossVariantTransitionCount,
        crossFloorBandTransitionCount,
        unclassifiedTransitionCount,
        interpretation = "geometric candidates only; bounds are uncalibrated and do not identify active variant, current room, open passage, or traversability",
    };
}

static bool TryGetEurekaOrthosBand(JsonElement snapshot, out string band)
{
    band = "unknown";
    if (!snapshot.TryGetProperty("dungeon", out var dungeon)
        || dungeon.ValueKind != JsonValueKind.Object
        || !dungeon.TryGetProperty("kind", out var kind)
        || kind.GetString() != "eurekaOrthos"
        || !dungeon.TryGetProperty("floorRange", out var range)
        || range.ValueKind != JsonValueKind.Object
        || !range.TryGetProperty("start", out var start)
        || !range.TryGetProperty("end", out var end)
        || !start.TryGetInt32(out var startFloor)
        || !end.TryGetInt32(out var endFloor))
        return false;

    band = $"{startFloor}-{endFloor}";
    return true;
}

static void AddCount<TKey>(IDictionary<TKey, int> counts, TKey key) where TKey : notnull
{
    counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;
}

readonly record struct CandidateRoomBox(string Layer, int Index, double MinX, double MaxX, double MinZ, double MaxZ);
readonly record struct CandidateCell(string FloorBand, string Layer, int Index);
