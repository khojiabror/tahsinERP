<#
.SYNOPSIS
    Firma ERP instance'ini serverdan olib tashlaydi: IIS sayt/pool erp-<slug>, Updater holatidagi yozuv, (ixtiyoriy) papka.
    -Demo: umumiy demo tenant qoldiqlari ham - DB_DEMO_TEMPLATE.bak va agentdagi tungi demo tiklash o'chiriladi.

.DESCRIPTION
    Baza va tenants.json yozuvini PANEL o'chiradi (Tashkilot -> Arxiv -> O'chirish, agent bajaradi). Bu skript faqat panel
    tegmaydigan qismlarni tozalaydi, shuning uchun avval panelda o'chiring: tenants.json'da slug hali yoqilgan bo'lsa skript
    to'xtaydi (-Force bilan davom etadi). Administrator PowerShell'da (Windows PowerShell 5.1). Faqat ASCII.

.EXAMPLE
    .\Remove-ErpInstance.ps1 -Slug demo -Demo -RemoveFiles
#>
param(
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9-]{1,63}$')] [string] $Slug,
    [switch] $Demo,
    [switch] $RemoveFiles,
    [switch] $Force,
    [string] $PoolPrefix = 'erp-',
    [string] $InstancesRoot = 'C:\inetpub\erp',
    [string] $DataDir = 'C:\ProgramData\tahsinERP',
    [string] $UpdaterService = 'tahsinERP.Updater',
    [string] $AgentService = 'tahsinERP.ProvisioningAgent',
    [string] $AgentDir = 'C:\tahsinERP\Agent'
)
$ErrorActionPreference = 'Stop'
function Step($t) { Write-Host "== $t" -ForegroundColor Cyan }
function Ok($t) { Write-Host "   OK  $t" -ForegroundColor Green }
function Skip($t) { Write-Host "   --  $t" -ForegroundColor DarkGray }

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal $id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Administrator PowerShell kerak.'
}
$name = $PoolPrefix + $Slug

Step '1/5 tenants.json tekshiruvi'
$tenantsFile = $env:TAHSINERP_TENANTS_FILE
if (-not $tenantsFile) { $tenantsFile = Join-Path $DataDir 'tenants.json' }
if (Test-Path $tenantsFile) {
    $reg = Get-Content $tenantsFile -Raw | ConvertFrom-Json
    $t = @($reg.tenants) | Where-Object { $_.slug -eq $Slug }
    if ($t -and $t.enabled -ne $false -and -not $Force) {
        throw "'$Slug' tenants.json'da hali yoqilgan. Avval panelda Arxiv -> O'chirish qiling (yoki -Force)."
    }
    if ($t) { Skip "tenants.json'da bor (enabled=$($t.enabled)) - panel O'chirish job'i olib tashlaydi" } else { Ok "tenants.json'da yo'q" }
} else { Skip "tenants.json topilmadi: $tenantsFile" }

Step "2/5 Updater holati ($UpdaterService)"
$stateFile = Join-Path $DataDir 'Updater\state.json'
if (Test-Path $stateFile) {
    $svc = Get-Service $UpdaterService -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') { Stop-Service $UpdaterService -Force; Start-Sleep -Seconds 2 }
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        if ($state.instances -and ($state.instances.PSObject.Properties.Name -contains $Slug)) {
            Copy-Item $stateFile ($stateFile + '.' + (Get-Date -Format 'yyyyMMddHHmmss') + '.bak')
            $state.instances.PSObject.Properties.Remove($Slug)
            $json = $state | ConvertTo-Json -Depth 20
            [IO.File]::WriteAllText($stateFile, $json, (New-Object Text.UTF8Encoding($false)))
            Ok "instances.$Slug olib tashlandi (zaxira: state.json.*.bak)"
        } else { Skip "state.json'da '$Slug' instance'i yo'q" }
    }
    finally {
        if ($svc) { Start-Service $UpdaterService; Ok "$UpdaterService qayta ishga tushdi" }
    }
} else { Skip "state.json topilmadi: $stateFile" }

Step "3/5 IIS sayt va pool: $name"
Import-Module WebAdministration
if (Get-Website -Name $name) { Stop-Website -Name $name -ErrorAction SilentlyContinue; Remove-Website -Name $name; Ok "sayt o'chirildi: $name" } else { Skip "sayt yo'q: $name" }
if (Test-Path "IIS:\AppPools\$name") { Remove-WebAppPool -Name $name; Ok "pool o'chirildi: $name" } else { Skip "pool yo'q: $name" }

Step '4/5 instance papkasi'
$path = Join-Path $InstancesRoot $Slug
if (Test-Path $path) {
    if ($RemoveFiles) { Remove-Item $path -Recurse -Force; Ok "o'chirildi: $path" } else { Skip "qoldirildi: $path (-RemoveFiles bilan o'chadi)" }
} else { Skip "papka yo'q: $path" }

Step '5/5 demo qoldiqlari'
if ($Demo) {
    $tpl = Join-Path $DataDir 'Backups\DB_DEMO_TEMPLATE.bak'
    if (Test-Path $tpl) { Remove-Item $tpl -Force; Ok "o'chirildi: $tpl" } else { Skip "shablon yo'q: $tpl" }
    $settings = Join-Path $AgentDir 'appsettings.json'
    if (Test-Path $settings) {
        $cfg = Get-Content $settings -Raw | ConvertFrom-Json
        if ($cfg.Demo -and ($cfg.Demo.Enabled -ne $false -or $cfg.Demo.Slug)) {
            Copy-Item $settings ($settings + '.bak')
            $cfg.Demo.Enabled = $false
            $cfg.Demo.Slug = ''
            [IO.File]::WriteAllText($settings, ($cfg | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding($false)))
            Ok 'agent: Demo.Enabled=false, Demo.Slug="" (tungi demo tiklash yo''q)'
            if (Get-Service $AgentService -ErrorAction SilentlyContinue) { Restart-Service $AgentService; Ok "$AgentService qayta ishga tushdi" }
        } else { Skip 'agentda demo allaqachon o''chiq' }
    } else { Skip "agent sozlamasi topilmadi: $settings" }
} else { Skip '-Demo berilmagan' }

$db = 'DB_' + $Slug
$exists = & sqlcmd -S . -E -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$db'" 2>$null
if ("$exists".Trim() -eq '1') { Write-Host "   !!  $db bazasi hali bor - panelda Arxiv -> O'chirish qiling" -ForegroundColor Yellow }
Write-Host 'TAYYOR' -ForegroundColor Green
