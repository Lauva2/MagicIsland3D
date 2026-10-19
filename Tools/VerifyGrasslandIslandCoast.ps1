param([string]$UnityEditorDirectory='C:/Program Files/Unity 2022.3.62f2/Editor')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$taskBase=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono'
$taskOriginalMonoPath=$env:MONO_PATH
Push-Location -LiteralPath $taskRoot
try {
    [IO.Directory]::CreateDirectory('Logs/GrasslandCoast') | Out-Null
    $env:MONO_PATH=$null
    & $taskMono "$taskBase/4.5/csc.exe" /nologo /nostdlib+ "/r:$taskBase/unityjit-win32/mscorlib.dll" "/r:$taskBase/unityjit-win32/System.dll" /out:Logs/GrasslandCoast/Rules.exe Tools/VerifyGrasslandIslandCoast.cs Tools/Fixtures/BiomeIslandsBeforeCoast.cs Assets/Scripts/Interaction/GrasslandIslandCoast.cs Assets/Scripts/Interaction/BiomeIslandLayout.cs Assets/Scripts/Interaction/StarterIslandLayout.cs Assets/Scripts/Interaction/IslandLayout.cs
    if($LASTEXITCODE -ne 0){throw 'Coast rules compilation failed.'}
    & $taskMono Logs/GrasslandCoast/Rules.exe
    if($LASTEXITCODE -ne 0){throw 'Coast rules verification failed.'}
}
finally {$env:MONO_PATH=$taskOriginalMonoPath;Pop-Location}
