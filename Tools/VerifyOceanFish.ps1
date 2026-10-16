param(
    [ValidateRange(48,3600)][int]$Seconds=600,
    [string]$UnityEditorDirectory='C:/Program Files/Unity 2022.3.62f2/Editor'
)

$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$taskBase=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono'
$taskEngine=Join-Path $UnityEditorDirectory 'Data/Managed/UnityEngine'
$taskCompiler=Join-Path $taskBase '4.5/csc.exe'
$taskOutput=Join-Path $taskRoot 'Logs'
$taskReferences=@('/nologo','/nostdlib+',
    "/r:$taskBase/unityjit-win32/mscorlib.dll",
    "/r:$taskBase/unityjit-win32/System.dll",
    "/r:$taskBase/unityjit-win32/System.Core.dll",
    "/r:$taskBase/unityjit-win32/Facades/netstandard.dll",
    "/r:$taskEngine/UnityEngine.CoreModule.dll")
$taskOriginalMonoPath=$env:MONO_PATH
if(!(Test-Path -LiteralPath $taskMono)){throw 'Unity Mono runtime is missing; specify UnityEditorDirectory.'}
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
Push-Location -LiteralPath $taskRoot
try {
    # The compiler must use its Windows runtime, not the Unix assemblies in mono/4.5.
    $env:MONO_PATH=$null
    & $taskMono $taskCompiler @taskReferences /out:Logs/VerifyOceanFishBoids.exe Tools/VerifyOceanFishBoids.cs Assets/Scripts/Ocean/OceanFishBoids.cs
    if($LASTEXITCODE -ne 0){throw 'Boids check compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/VerifyOceanFishBoids.exe
    if($LASTEXITCODE -ne 0){throw 'Boids neighbourhood check failed.'}
    $env:MONO_PATH=$null
    $taskSources=@('Tools/VerifyOceanFishSteering.cs','Assets/Scripts/Ocean/OceanFishBoids.cs',
        'Assets/Scripts/Ocean/OceanFishSteering.cs','Assets/Scripts/Ocean/OceanSchool.cs',
        'Assets/Scripts/Ocean/OceanFishMotion.cs','Assets/Scripts/Ocean/OceanSwimNavigation.cs',
        'Assets/Scripts/Ocean/WildlifeAlarm.cs','Assets/Scripts/Ocean/OceanGeography.cs',
        'Assets/Scripts/Interaction/IslandLayout.cs','Assets/Scripts/Ocean/OceanFishPopulation.cs',
        'Assets/Scripts/Ocean/OceanFishAnimation.cs','Assets/Scripts/Ocean/OceanFishPassage.cs')
    & $taskMono $taskCompiler @taskReferences /main:VerifyOceanFishPopulation /out:Logs/VerifyOceanFishPopulation.exe @taskSources Tools/VerifyOceanFishPopulation.cs
    if($LASTEXITCODE -ne 0){throw 'Population check compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/VerifyOceanFishPopulation.exe
    if($LASTEXITCODE -ne 0){throw 'Seeded population check failed.'}
    $env:MONO_PATH=$null
    & $taskMono $taskCompiler @taskReferences /main:VerifyOceanFishBody /out:Logs/VerifyOceanFishBody.exe @taskSources Tools/VerifyOceanFishBody.cs
    if($LASTEXITCODE -ne 0){throw 'Fish body check compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/VerifyOceanFishBody.exe
    if($LASTEXITCODE -ne 0){throw 'Fish body / rhythm check failed.'}
    $env:MONO_PATH=$null
    & $taskMono $taskCompiler @taskReferences /main:VerifyOceanFishPassage /out:Logs/VerifyOceanFishPassage.exe @taskSources Tools/VerifyOceanFishPassage.cs
    if($LASTEXITCODE -ne 0){throw 'Fish passage check compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/VerifyOceanFishPassage.exe
    if($LASTEXITCODE -ne 0){throw 'Fish split / regroup check failed.'}
    $env:MONO_PATH=$null
    & $taskMono $taskCompiler @taskReferences /out:Logs/VerifyOceanFishSteering.exe @taskSources
    if($LASTEXITCODE -ne 0){throw 'Steering check compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/VerifyOceanFishSteering.exe . $Seconds 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'OceanFishManagedVerification.txt')
    if($LASTEXITCODE -ne 0){throw 'Steering / baked habitat check failed.'}
}
finally {
    $env:MONO_PATH=$taskOriginalMonoPath
    Pop-Location
}
