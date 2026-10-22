param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$localDb=Get-ChildItem 'C:/Program Files/Microsoft SQL Server' -Filter SqlLocalDB.exe -Recurse -ErrorAction SilentlyContinue|Sort-Object FullName -Descending|Select-Object -First 1 -ExpandProperty FullName
if(!$localDb){throw 'Install SQL Server Express LocalDB first.'}
& $localDb start MSSQLLocalDB
if($LASTEXITCODE -ne 0){
 throw 'SQL Server LocalDB could not start. Run this launcher as the Windows user who owns MSSQLLocalDB and inspect the LocalDB instance error.log. Existing data has not been reset.'
}
$probe=New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Connect Timeout=30'
try {$probe.Open()}
catch {throw "LocalDB started but a SQL connection could not be established: $($_.Exception.Message)"}
finally {$probe.Dispose()}
foreach($project in @("ResourceAllocationService","ResourceAllocationAdmin")){if(!(Test-Path "$root/$project/Web.config")){Copy-Item "$root/$project/Web.config.example" "$root/$project/Web.config"}}
& "$PSScriptRoot/Initialize-System.ps1"
if(!$SkipBuild){
 $msbuild=Get-ChildItem 'C:/Program Files/Microsoft Visual Studio' -Filter MSBuild.exe -Recurse -ErrorAction SilentlyContinue|Where-Object FullName -NotMatch '\\amd64\\'|Select-Object -First 1 -ExpandProperty FullName
 if(!$msbuild){throw 'Install Visual Studio with ASP.NET and .NET Framework 4.7.2 build tools.'}
 & $msbuild "$root/ResourceAllocationService/ResourceAllocationService.sln" /t:Restore /p:RestorePackagesConfig=true /v:minimal /nologo
 if($LASTEXITCODE -ne 0){throw 'NuGet package restore failed. Check internet access or restore packages through Visual Studio.'}
 & $msbuild "$root/ResourceAllocationService/ResourceAllocationService.sln" /t:Build /p:Configuration=Debug /v:minimal /nologo
 if($LASTEXITCODE -ne 0){throw 'Build failed.'}
}
$iis='C:/Program Files (x86)/IIS Express/iisexpress.exe'
$logDirectory=Join-Path $root '.local/logs'
New-Item -ItemType Directory -Path $logDirectory -Force|Out-Null
foreach($entry in @(@{Project='ResourceAllocationService';Port=9002},@{Project='ResourceAllocationAdmin';Port=9003})){
 $listening=Get-NetTCPConnection -LocalPort $entry.Port -State Listen -ErrorAction SilentlyContinue
 if(!$listening){
  $projectPath=Join-Path $root $entry.Project
  $process=Start-Process -FilePath $iis -ArgumentList @("/path:`"$projectPath`"","/port:$($entry.Port)") -WindowStyle Hidden -PassThru -RedirectStandardOutput "$logDirectory/iis-$($entry.Port).log" -RedirectStandardError "$logDirectory/iis-$($entry.Port)-error.log"
  Write-Output "Started $($entry.Project), PID $($process.Id)"
 }
}
$failures=@()
foreach($port in @(9002,9003)){
 try {
  $response=Invoke-WebRequest "http://localhost:$port/Account/Login" -UseBasicParsing -TimeoutSec 60 -SkipHttpErrorCheck
  if($response.StatusCode -ne 200){
   if($response.Content -match '0x800711[Cc]7|Application Control policy'){
    throw "Windows Application Control blocked a portal assembly. See the README troubleshooting section; the PC policy administrator must approve the application."
   }
   throw "HTTP $($response.StatusCode). Inspect the portal response and .local/logs/iis-$port-error.log."
  }
  Write-Output "PASS portal health: http://localhost:$port/Account/Login"
 } catch {$failures+="Port ${port}: $($_.Exception.Message)"}
}
if($failures.Count){throw ($failures -join [Environment]::NewLine)}
Write-Output 'Ready. Requester portal: http://localhost:9002 | Staff portal: http://localhost:9003'

