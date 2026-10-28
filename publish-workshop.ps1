param(
    [string]$ChangeNote = "Beta update",
    [string]$SteamUser = "piyper"
)

$ErrorActionPreference = "Stop"

$sourceRoot = $PSScriptRoot
$modsRoot = Split-Path -Parent $sourceRoot
$liveRoot = Join-Path $modsRoot "ServerIntegrity"
$toolsRoot = Join-Path $modsRoot ".tools\WorkshopPublisher"
$steamCmd = Join-Path $toolsRoot "steamcmd\steamcmd.exe"
$vdfPath = Join-Path $toolsRoot "server-integrity-workshop.vdf"
$aboutPath = Join-Path $liveRoot "About\About.xml"
$sourceAboutPath = Join-Path $sourceRoot "Package\About\About.xml"
$descriptionPath = Join-Path $sourceRoot "Workshop\Description.bbcode.txt"
$previewPath = Join-Path $liveRoot "About\Preview.png"

foreach ($requiredPath in @($steamCmd, $aboutPath, $descriptionPath, $previewPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required publishing file is missing: $requiredPath"
    }
}

[xml]$about = Get-Content -LiteralPath $aboutPath -Raw
$publishedId = [string]$about.ModMetadata.WorkshopHandle
if ([string]::IsNullOrWhiteSpace($publishedId)) { $publishedId = "0" }

$description = (Get-Content -LiteralPath $descriptionPath -Raw)
$description = $description.Replace('"', "'").Replace("`r", " ").Replace("`n", " ")
$description = [regex]::Replace($description, '\s{2,}', ' ').Trim()
$contentFolder = $liveRoot.Replace("\", "\\")
$previewFile = $previewPath.Replace("\", "\\")
$safeChangeNote = $ChangeNote.Replace('"', "'")

function Write-WorkshopVdf([string]$id) {
@"
"workshopitem"
{
    "appid" "544550"
    "publishedfileid" "$id"
    "contentfolder" "$contentFolder"
    "previewfile" "$previewFile"
    "visibility" "3"
    "title" "Server Integrity"
    "description" "$description"
    "changenote" "$safeChangeNote"
}
"@ | Set-Content -LiteralPath $vdfPath -Encoding UTF8
}

function Save-WorkshopHandle([string]$id, [string]$path) {
    [xml]$document = Get-Content -LiteralPath $path -Raw
    if ($document.ModMetadata.WorkshopHandle -eq $null) {
        $handle = $document.CreateElement("WorkshopHandle")
        $handle.InnerText = $id
        [void]$document.ModMetadata.InsertAfter($handle, $document.ModMetadata.Author)
    } else {
        $document.ModMetadata.WorkshopHandle = $id
    }
    $document.Save($path)
}

Write-WorkshopVdf $publishedId
& $steamCmd "+login" $SteamUser "+workshop_build_item" $vdfPath "+quit"
if ($LASTEXITCODE -ne 0) { throw "Server Integrity Workshop upload failed." }

if ($publishedId -eq "0") {
    $match = [regex]::Match((Get-Content -LiteralPath $vdfPath -Raw), '"publishedfileid"\s+"(\d+)"')
    if (-not $match.Success -or $match.Groups[1].Value -eq "0") {
        throw "Steam did not return a Workshop item ID."
    }
    $publishedId = $match.Groups[1].Value
    Save-WorkshopHandle $publishedId $aboutPath
    Save-WorkshopHandle $publishedId $sourceAboutPath
}

Write-Host "Published Server Integrity as unlisted Workshop item $publishedId."
Write-Host "https://steamcommunity.com/sharedfiles/filedetails/?id=$publishedId"
