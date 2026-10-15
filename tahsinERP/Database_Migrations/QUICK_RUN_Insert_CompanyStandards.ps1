# =============================================
# Korxona Standartlarini Import Qilish - TEZKOR YECHIM
# =============================================
# 
# Bu script bir marta ishga tushiriladi va barcha .docx fayllarni import qiladi
# 
# ISHGA TUSHIRISH:
# 1. PowerShell ni Administrator sifatida oching
# 2. Bu faylni o'ng tugma bilan bosib "Run with PowerShell" ni tanlang
#    YOKI
# 3. PowerShell'da: .\QUICK_RUN_Insert_CompanyStandards.ps1
#
# =============================================

# =============================================
# SOZLAMALAR - BU YERDAGI MA'LUMOTLARNI O'ZGARTIRING
# =============================================

$ServerName = "192.168.100.200"  # SQL Server nomi yoki IP
$DatabaseName = "DBTHSN-IPACK"  # Database nomi
$Username = "sa"  # SQL Server foydalanuvchi nomi
$Password = "Sch@2024"  # SQL Server paroli
$FolderPath = "\\192.168.100.200\iPack_shared\Korxona Standarti"  # .docx fayllar papkasi
$CreatedByUserID = 1  # Foydalanuvchi ID (database'dagi USERS jadvalidagi ID)

# =============================================
# ASOSIY KOD - O'ZGARTIRMAYDI
# =============================================

# Execution Policy ni tekshirish va o'zgartirish
$currentPolicy = Get-ExecutionPolicy
if ($currentPolicy -eq "Restricted") {
    Write-Host "Execution Policy o'zgartirilmoqda..." -ForegroundColor Yellow
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force | Out-Null
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Korxona Standartlari Import" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# SQL Server bilan ulanish string
$ConnectionString = "Server=$ServerName;Database=$DatabaseName;User Id=$Username;Password=$Password;"

# Papka mavjudligini tekshirish
if (-not (Test-Path $FolderPath)) {
    Write-Host "Xatolik: Papka topilmadi: $FolderPath" -ForegroundColor Red
    Write-Host "Iltimos, papka yo'lini to'g'ri kiriting." -ForegroundColor Yellow
    Read-Host "Chiqish uchun Enter ni bosing"
    exit 1
}

# .docx fayllarni topish
$DocxFiles = Get-ChildItem -Path $FolderPath -Filter "*.docx" -File

if ($DocxFiles.Count -eq 0) {
    Write-Host "Xatolik: $FolderPath papkasida .docx fayllar topilmadi." -ForegroundColor Red
    Read-Host "Chiqish uchun Enter ni bosing"
    exit 1
}

Write-Host "Topilgan fayllar: $($DocxFiles.Count)" -ForegroundColor Yellow
Write-Host ""

# Database connection
try {
    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $Connection.Open()
    
    Write-Host "Database'ga ulandi: $DatabaseName" -ForegroundColor Green
    Write-Host ""
    
    $Command = $Connection.CreateCommand()
    
    # DocTypeID ni olish/yaratish
    $Command.CommandText = "SELECT ID FROM DOC_DOCUMENT_TYPES WHERE Name = 'CompanyStandard'"
    $DocTypeID = $Command.ExecuteScalar()
    if (-not $DocTypeID) {
        # DOC_DOCUMENT_TYPES jadvalida: ID, Code, Name, IsActive, SortOrder (Description yo'q)
        $Command.CommandText = @"
            INSERT INTO DOC_DOCUMENT_TYPES (Code, Name, IsActive, SortOrder)
            VALUES ('COMPANY_STANDARD', 'CompanyStandard', 1, 0);
            SELECT SCOPE_IDENTITY();
"@
        $DocTypeID = [int]$Command.ExecuteScalar()
        Write-Host "DocType yaratildi: $DocTypeID" -ForegroundColor Green
    }
    else {
        Write-Host "DocTypeID topildi: $DocTypeID" -ForegroundColor Cyan
    }
    
    # StatusID ni olish
    $Command.CommandText = "SELECT ID FROM DOC_DOCUMENT_STATUS_REF WHERE Code = 'Active' OR Code = 'ACTIVE' OR Name = 'Active'"
    $StatusID = $Command.ExecuteScalar()
    if (-not $StatusID) {
        $StatusID = 1
        Write-Host "StatusID (default): $StatusID" -ForegroundColor Yellow
    }
    else {
        Write-Host "StatusID topildi: $StatusID" -ForegroundColor Cyan
    }
    
    Write-Host ""
    
    # Har bir fayl uchun
    $SuccessCount = 0
    $ErrorCount = 0
    
    foreach ($File in $DocxFiles) {
        $FilePath = $File.FullName
        $FileName = $File.Name
        $StandardCode = [System.IO.Path]::GetFileNameWithoutExtension($FileName)
        
        Write-Host "Ishlanyapti: $FileName..." -ForegroundColor Yellow
        
        try {
            # Fayl content ni o'qish
            $FileContent = [System.IO.File]::ReadAllBytes($FilePath)
            $FileSize = $FileContent.Length
            
            Write-Host "  Fayl hajmi: $FileSize bytes" -ForegroundColor Gray
            
            # Transaction boshlandi
            $Transaction = $Connection.BeginTransaction()
            $Command.Transaction = $Transaction
            
            try {
                # DOC_DOCUMENTS insert
                $DocTitle = "$StandardCode - Korxona standarti"
                
                $Command.CommandText = @"
                    INSERT INTO DOC_DOCUMENTS (
                        DocTypeID, 
                        StatusID, 
                        Code, 
                        Title, 
                        CreatedDT, 
                        IntegrationUID,
                        ExternalSystemCode,
                        IntegrationStatusCode
                    )
                    VALUES (
                        @DocTypeID, 
                        @StatusID, 
                        @Code, 
                        @Title, 
                        GETDATE(), 
                        NEWID(),
                        'INTERNAL',
                        'SYNCED'
                    );
                    SELECT SCOPE_IDENTITY();
"@
                $Command.Parameters.Clear()
                $Command.Parameters.AddWithValue("@DocTypeID", $DocTypeID) | Out-Null
                $Command.Parameters.AddWithValue("@StatusID", $StatusID) | Out-Null
                $Command.Parameters.AddWithValue("@Code", $StandardCode) | Out-Null
                $Command.Parameters.AddWithValue("@Title", $DocTitle) | Out-Null
                $DocumentID = [long]$Command.ExecuteScalar()
                
                # STD_COMPANY_STANDARDS insert
                $Command.CommandText = @"
                    INSERT INTO STD_COMPANY_STANDARDS (
                        DocumentID, 
                        StandardCode, 
                        Area, 
                        Notes, 
                        VersionNo
                    )
                    VALUES (
                        @DocumentID, 
                        @StandardCode, 
                        @Area, 
                        @Notes, 
                        1.00
                    );
"@
                $Command.Parameters.Clear()
                $Command.Parameters.AddWithValue("@DocumentID", $DocumentID) | Out-Null
                $Command.Parameters.AddWithValue("@StandardCode", $StandardCode) | Out-Null
                $Command.Parameters.AddWithValue("@Area", [DBNull]::Value) | Out-Null
                $Command.Parameters.AddWithValue("@Notes", [DBNull]::Value) | Out-Null
                $Command.ExecuteNonQuery() | Out-Null
                
                # DOC_FILES insert
                $Command.CommandText = @"
                    INSERT INTO DOC_FILES (
                        DocumentID, 
                        VersionNo, 
                        FileName, 
                        FileExtension, 
                        MimeType, 
                        FileSizeBytes, 
                        FileContent, 
                        IsMain, 
                        CreatedByUserID,
                        CreatedDT
                    )
                    VALUES (
                        @DocumentID, 
                        1, 
                        @FileName, 
                        '.docx', 
                        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
                        @FileSize, 
                        @FileContent, 
                        1, 
                        @CreatedByUserID,
                        GETDATE()
                    );
"@
                $Command.Parameters.Clear()
                $Command.Parameters.AddWithValue("@DocumentID", $DocumentID) | Out-Null
                $Command.Parameters.AddWithValue("@FileName", $FileName) | Out-Null
                $Command.Parameters.AddWithValue("@FileSize", $FileSize) | Out-Null
                $Command.Parameters.AddWithValue("@CreatedByUserID", $CreatedByUserID) | Out-Null
                $FileContentParam = New-Object System.Data.SqlClient.SqlParameter("@FileContent", [System.Data.SqlDbType]::VarBinary, -1)
                $FileContentParam.Value = $FileContent
                $Command.Parameters.Add($FileContentParam) | Out-Null
                $Command.ExecuteNonQuery() | Out-Null
                
                # Commit
                $Transaction.Commit()
                Write-Host "  [OK] Muvaffaqiyatli qo'shildi: $StandardCode (DocumentID: $DocumentID)" -ForegroundColor Green
                $SuccessCount++
            }
            catch {
                $Transaction.Rollback()
                Write-Host "  [ERROR] Xatolik: $($_.Exception.Message)" -ForegroundColor Red
                $ErrorCount++
            }
        }
        catch {
            Write-Host "  [ERROR] Xatolik (fayl o'qish): $($_.Exception.Message)" -ForegroundColor Red
            $ErrorCount++
        }
        
        Write-Host ""
    }
    
    # Natijalar
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Natijalar:" -ForegroundColor Cyan
    Write-Host "  Muvaffaqiyatli: $SuccessCount" -ForegroundColor Green
    if ($ErrorCount -gt 0) {
        Write-Host "  Xatoliklar: $ErrorCount" -ForegroundColor Red
    }
    else {
        Write-Host "  Xatoliklar: $ErrorCount" -ForegroundColor Green
    }
    Write-Host "======================================" -ForegroundColor Cyan
    
    $Connection.Close()
}
catch {
    Write-Host "Database xatolik: $($_.Exception.Message)" -ForegroundColor Red
    if ($Connection.State -eq [System.Data.ConnectionState]::Open) {
        $Connection.Close()
    }
    Read-Host "Chiqish uchun Enter ni bosing"
    exit 1
}

Write-Host ""
Write-Host "Tugallandi!" -ForegroundColor Green
Read-Host "Chiqish uchun Enter ni bosing"
