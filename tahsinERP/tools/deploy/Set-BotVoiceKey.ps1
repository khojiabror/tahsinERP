<#
.SYNOPSIS
    Telegram botda ovozli buyurtma uchun Groq API kalitini (Bot__Voice__GroqApiKey) bot pool'iga yozadi.

.DESCRIPTION
    Bot ovozni Groq Whisper orqali matnga aylantiradi. Kalit bo'lmasa bot "Ovozli buyurtma sozlanmagan
    (nutqni aniqlash kaliti yo'q)" deydi. Kalit chatga/skriptga yozilmaydi: Notepad'da vaqtinchalik faylga
    qo'yiladi, skript o'qiydi va faylni o'chiradi.

    1) https://console.groq.com -> API Keys -> Create API Key (gsk_... bilan boshlanadi)
    2) Notepad: C:\temp\groq.txt ga faqat kalitni yozing, saqlang
    3) Administrator PowerShell:  .\Set-BotVoiceKey.ps1 -SecretsFile C:\temp\groq.txt
    4) ERP: Distributsiya -> Mashinalar -> Yetkazish sozlamalari -> "Botda ovozli buyurtma" yoqilgan bo'lsin (har firmada)

.EXAMPLE
    .\Set-BotVoiceKey.ps1 -SecretsFile C:\temp\groq.txt
    .\Set-BotVoiceKey.ps1 -Check
    .\Set-BotVoiceKey.ps1 -Remove
#>
param(
    [string] $SecretsFile,
    [switch] $Check,
    [switch] $Remove,
    [string] $BotPool = 'tahsinERP.Bot'
)
$ErrorActionPreference = 'Stop'
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
$name = 'Bot__Voice__GroqApiKey'

function Get-PoolEnv {
    Import-Module WebAdministration
    $e = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.applicationHost/applicationPools/add[@name='$BotPool']/environmentVariables/add[@name='$name']"
    if ($e) { return [string]$e.value } else { return $null }
}

if (-not (& $appcmd list apppool /name:$BotPool)) { throw "IIS pool topilmadi: $BotPool" }

if ($Check) {
    $v = Get-PoolEnv
    if ($v) { Write-Host ("[ok] $name bor (" + $v.Substring(0, [Math]::Min(4, $v.Length)) + "..., uzunligi " + $v.Length + ")") }
    else { Write-Host "[!] $name yo'q - ovozli buyurtma o'chiq" -ForegroundColor Yellow }
    return
}

& $appcmd set config -section:system.applicationHost/applicationPools "/-[name='$BotPool'].environmentVariables.[name='$name']" /commit:apphost 2>$null | Out-Null

if ($Remove) {
    & $appcmd recycle apppool /apppool.name:$BotPool | Out-Null
    Write-Host "[ok] $name olib tashlandi, bot qayta ishga tushdi."
    return
}

if (-not $SecretsFile -or -not (Test-Path $SecretsFile)) { throw 'Kalit fayli topilmadi: -SecretsFile C:\temp\groq.txt' }
$key = (Get-Content $SecretsFile -Raw).Trim()
Remove-Item $SecretsFile -Force
if ($key -notmatch '^gsk_[A-Za-z0-9]{20,}$') { throw "Kalit formati noto'g'ri (gsk_... bo'lishi kerak). Fayl o'chirildi - qaytadan yarating." }

# Kalitni Groq'da tekshirish (modellar ro'yxati) - noto'g'ri kalit bilan bot ishlamay qolmasin
try {
    $r = Invoke-WebRequest -Uri 'https://api.groq.com/openai/v1/models' -Headers @{ Authorization = "Bearer $key" } -UseBasicParsing -TimeoutSec 20
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Write-Host '[ok] Groq kalitni qabul qildi.'
} catch {
    throw ("Groq kalitni rad etdi yoki ulanib bo'lmadi: " + $_.Exception.Message)
}

& $appcmd set config -section:system.applicationHost/applicationPools "/+[name='$BotPool'].environmentVariables.[name='$name',value='$key']" /commit:apphost | Out-Null
if ($LASTEXITCODE -ne 0) { throw "environmentVariables yozilmadi: $BotPool / $name" }
& $appcmd recycle apppool /apppool.name:$BotPool | Out-Null
Write-Host "[ok] $name yozildi, bot qayta ishga tushdi."
Write-Host 'Endi ERP: Distributsiya -> Mashinalar -> Yetkazish sozlamalari -> "Botda ovozli buyurtma" yoqing va agent bilan ovozli xabar yuboring.'
