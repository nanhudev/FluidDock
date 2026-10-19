param([switch]$Test)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if(!(Test-Path (Join-Path $framework 'csc.exe'))){$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'}
$out=Join-Path $root 'bin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$refs=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')|ForEach-Object {'/r:'+(Join-Path $framework ('WPF\'+$_))}
$sources=@(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs'|ForEach-Object FullName)
$model=Join-Path $root 'models\intent-model.json.gz'
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ /out:"$out\FluidDock.exe" /win32icon:"$root\assets\buddy.ico" /win32manifest:"$root\app.manifest" /resource:"$model,FluidDock.IntentModel.gz" /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.Data.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll @refs @sources
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
if($Test){
    $testData=Join-Path $root 'test-results'
    New-Item -ItemType Directory -Path $testData -Force|Out-Null
    $process=Start-Process -FilePath "$out\FluidDock.exe" -ArgumentList @('--self-test','--data-dir',('"'+$testData+'"')) -PassThru -Wait -WindowStyle Hidden
    $result=Get-Content (Join-Path $testData 'self-test.json') -Raw|ConvertFrom-Json
    $result.cases|Where-Object {!$_.passed}|Format-Table name,detail -Wrap
    Write-Output ('Checks: '+$result.checks+'; failures: '+$result.failures)
    if($process.ExitCode -ne 0){throw 'Self-tests failed.'}
}

