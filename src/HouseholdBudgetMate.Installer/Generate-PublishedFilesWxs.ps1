param(
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

# Default publish location for the web app when parameter is not explicitly provided.
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $installerDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $PublishDir = Join-Path $installerDir "..\HouseholdBudgetMate.Web\bin\Release\net10.0\win-x64\publish"
}

# Normalize path so values like ...\publish\ or ...\publish\. are handled consistently.
$normalizedPublishDir = [System.IO.Path]::GetFullPath($PublishDir)

if (-not (Test-Path -LiteralPath $normalizedPublishDir)) {
    throw "Publish directory does not exist: $PublishDir"
}

$publishRoot = (Resolve-Path -LiteralPath $normalizedPublishDir).Path.TrimEnd('\', '/')
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

function New-SafeId {
    param(
        [string]$Prefix,
        [string]$Value
    )

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hashBytes = $sha1.ComputeHash($bytes)
        $hash = ([System.BitConverter]::ToString($hashBytes)).Replace("-", "")
        return "${Prefix}${hash}".Substring(0, [Math]::Min(64, "${Prefix}${hash}".Length))
    }
    finally {
        $sha1.Dispose()
    }
}

function Escape-XmlAttribute {
    param([string]$Value)

    return $Value.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function Get-PublishRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishRoot,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $targetFull = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not $targetFull.StartsWith($PublishRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Target file is outside publish directory: $TargetPath"
    }

    return (Normalize-RelativePath -PathValue $targetFull.Substring($PublishRoot.Length))
}

function Normalize-RelativePath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    return $PathValue.Replace('/', '\\').Trim('\\')
}

function Get-DirectoryPart {
    param([string]$RelativePath)

    $normalized = Normalize-RelativePath -PathValue $RelativePath
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ""
    }

    $lastSeparatorIndex = $normalized.LastIndexOf('\')
    if ($lastSeparatorIndex -lt 0) {
        return ""
    }

    return $normalized.Substring(0, $lastSeparatorIndex)
}

function Get-ParentDirectory {
    param([string]$DirectoryPath)

    $normalized = Normalize-RelativePath -PathValue $DirectoryPath
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ""
    }

    $lastSeparatorIndex = $normalized.LastIndexOf('\')
    if ($lastSeparatorIndex -lt 0) {
        return ""
    }

    return $normalized.Substring(0, $lastSeparatorIndex)
}

$files = Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Sort-Object FullName
if ($files.Count -eq 0) {
    throw "Publish directory is empty: $publishRoot"
}

$directorySet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
[void]$directorySet.Add("")

foreach ($file in $files) {
    $relativePath = Get-PublishRelativePath -PublishRoot $publishRoot -TargetPath $file.FullName
    $relativeDirectory = Get-DirectoryPart -RelativePath $relativePath

    if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
        continue
    }

    $relativeDirectory = Normalize-RelativePath -PathValue $relativeDirectory
    $segments = $relativeDirectory.Split([char]'\', [System.StringSplitOptions]::RemoveEmptyEntries)
    $current = ""

    foreach ($segment in $segments) {
        $current = if ([string]::IsNullOrEmpty($current)) { $segment } else { "$current\$segment" }
        [void]$directorySet.Add($current)
    }
}

$sortedDirectories = $directorySet | Sort-Object @{ Expression = { if ([string]::IsNullOrEmpty($_)) { 0 } else { $_.Split([char]'\', [System.StringSplitOptions]::RemoveEmptyEntries).Count } } }, @{ Expression = { $_ } }
$directoryIds = @{}
$directoryIds[""] = "INSTALLFOLDER"

foreach ($directory in $sortedDirectories) {
    if ($directory -eq "") {
        continue
    }

    $directoryIds[$directory] = New-SafeId -Prefix "Dir" -Value $directory
}

$childrenByParent = @{}
foreach ($directory in $sortedDirectories) {
    if ($directory -eq "") {
        continue
    }

    $parent = Get-ParentDirectory -DirectoryPath $directory

    if (-not $childrenByParent.ContainsKey($parent)) {
        $childrenByParent[$parent] = New-Object System.Collections.Generic.List[string]
    }

    $childrenByParent[$parent].Add($directory)
}

function Append-DirectoryNodes {
    param(
        [System.Text.StringBuilder]$Builder,
        [hashtable]$ChildrenByParent,
        [hashtable]$DirectoryIds,
        [string]$ParentPath,
        [string]$Indent
    )

    if (-not $ChildrenByParent.ContainsKey($ParentPath)) {
        return
    }

    foreach ($childPath in ($ChildrenByParent[$ParentPath] | Sort-Object)) {
        $directoryId = $DirectoryIds[$childPath]
        $name = Split-Path -Path $childPath -Leaf
        $nameEscaped = Escape-XmlAttribute -Value $name
        [void]$Builder.AppendLine("$Indent<Directory Id=`"$directoryId`" Name=`"$nameEscaped`">")
        Append-DirectoryNodes -Builder $Builder -ChildrenByParent $ChildrenByParent -DirectoryIds $DirectoryIds -ParentPath $childPath -Indent "$Indent  "
        [void]$Builder.AppendLine("$Indent</Directory>")
    }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
Append-DirectoryNodes -Builder $sb -ChildrenByParent $childrenByParent -DirectoryIds $directoryIds -ParentPath "" -Indent '      '
[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="PublishedFiles" Directory="INSTALLFOLDER">')

foreach ($file in $files) {
    $relativePath = Normalize-RelativePath -PathValue (Get-PublishRelativePath -PublishRoot $publishRoot -TargetPath $file.FullName)
    $relativeDirectory = Get-DirectoryPart -RelativePath $relativePath
    if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
        $relativeDirectory = ""
    }
    else {
        $relativeDirectory = Normalize-RelativePath -PathValue $relativeDirectory
    }

    if (-not $directoryIds.ContainsKey($relativeDirectory)) {
        throw "Missing target directory id for relative directory: $relativeDirectory (file: $relativePath)"
    }

    $targetDirectoryId = $directoryIds[$relativeDirectory]
    $componentId = New-SafeId -Prefix "Cmp" -Value $relativePath
    $fileId = New-SafeId -Prefix "Fil" -Value $relativePath
    $fullPath = Escape-XmlAttribute -Value $file.FullName

    [void]$sb.AppendLine("      <Component Id=`"$componentId`" Guid=`"*`" Bitness=`"always64`" Directory=`"$targetDirectoryId`">")
    [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"$fullPath`" KeyPath=`"yes`" />")
    [void]$sb.AppendLine('      </Component>')
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

Set-Content -LiteralPath $OutputPath -Value $sb.ToString() -Encoding UTF8
Write-Host "Generated: $OutputPath" -ForegroundColor DarkGray

