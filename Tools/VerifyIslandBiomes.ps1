param([string]$UnityEditorDirectory='E:/UnityHub/Editor')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$taskBase=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono'
$taskEngine=Join-Path $UnityEditorDirectory 'Data/Managed/UnityEngine'
$taskOriginalMonoPath=$env:MONO_PATH
Push-Location -LiteralPath $taskRoot
try {
    [IO.Directory]::CreateDirectory('Logs/IslandBiomes') | Out-Null
    $env:MONO_PATH=$null
    & $taskMono "$taskBase/4.5/csc.exe" /nologo /nostdlib+ "/r:$taskBase/unityjit-win32/mscorlib.dll" "/r:$taskBase/unityjit-win32/System.dll" "/r:$taskBase/unityjit-win32/System.Core.dll" "/r:$taskBase/unityjit-win32/Facades/netstandard.dll" "/r:$taskEngine/UnityEngine.CoreModule.dll" /out:Logs/IslandBiomes/Rules.exe Tools/VerifyIslandBiomes.cs Assets/Scripts/Terrain/IslandBiomeSettings.cs Assets/Scripts/Interaction/IslandLayout.cs Assets/Scripts/Interaction/StarterIslandLayout.cs
    if($LASTEXITCODE -ne 0){throw 'Biome rule compilation failed.'}
    $env:MONO_PATH=$taskEngine
    & $taskMono Logs/IslandBiomes/Rules.exe
    if($LASTEXITCODE -ne 0){throw 'Biome rule verification failed.'}
}
finally {$env:MONO_PATH=$taskOriginalMonoPath;Pop-Location}
