using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LuminaMapProbe <Lumina.dll-directory> <sqpack-directory> [virtual-path|--public-api-surface]");
    return 2;
}

var luminaDirectory = Path.GetFullPath(args[0]);
var sqpackDirectory = Path.GetFullPath(args[1]);
var apiSurfaceOnly = args.Length > 2 && args[2] == "--public-api-surface";
var virtualPath = args.Length > 2 && !apiSurfaceOnly ? args[2] : null;
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
