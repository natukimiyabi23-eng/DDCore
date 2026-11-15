using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LuminaMapProbe <Lumina.dll-directory> <sqpack-directory> [virtual-path|--public-api-surface|--deep-dungeon-grids|--deep-dungeon-sheet-surface|--deep-dungeon-room-levels|--deep-dungeon-wall-crosswalk <Walls.json>|--replay-room-candidates <export-directory>|--validate-room-labels <replay.json> <labels.json>|--replay-room-box-trace <replay.json>]");
    return 2;
}

var luminaDirectory = Path.GetFullPath(args[0]);
var sqpackDirectory = Path.GetFullPath(args[1]);
var apiSurfaceOnly = args.Length > 2 && args[2] == "--public-api-surface";
var deepDungeonGridsOnly = args.Length > 2 && args[2] == "--deep-dungeon-grids";
var deepDungeonSheetSurfaceOnly = args.Length > 2 && args[2] == "--deep-dungeon-sheet-surface";
var deepDungeonRoomLevelsOnly = args.Length > 2 && args[2] == "--deep-dungeon-room-levels";
var deepDungeonWallCrosswalk = args.Length > 2 && args[2] == "--deep-dungeon-wall-crosswalk";
var replayRoomCandidates = args.Length > 2 && args[2] == "--replay-room-candidates";
var validateRoomLabels = args.Length > 2 && args[2] == "--validate-room-labels";
var replayRoomBoxTrace = args.Length > 2 && args[2] == "--replay-room-box-trace";
var virtualPath = args.Length > 2 && !apiSurfaceOnly && !deepDungeonGridsOnly && !deepDungeonSheetSurfaceOnly && !deepDungeonRoomLevelsOnly && !deepDungeonWallCrosswalk && !replayRoomCandidates && !validateRoomLabels && !replayRoomBoxTrace ? args[2] : null;
if (deepDungeonWallCrosswalk && args.Length < 4)
{
    Console.Error.WriteLine("--deep-dungeon-wall-crosswalk requires a pinned BossMod Walls.json path.");
    return 2;
}
if (replayRoomCandidates && args.Length < 4)
{
    Console.Error.WriteLine("--replay-room-candidates requires an export-directory argument.");
    return 2;
}
if (validateRoomLabels && args.Length < 5)
{
    Console.Error.WriteLine("--validate-room-labels requires a replay JSON and a user-authored labels JSON.");
    return 2;
}
if (replayRoomBoxTrace && args.Length < 4)
{
    Console.Error.WriteLine("--replay-room-box-trace requires one schema-5 replay JSON.");
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

if (deepDungeonSheetSurfaceOnly)
{
    var sheets = excelAssembly.GetExportedTypes()
        .Where(static type => type.Namespace == "Lumina.Excel.Sheets" && (type.Name.Contains("DeepDungeon", StringComparison.OrdinalIgnoreCase) || type.Name == "Level"))
        .OrderBy(static type => type.Name, StringComparer.Ordinal)
        .Select(static type => new
        {
            sheet = type.Name,
            publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static property => $"{property.PropertyType.FullName} {property.Name}")
                .Order(StringComparer.Ordinal)
                .ToArray(),
            publicFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(static field => $"{field.FieldType.FullName} {field.Name}")
                .Order(StringComparer.Ordinal)
                .ToArray(),
        })
        .ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new { luminaAssemblyVersion = version, sheetCount = sheets.Length, sheets }, new JsonSerializerOptions { WriteIndented = true }));
    return sheets.Length > 0 ? 0 : 1;
}

if (deepDungeonRoomLevelsOnly)
{
    var levelSummary = ReadEurekaRoomLevelSummary(gameData, gameDataType, excelAssembly, excelModule);
    Console.WriteLine(JsonSerializer.Serialize(levelSummary.Report, new JsonSerializerOptions { WriteIndented = true }));
    return levelSummary.RoomSetCount > 0 ? 0 : 1;
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
var planmapSha256 = Convert.ToHexString(SHA256.HashData(data));
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

if (deepDungeonWallCrosswalk)
{
    var crosswalk = CompareStaticWallTemplates(args[3], roomCandidates, planmapSha256);
    Console.WriteLine(JsonSerializer.Serialize(crosswalk.Report, new JsonSerializerOptions { WriteIndented = true }));
    return crosswalk.IsComplete ? 0 : 1;
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

if (replayRoomCandidates || validateRoomLabels || replayRoomBoxTrace)
{
    var gridReport = ReadEurekaOrthosGrids(gameData, gameDataType, excelAssembly);
    object report;
    int resultCount;
    try
    {
        if (replayRoomCandidates)
        {
            var replayReport = ReadReplayRoomCandidateCollection(args[3], roomCandidates, gridReport.FloorSets);
            report = replayReport.Report;
            resultCount = replayReport.SelectedReplayCount;
        }
        else
        {
            var bytes = File.ReadAllBytes(args[3]);
            using var replayDocument = JsonDocument.Parse(bytes);
            var boxes = roomCandidates.Select(candidate =>
            {
                var bounds = GetAnonymousProperty<object>(candidate, "boundsUsingBossModConvention");
                return new ReplayRoomCandidateBox(
                    GetAnonymousProperty<string>(candidate, "layer"),
                    GetAnonymousProperty<int>(candidate, "priority") - 100,
                    Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minX")),
                    Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxX")),
                    Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minZ")),
                    Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxZ")));
            }).Where(static box => box.Index is >= 0 and < 25).ToArray();
            var replaySha256 = Convert.ToHexString(SHA256.HashData(bytes));
            if (validateRoomLabels)
            {
                var labelsBytes = File.ReadAllBytes(args[4]);
                using var labelsDocument = JsonDocument.Parse(labelsBytes);
                var labels = JsonSerializer.Deserialize<ReplayRoomLabelSet>(labelsDocument.RootElement.GetRawText(), ReplayRoomLabelEvaluator.JsonOptions)
                    ?? throw new InvalidDataException("The labels JSON was empty.");
                var summary = ReplayRoomLabelEvaluator.Evaluate(replayDocument.RootElement, replaySha256, planmapSha256, labels, boxes);
                var labelCanonical = JsonSerializer.Serialize(summary);
                report = new
                {
                    schemaVersion = 2,
                    replaySha256,
                    planmapSha256,
                    evidenceMode = "user-authored labels compared with static candidate boxes; not live topology or traversability",
                    summary,
                    canonicalSummarySha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(labelCanonical))),
                };
                resultCount = labels.Labels.Count;
            }
            else
            {
                var trace = ReplayRoomLabelEvaluator.CreateBoxTrace(replayDocument.RootElement, boxes);
                var canonicalTrace = JsonSerializer.Serialize(trace);
                report = new
                {
                    schemaVersion = 1,
                    replaySha256,
                    planmapSha256,
                    evidenceMode = "per-snapshot containment in uncalibrated static room boxes; not room identity, active layout, topology, or traversability",
                    trace,
                    canonicalTraceSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalTrace))),
                };
                resultCount = trace.Count;
            }
        }
    }
    catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or OverflowException)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = true }));
    return resultCount > 0 ? 0 : 1;
}

var canonical = JsonSerializer.Serialize(new { summaries, countsByAssetType, objectTypeSummaries, roomCandidates, sharedGroupSummaries, referencedSgbSummaries });
var canonicalHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
var digest = planmapSha256;
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
            roomCReferenceRowId = Convert.ToUInt32(GetMember(GetMember(layer, "RoomC"), "RowId")),
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

static (int RoomSetCount, int UnresolvedLevelReferenceCount, object Report) ReadEurekaRoomLevelSummary(object gameData, Type gameDataType, Assembly excelAssembly, object? excelModule)
{
    static IEnumerable ReadSheet(object gameData, Type gameDataType, Assembly excelAssembly, string sheetName)
    {
        var rowType = excelAssembly.GetType($"Lumina.Excel.Sheets.{sheetName}", throwOnError: true)!;
        var getSheet = gameDataType.GetMethods().Single(method => method.Name == "GetExcelSheet" && method.IsGenericMethod)
            .MakeGenericMethod(rowType);
        return (IEnumerable)getSheet.Invoke(gameData, [null, null])!;
    }

    var roomRowIds = new HashSet<uint>();
    foreach (var layer in ReadSheet(gameData, gameDataType, excelAssembly, "DeepDungeonLayer"))
    {
        var dungeon = GetPublicProperty(layer, "DeepDungeon");
        if (Convert.ToUInt32(GetPublicProperty(dungeon, "RowId")) != 3)
            continue;

        foreach (var variantName in new[] { "RoomA", "RoomB" })
        {
            var mapReference = GetPublicProperty(layer, variantName);
            foreach (var mapRow in (IEnumerable)GetPublicProperty(mapReference, "Value")!)
            {
                foreach (var roomReference in (IEnumerable)GetPublicProperty(mapRow, "DeepDungeonRoom")!)
                {
                    var rowId = Convert.ToUInt32(GetPublicProperty(roomReference, "RowId"));
                    if (rowId != 0)
                        roomRowIds.Add(rowId);
                }
            }
        }
    }

    var rooms = ReadSheet(gameData, gameDataType, excelAssembly, "DeepDungeonRoom")
        .Cast<object>()
        .Where(room => roomRowIds.Contains(Convert.ToUInt32(GetPublicProperty(room, "RowId"))))
        .ToDictionary(room => Convert.ToUInt32(GetPublicProperty(room, "RowId")));
    var levelRowIds = new HashSet<uint>();
    var levelReferenceCount = 0;
    foreach (var room in rooms.Values)
    {
        foreach (var levelReference in (IEnumerable)GetPublicProperty(room, "Level")!)
        {
            levelReferenceCount++;
            var rowId = Convert.ToUInt32(GetPublicProperty(levelReference, "RowId"));
            if (rowId != 0)
                levelRowIds.Add(rowId);
        }
    }

    var levelType = excelAssembly.GetType("Lumina.Excel.Sheets.Level", throwOnError: true)!;
    var getSheet = excelModule?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Single(method => method.Name == "GetSheet" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2);
    var levelSheet = getSheet?.MakeGenericMethod(levelType).Invoke(excelModule, [null, "Level"]);
    var getRow = levelSheet?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(method => method.Name == "GetRow" && method.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(uint));
    var levels = new Dictionary<uint, object>();
    var levelRowLookupFailureCount = 0;
    foreach (var rowId in levelRowIds)
    {
        try
        {
            var level = getRow?.Invoke(levelSheet, [rowId]);
            if (level is not null)
                levels.Add(Convert.ToUInt32(GetPublicProperty(level, "RowId")), level);
        }
        catch (TargetInvocationException)
        {
            levelRowLookupFailureCount++;
        }
    }
    var territoryCounts = new SortedDictionary<uint, int>();
    var mapCounts = new SortedDictionary<uint, int>();
    var typeCounts = new SortedDictionary<byte, int>();
    var finitePositionCount = 0;

    foreach (var level in levels.Values)
    {
        var territoryId = Convert.ToUInt32(GetPublicProperty(GetPublicProperty(level, "Territory"), "RowId"));
        var mapId = Convert.ToUInt32(GetPublicProperty(GetPublicProperty(level, "Map"), "RowId"));
        var type = Convert.ToByte(GetPublicProperty(level, "Type"));
        territoryCounts[territoryId] = territoryCounts.GetValueOrDefault(territoryId) + 1;
        mapCounts[mapId] = mapCounts.GetValueOrDefault(mapId) + 1;
        typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;
        if (float.IsFinite(Convert.ToSingle(GetPublicProperty(level, "X")))
            && float.IsFinite(Convert.ToSingle(GetPublicProperty(level, "Y")))
            && float.IsFinite(Convert.ToSingle(GetPublicProperty(level, "Z")))
            && float.IsFinite(Convert.ToSingle(GetPublicProperty(level, "Yaw")))
            && float.IsFinite(Convert.ToSingle(GetPublicProperty(level, "Radius"))))
            finitePositionCount++;
    }

    var report = new
    {
        dungeonRowId = 3,
        variantGridCount = 20,
        roomSetCount = roomRowIds.Count,
        resolvedRoomRowCount = rooms.Count,
        levelReferenceCount,
        uniqueLevelRowCount = levelRowIds.Count,
        resolvedLevelRowCount = levels.Count,
        unresolvedLevelReferenceCount = levelRowIds.Count - levels.Count,
        levelRowLookupFailureCount,
        finitePositionCount,
        territoryRowIdCounts = territoryCounts,
        mapRowIdCounts = mapCounts,
        levelTypeCounts = typeCounts,
        interpretation = "static references and coordinate metadata only; not current randomized layout, room identity, passage state, or walkability",
    };
    return (roomRowIds.Count, levelRowIds.Count - levels.Count, report);
}

static (bool IsComplete, object Report) CompareStaticWallTemplates(string wallsJsonPath, List<object> roomCandidates, string planmapSha256)
{
    if (!File.Exists(wallsJsonPath))
        throw new FileNotFoundException("Pinned BossMod Walls.json was not found.");

    var wallsBytes = File.ReadAllBytes(wallsJsonPath);
    using var wallsDocument = JsonDocument.Parse(wallsBytes);
    var boxes = new Dictionary<(char Variant, int Index), List<(double MinX, double MaxX, double MinZ, double MaxZ)>>();
    foreach (var candidate in roomCandidates)
    {
        var layer = GetAnonymousProperty<string>(candidate, "layer");
        var variant = layer.EndsWith("A", StringComparison.Ordinal) ? 'A'
            : layer.EndsWith("B", StringComparison.Ordinal) ? 'B'
            : '\0';
        var index = GetAnonymousProperty<int>(candidate, "priority") - 100;
        if (variant is '\0' || index is < 0 or >= 25)
            continue;

        var bounds = GetAnonymousProperty<object>(candidate, "boundsUsingBossModConvention");
        var key = (variant, index);
        if (!boxes.TryGetValue(key, out var cellBoxes))
        {
            cellBoxes = [];
            boxes.Add(key, cellBoxes);
        }
        cellBoxes.Add((
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minX")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxX")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "minZ")),
            Convert.ToDouble(GetAnonymousProperty<object>(bounds, "maxZ"))));
    }

    var floorSetResults = new List<object>();
    var pooledDistances = new Dictionary<(char WallVariant, char BoxVariant), List<double>>();
    var pooledMissing = new Dictionary<(char WallVariant, char BoxVariant), int>();
    var invalidTemplateCount = 0;
    var allFloorKeysPresent = true;

    foreach (var floorSet in Enumerable.Range(1, 10))
    {
        var key = $"3.{floorSet}";
        if (!wallsDocument.RootElement.TryGetProperty(key, out var floorElement))
        {
            allFloorKeysPresent = false;
            continue;
        }

        var comparisons = new List<object>();
        foreach (var wallVariant in new[] { 'A', 'B' })
        {
            var wallRooms = floorElement.GetProperty($"Rooms{wallVariant}").GetProperty("Rooms");
            if (wallRooms.GetArrayLength() != 25)
            {
                invalidTemplateCount++;
                continue;
            }

            var roomWalls = wallRooms.EnumerateArray().Select(static room => ReadRoomWalls(room)).ToArray();
            if (roomWalls.Sum(static wallsForRoom => wallsForRoom.NonzeroDepthCount) != 28
                || roomWalls.Any(static wallsForRoom => wallsForRoom.InvalidValues))
                invalidTemplateCount++;

            foreach (var boxVariant in new[] { 'A', 'B' })
            {
                var distances = new List<double>();
                var missingBoxCount = 0;
                for (var index = 0; index < roomWalls.Length; index++)
                {
                    foreach (var wall in roomWalls[index].Walls)
                    {
                        if (wall.Depth <= 0)
                            continue;
                        if (!boxes.TryGetValue((boxVariant, index), out var bounds))
                        {
                            missingBoxCount++;
                            continue;
                        }

                        distances.Add(bounds.Select(box => StaticWallGeometryCrosswalk.DistanceToNearestSupportingLine(
                                wall.X,
                                wall.Z,
                                wall.Direction,
                                box.MinX,
                                box.MaxX,
                                box.MinZ,
                                box.MaxZ))
                            .Min());
                    }
                }

                var comparisonKey = (wallVariant, boxVariant);
                if (!pooledDistances.TryGetValue(comparisonKey, out var pooled))
                {
                    pooled = [];
                    pooledDistances.Add(comparisonKey, pooled);
                    pooledMissing.Add(comparisonKey, 0);
                }
                pooled.AddRange(distances);
                pooledMissing[comparisonKey] += missingBoxCount;

                comparisons.Add(new
                {
                    wallTemplate = wallVariant.ToString(),
                    boxLayer = boxVariant.ToString(),
                    wallPointCount = distances.Count,
                    missingBoxCount,
                    medianSupportingLineDistance = Median(distances),
                    within5Units = distances.Count(static value => value <= 5),
                    within10Units = distances.Count(static value => value <= 10),
                });
            }
        }

        floorSetResults.Add(new { floorset = key, comparisons });
    }

    var overallComparisons = pooledDistances
        .OrderBy(static pair => pair.Key.WallVariant)
        .ThenBy(static pair => pair.Key.BoxVariant)
        .Select(pair => new
        {
            wallTemplate = pair.Key.WallVariant.ToString(),
            boxLayer = pair.Key.BoxVariant.ToString(),
            wallPointCount = pair.Value.Count,
            missingBoxCount = pooledMissing[pair.Key],
            medianSupportingLineDistance = Median(pair.Value),
            within5Units = pair.Value.Count(static value => value <= 5),
            within10Units = pair.Value.Count(static value => value <= 10),
        })
        .ToArray();
    var summaryCore = new
    {
        dungeonRowId = 3,
        floorSetCount = floorSetResults.Count,
        allFloorKeysPresent,
        roomCandidateShapeCount = roomCandidates.Count,
        roomCandidateCellCount = boxes.Count,
        cellsWithMultipleCandidateShapes = boxes.Values.Count(static candidates => candidates.Count > 1),
        invalidTemplateCount,
        planmapSha256,
        wallsJsonSha256 = Convert.ToHexString(SHA256.HashData(wallsBytes)),
        metric = "distance to nearest orientation-matched infinite edge line using static planmap bounds Translation +/- abs(Scale); ignores segment extent and is uncalibrated",
        floorSetResults,
        overallComparisons,
        inferenceLimit = "static geometry comparison only; not active layout, room identity, wall truth, passage state, collision, or walkability",
    };
    var summarySha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(summaryCore))));
    var complete = floorSetResults.Count == 10 && allFloorKeysPresent && roomCandidates.Count == 53 && boxes.Count == 42
        && invalidTemplateCount == 0 && pooledMissing.Values.All(static missing => missing == 0)
        && pooledDistances.Values.All(static distances => distances.Count == 280);
    return (complete, new { summaryCore, summarySha256 });

    static (IReadOnlyList<(string Direction, double X, double Z, double Depth)> Walls, int NonzeroDepthCount, bool InvalidValues) ReadRoomWalls(JsonElement room)
    {
        var walls = new List<(string Direction, double X, double Z, double Depth)>();
        var nonzeroDepthCount = 0;
        var invalidValues = false;
        foreach (var direction in new[] { "North", "South", "West", "East" })
        {
            var edge = room.GetProperty(direction);
            var depth = edge.GetProperty("Depth").GetDouble();
            var x = edge.GetProperty("Position").GetProperty("X").GetDouble();
            var z = edge.GetProperty("Position").GetProperty("Z").GetDouble();
            if (!double.IsFinite(depth) || !double.IsFinite(x) || !double.IsFinite(z))
            {
                invalidValues = true;
                continue;
            }
            if (depth != 0)
                nonzeroDepthCount++;
            walls.Add((direction, x, z, depth));
        }
        return (walls, nonzeroDepthCount, invalidValues);
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}

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
    var noCandidateDistances = new List<double>();
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
            if (boxes.Length > 0)
            {
                var nearestDistance = boxes.Min(box =>
                {
                    var deltaX = Math.Max(0, Math.Max(box.MinX - x, x - box.MaxX));
                    var deltaZ = Math.Max(0, Math.Max(box.MinZ - z, z - box.MaxZ));
                    return Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                });
                noCandidateDistances.Add(nearestDistance);
            }
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

    var sortedNoCandidateDistances = noCandidateDistances.Order().ToArray();
    var noCandidateDistanceCount = sortedNoCandidateDistances.Length;
    double? noCandidateDistanceMedian = noCandidateDistanceCount == 0
        ? null
        : noCandidateDistanceCount % 2 == 0
            ? (sortedNoCandidateDistances[(noCandidateDistanceCount / 2) - 1] + sortedNoCandidateDistances[noCandidateDistanceCount / 2]) / 2
            : sortedNoCandidateDistances[noCandidateDistanceCount / 2];
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
        noCandidateNearestBoxDistance = sortedNoCandidateDistances.Length == 0 ? null : new
        {
            count = sortedNoCandidateDistances.Length,
            minimum = sortedNoCandidateDistances[0],
            median = noCandidateDistanceMedian,
            p90 = sortedNoCandidateDistances[(int)Math.Ceiling(0.9 * sortedNoCandidateDistances.Length) - 1],
            maximum = sortedNoCandidateDistances[^1],
            metric = "Euclidean distance in projected X/Z coordinates to nearest uncalibrated axis-aligned candidate room box; median averages the two middle values for even counts; P90 uses nearest-rank ceil(0.9*n); not a corridor or navigability measurement",
        },
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
