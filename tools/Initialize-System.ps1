param([ValidateSet('PGHResourceAllocation')][string]$DatabaseName='PGHResourceAllocation')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$instance='(localdb)\MSSQLLocalDB'
function Execute-Sql([string]$database,[string]$sql,[hashtable]$parameters=@{}) {
 $connection=New-Object System.Data.SqlClient.SqlConnection "Server=$instance;Database=$database;Integrated Security=True;Connect Timeout=30"
 $connection.Open()
 try {
  foreach($batch in [regex]::Split($sql,'(?m)^GO\s*\r?$')) {
   if([string]::IsNullOrWhiteSpace($batch)){continue}
   $command=$connection.CreateCommand();$command.CommandText=$batch;$command.CommandTimeout=60
   foreach($key in $parameters.Keys){$null=$command.Parameters.AddWithValue($key,$parameters[$key])}
   $null=$command.ExecuteNonQuery();$command.Dispose()
  }
 } finally {$connection.Dispose()}
}
Execute-Sql master "IF DB_ID(N'$DatabaseName') IS NULL CREATE DATABASE [$DatabaseName]"
$connection=New-Object System.Data.SqlClient.SqlConnection "Server=$instance;Database=$DatabaseName;Integrated Security=True"
$connection.Open();$command=$connection.CreateCommand();$command.CommandText="SELECT COUNT(*) FROM sys.tables WHERE name='Request'";$exists=$command.ExecuteScalar();$connection.Dispose()
if(!$exists){Execute-Sql $DatabaseName (Get-Content "$root/database/001-base-schema.sql" -Raw)}
Execute-Sql $DatabaseName (Get-Content "$root/database/002-workflow.sql" -Raw)
Execute-Sql $DatabaseName (Get-Content "$root/database/003-hgras-network.sql" -Raw)
Execute-Sql $DatabaseName "IF NOT EXISTS(SELECT 1 FROM dbo.RequestStage) INSERT dbo.RequestStage(Name,StageLevel) VALUES ('INITIAL',1),('APPROVED',2),('RELEASED',3),('DISPATCHED',4),('COMPLETED',5),('REJECTED',6);"
foreach($role in @('ADMIN','APPROVER','RELEASER','DISPATCHER')) {
 Execute-Sql $DatabaseName "IF NOT EXISTS(SELECT 1 FROM dbo.AspNetRoles WHERE Name=@role) INSERT dbo.AspNetRoles(Id,Name) VALUES (@role,@role)" @{'@role'=$role}
}
if($DatabaseName -eq 'PGHResourceAllocation') {
 foreach($project in @('ResourceAllocationService','ResourceAllocationAdmin')) {
  $file="$root/$project/Web.config";[xml]$config=Get-Content $file
  foreach($item in $config.configuration.connectionStrings.add) {
   if($item.name -eq 'DefaultConnection') {$item.connectionString="Data Source=$instance;Initial Catalog=$DatabaseName;Integrated Security=True;MultipleActiveResultSets=True"}
   elseif($item.name -eq 'ResourceDistrubitionDBEntities') {
    $item.connectionString=[regex]::Replace($item.connectionString,'(?i)data source=[^;]+',"data source=$instance")
    $item.connectionString=[regex]::Replace($item.connectionString,'(?i)initial catalog=[^;]+',"initial catalog=$DatabaseName")
   }
  }
  $setting=$config.SelectSingleNode('/configuration/appSettings/add[@key="DemoMode"]')
  if(!$setting){$setting=$config.CreateElement('add');$setting.SetAttribute('key','DemoMode');$config.SelectSingleNode('/configuration/appSettings').AppendChild($setting)|Out-Null};$setting.SetAttribute('value','false')
  $machineKey=$config.SelectSingleNode('/configuration/system.web/machineKey')
  if(!$machineKey){
   $machineKey=$config.CreateElement('machineKey')
   $rng=[System.Security.Cryptography.RandomNumberGenerator]::Create()
   try {
    foreach($key in @(@('validationKey',64),@('decryptionKey',32))){
     $bytes=New-Object byte[] $key[1];$rng.GetBytes($bytes)
     $machineKey.SetAttribute($key[0],([BitConverter]::ToString($bytes).Replace('-','')))
    }
   } finally {$rng.Dispose()}
   $machineKey.SetAttribute('validation','HMACSHA256');$machineKey.SetAttribute('decryption','AES')
   $null=$config.SelectSingleNode('/configuration/system.web').AppendChild($machineKey)
  }
  $config.Save($file)
 }
}
Write-Output "Ready: $instance / $DatabaseName. No sample accounts, organisations or stock were created."
