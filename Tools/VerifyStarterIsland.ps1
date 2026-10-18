param([string]$UnityEditorDirectory='E:/UnityHub/Editor')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$taskBase=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono'
$taskOriginalMonoPath=$env:MONO_PATH
Push-Location -LiteralPath $taskRoot
try {
    [IO.Directory]::CreateDirectory('Logs/StarterWorld') | Out-Null
    $env:MONO_PATH=$null
    & $taskMono "$taskBase/4.5/csc.exe" /nologo /nostdlib+ "/r:$taskBase/unityjit-win32/mscorlib.dll" "/r:$taskBase/unityjit-win32/System.dll" /out:Logs/StarterWorld/Rules.exe Tools/VerifyStarterIsland.cs Assets/Scripts/Interaction/StarterIslandLayout.cs Assets/Scripts/Interaction/IslandLayout.cs Assets/Scripts/Interaction/HousingState.cs
    if($LASTEXITCODE -ne 0){throw 'World rule compilation failed.'}
    & $taskMono Logs/StarterWorld/Rules.exe
    if($LASTEXITCODE -ne 0){throw 'World rule verification failed.'}
}
finally {$env:MONO_PATH=$taskOriginalMonoPath;Pop-Location}
