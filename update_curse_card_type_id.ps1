# PowerShell script to update cursedCardTypeID in all CostNEffectContainer components
# Target SO: Assets/SORefs/CombatRefs/CurseCardTypeID.asset

$targetFolder = "Assets/Prefabs/Cards/3.0 no cost (current)"
$curseCardTypeIDGuid = "07a2aa375c0142b418e46314e9b2ca22"
$curseCardTypeIDFileID = "11400000"
$costNEffectContainerScriptGuid = "a21da06ba55646f29c59d9dbf90834b3"

# Find all prefab files recursively
$prefabFiles = Get-ChildItem -Path $targetFolder -Filter "*.prefab" -Recurse

$updatedCount = 0
$skippedCount = 0
$alreadySetCount = 0

foreach ($file in $prefabFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Check if this prefab contains CostNEffectContainer component
    if ($content -notmatch "guid: $costNEffectContainerScriptGuid") {
        $skippedCount++
        continue
    }
    
    # Check if cursedCardTypeID is already set to the target
    $targetPattern = "cursedCardTypeID: {fileID: $curseCardTypeIDFileID, guid: $curseCardTypeIDGuid, type: 2}"
    if ($content -match [regex]::Escape($targetPattern)) {
        $alreadySetCount++
        continue
    }
    
    # Replace cursedCardTypeID: (empty or any existing value) with the target reference
    # Pattern matches:
    #   cursedCardTypeID: {fileID: 0}
    #   cursedCardTypeID: 
    #   cursedCardTypeID: {fileID: xxxx, guid: xxxx, type: 2}
    
    $pattern = 'cursedCardTypeID:.*(\r?\n)'
    $replacement = "cursedCardTypeID: {fileID: $curseCardTypeIDFileID, guid: $curseCardTypeIDGuid, type: 2}`$1"
    
    $newContent = [regex]::Replace($content, $pattern, $replacement)
    
    if ($newContent -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "Updated: $($file.FullName)" -ForegroundColor Green
        $updatedCount++
    } else {
        Write-Host "No change needed: $($file.FullName)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Updated: $updatedCount" -ForegroundColor Green
Write-Host "Already set correctly: $alreadySetCount" -ForegroundColor Blue
Write-Host "Skipped (no CostNEffectContainer): $skippedCount" -ForegroundColor Yellow
