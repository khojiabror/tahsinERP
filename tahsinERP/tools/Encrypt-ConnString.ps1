<#
    Encrypt-ConnString.ps1
    -----------------------
    DB connection string'ni shifrlab, DbConfig.cs ga qo'yish uchun Base64 blob chiqaradi.

    Ishlatish:
        .\Encrypt-ConnString.ps1 -Plain "data source=192.168.100.200;initial catalog=DBTHSN-IPACK;user id=sa;password=Parol;trustservercertificate=True;MultipleActiveResultSets=True"

    Chiqqan Base64 satrni DbConfig.cs dagi tegishli qatorga (DEBUG=lokal, ELSE=server) qo'ying.

    DIQQAT: bu yerdagi KeyPhrase/IvPhrase DbConfig.cs dagi bilan AYNAN bir xil bo'lishi shart.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Plain,

    # Shifrni tekshirish uchun: chiqqan blob'ni qayta ochib ko'rsatadi
    [switch]$Verify
)

# --- DbConfig.cs bilan bir xil bo'lishi SHART ---
$KeyPhrase = "tahsinERP.DbConfig.Key.2026#Sch"
$IvPhrase  = "tahsinERP.DbConfig.IV.2026#Sch"
# ------------------------------------------------

function New-Aes {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $key = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($KeyPhrase))
    $iv  = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($IvPhrase))

    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.Mode    = [System.Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key     = $key
    $aes.IV      = $iv
    return $aes
}

$aes = New-Aes
$encryptor = $aes.CreateEncryptor()
$plainBytes = [System.Text.Encoding]::UTF8.GetBytes($Plain)
$cipher = $encryptor.TransformFinalBlock($plainBytes, 0, $plainBytes.Length)
$blob = [Convert]::ToBase64String($cipher)

Write-Host ""
Write-Host "=== Shifrlangan blob (DbConfig.cs ga qo'ying) ===" -ForegroundColor Green
Write-Host $blob
Write-Host ""

if ($Verify) {
    $aes2 = New-Aes
    $decryptor = $aes2.CreateDecryptor()
    $cipher2 = [Convert]::FromBase64String($blob)
    $back = $decryptor.TransformFinalBlock($cipher2, 0, $cipher2.Length)
    $text = [System.Text.Encoding]::UTF8.GetString($back)
    Write-Host "=== Tekshiruv (ochilgani) ===" -ForegroundColor Yellow
    Write-Host $text
    Write-Host ""
}
