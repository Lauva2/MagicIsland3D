param([Parameter(Mandatory=$true)][string]$UnityEditorDirectory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMono=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe'
$taskCompiler=Join-Path $UnityEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
if(!(Test-Path -LiteralPath $taskMono)){throw 'Specify the Unity 2022.3.62f2 Editor directory.'}
Push-Location -LiteralPath $taskRoot
try {
    [IO.Directory]::CreateDirectory((Join-Path $taskRoot 'Logs')) | Out-Null
    $taskSources=Get-ChildItem -LiteralPath 'Assets/Scripts' -Recurse -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](UI|Managers|Configuration)[\\/]' -and
        [IO.File]::ReadAllText($_.FullName) -notmatch 'UnityEngine|UnityEditor|TMPro'
    } | ForEach-Object {$_.FullName}
    foreach($taskName in @('VerifyMagic','VerifyBeachForage','VerifyMagicExploration')) {
        $taskOutput=Join-Path $taskRoot "Logs/$taskName.exe"
        & $taskMono $taskCompiler /nologo /langversion:latest /r:System.Numerics.dll "/out:$taskOutput" "Tools/$taskName.cs" @taskSources
        if($LASTEXITCODE -ne 0){throw "$taskName compilation failed"}
        & $taskMono $taskOutput
        if($LASTEXITCODE -ne 0){throw "$taskName failed"}
    }
} finally {Pop-Location}
