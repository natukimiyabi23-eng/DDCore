param(
    [Parameter(Mandatory = $true)]
    [string]$BossModPath,

    [string]$Commit = '6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath (Join-Path $BossModPath '.git'))) {
    throw "BossModPath is not a Git checkout: $BossModPath"
}

function Invoke-Git([string[]]$Arguments) {
    $output = & git -C $BossModPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }

    return @($output)
}

function Get-BlobLines([string]$Path) {
    return @(Invoke-Git @('show', "${Commit}:$Path"))
}

$allPaths = Invoke-Git @('ls-tree', '-r', '--name-only', $Commit)
$direct = @($allPaths | Where-Object { $_ -match '^BossMod\.Modules/.*/DeepDungeon/' })
$support = @(
    'BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs'
    'BossMod/Data/DeepDungeonState.cs'
)
$crossCutting = @(
    'BossMod/BossModule/AIHints.cs'
    'BossMod/ActionQueue/ActionQueue.cs'
)

$missing = @($support + $crossCutting | Where-Object { $_ -notin $allPaths })
if ($missing.Count -gt 0) {
    throw "Pinned paths are missing: $($missing -join ', ')"
}

$families = [ordered]@{
    'BossMod.WorldModel' = '\b(WorldState|Actor|ActorCastInfo|ActorCastEvent|ActorStatus|PartyState)\b'
    'BossMod.Decision' = '\b(AIHints|ActionQueue|GoalZones|PotentialTargets|ForbiddenZones)\b'
    'BossMod.Geometry' = '\b(WPos|WDir|Angle|AOEShape|ShapeDistance|Sdf|ArenaBounds|Bitmap)\b'
    'BossMod.Framework' = '\b(ZoneModule|Service|EventSubscriptions|ReplayRecorder|ConfigNode)\b'
    'FFXIVClientStructs' = '\bFFXIVClientStructs\b|\bInstanceContentDeepDungeon\b|\bRoomFlags\b'
    'Dalamud' = '\bDalamud\b|\bICallGateSubscriber\b|\bIpcNotReadyError\b|\bImGui\b'
    'Lumina' = '\bLumina\b|\bLgbFile\b|\bTerritoryType\b|\bDeepDungeonLayer\b'
    'ResourceData' = '\bLoadResource\b|\.json\b'
}

$symbolPatterns = [ordered]@{
    'WorldState' = '\bWorldState\b'
    'Actor' = '\bActor\b'
    'DeepDungeonState' = '\bDeepDungeonState\b'
    'AIHints' = '\bAIHints\b'
    'ActionQueue' = '\bActionQueue\b'
    'ZoneModule' = '\bZoneModule\b'
    'Service' = '\bService\b'
    'AOEShape' = '\bAOEShape[A-Za-z0-9_]*\b'
    'WPos' = '\bWPos\b'
    'RoomFlags' = '\bRoomFlags\b'
}

function Measure-File([string]$Path, [string]$Scope) {
    $lines = Get-BlobLines $Path
    $text = $lines -join "`n"
    $dependencyFamilies = @($families.GetEnumerator() | Where-Object { $text -match $_.Value } | ForEach-Object Key)
    $symbols = [ordered]@{}
    foreach ($entry in $symbolPatterns.GetEnumerator()) {
        $count = [regex]::Matches($text, $entry.Value).Count
        if ($count -gt 0) {
            $symbols[$entry.Key] = $count
        }
    }

    [pscustomobject]@{
        Scope = $Scope
        Path = $Path
        Extension = [IO.Path]::GetExtension($Path)
        PhysicalLines = $lines.Count
        DependencyFamilies = $dependencyFamilies
        MatchedSymbols = $symbols
    }
}

$files = @(
    $direct | ForEach-Object { Measure-File $_ 'DD direct' }
    $support | ForEach-Object { Measure-File $_ 'DD support' }
    $crossCutting | ForEach-Object { Measure-File $_ 'Cross-cutting' }
)

$scopeSummary = @($files | Group-Object Scope | ForEach-Object {
    [pscustomobject]@{
        Scope = $_.Name
        Files = $_.Count
        PhysicalLines = ($_.Group | Measure-Object PhysicalLines -Sum).Sum
        CSharpFiles = @($_.Group | Where-Object Extension -eq '.cs').Count
        JsonFiles = @($_.Group | Where-Object Extension -eq '.json').Count
    }
})

$dependencySummary = @($families.Keys | ForEach-Object {
    $family = $_
    [pscustomobject]@{
        Family = $family
        Files = @($files | Where-Object { $family -in $_.DependencyFamilies }).Count
    }
})

$result = [pscustomobject]@{
    SchemaVersion = 1
    Commit = $Commit
    Measurement = [pscustomobject]@{
        PhysicalLineDefinition = 'Count of every line emitted by git show, including blank lines.'
        DependencyDefinition = 'Lexical occurrence by documented regex family; this is not a compiler-semantic dependency graph.'
    }
    Totals = [pscustomobject]@{
        Files = $files.Count
        PhysicalLines = ($files | Measure-Object PhysicalLines -Sum).Sum
        CSharpFiles = @($files | Where-Object Extension -eq '.cs').Count
        JsonFiles = @($files | Where-Object Extension -eq '.json').Count
    }
    Scopes = $scopeSummary
    Dependencies = $dependencySummary
    Files = $files
}

$result | ConvertTo-Json -Depth 8
