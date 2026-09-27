<#
.SYNOPSIS
    Supprime les dossiers de worktrees Claude Code laisses sur le disque apres
    qu'un `git worktree remove` ait desenregistre le worktree cote git sans
    reussir a effacer le dossier physique (verrou de fichier).

.DESCRIPTION
    `git worktree remove` peut reussir a desenregistrer un worktree (il
    disparait de `git worktree list`) tout en echouant a supprimer le dossier
    lui-meme si un processus y maintient un handle ouvert - typiquement la
    session Claude Code qui tourne DANS ce worktree (son shell y reste
    ancre), un IDE, ou un build en cours.

    Ce script :
      1. Lance `git worktree prune` pour nettoyer les metadonnees obsoletes.
      2. Repere, sous .claude/worktrees/, les dossiers qui ne sont PLUS des
         worktrees enregistres aupres de git (donc surs a supprimer).
      3. Tente de les supprimer, avec plusieurs essais espaces si le dossier
         est encore verrouille - le verrou se leve typiquement quelques
         secondes apres la fin du processus qui le tenait (ex: la session
         Claude Code qui y tournait vient de se terminer).

    A executer depuis un terminal/une session qui n'a PAS son repertoire de
    travail a l'interieur d'un des dossiers a supprimer (sinon ce terminal
    lui-meme maintient le verrou).

.PARAMETER RepoRoot
    Racine du depot git principal (celle qui contient .claude/worktrees).
    Par defaut : le depot courant.

.PARAMETER MaxRetries
    Nombre de tentatives avant d'abandonner un dossier verrouille.

.PARAMETER RetryDelaySeconds
    Delai en secondes entre deux tentatives.

.EXAMPLE
    ./Remove-StaleWorktrees.ps1

.EXAMPLE
    ./Remove-StaleWorktrees.ps1 -RepoRoot "E:\AnthoDingo\GlpiNg" -WhatIf

.EXAMPLE
    ./Remove-StaleWorktrees.ps1 -MaxRetries 10 -RetryDelaySeconds 5
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$RepoRoot,
    [int]$MaxRetries = 5,
    [int]$RetryDelaySeconds = 3
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) {
    $RepoRoot = git rev-parse --show-toplevel 2>$null
    if (-not $RepoRoot) {
        $RepoRoot = (Get-Location).Path
    }
}

if (-not (Test-Path $RepoRoot)) {
    throw "Depot introuvable : $RepoRoot"
}

Push-Location $RepoRoot
try {
    Write-Host "Depot : $RepoRoot"

    # 1. Nettoie les metadonnees des worktrees dont le dossier a deja disparu
    git worktree prune -v

    $worktreesDir = Join-Path $RepoRoot ".claude/worktrees"
    if (-not (Test-Path $worktreesDir)) {
        Write-Host "Aucun dossier .claude/worktrees trouve, rien a faire."
        return
    }

    # 2. Dossiers physiquement presents sous .claude/worktrees
    $onDisk = Get-ChildItem -Path $worktreesDir -Directory | Select-Object -ExpandProperty FullName

    if (-not $onDisk) {
        Write-Host "Aucun dossier sous .claude/worktrees."
        return
    }

    # 3. Worktrees encore enregistres aupres de git
    $registered = git worktree list --porcelain |
        Where-Object { $_ -like 'worktree *' } |
        ForEach-Object { ($_ -replace '^worktree ', '').Replace('/', '\') }

    $stale = $onDisk | Where-Object {
        $normalized = $_.TrimEnd('\').Replace('/', '\')
        -not ($registered | Where-Object { $_.TrimEnd('\') -ieq $normalized })
    }

    if (-not $stale) {
        Write-Host "Aucun dossier de worktree orphelin a supprimer."
        return
    }

    foreach ($dir in $stale) {
        Write-Host ""
        Write-Host "Orphelin detecte : $dir"

        if ($PSCmdlet.ShouldProcess($dir, "Supprimer le dossier")) {
            $removed = $false
            for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
                try {
                    Remove-Item -Recurse -Force -Confirm:$false -Path $dir -ErrorAction Stop
                    Write-Host "  Supprime (essai $attempt)."
                    $removed = $true
                    break
                }
                catch {
                    Write-Host "  Essai $attempt/$MaxRetries echoue : $($_.Exception.Message)"
                    if ($attempt -lt $MaxRetries) {
                        Start-Sleep -Seconds $RetryDelaySeconds
                    }
                }
            }

            if (-not $removed) {
                Write-Warning "Impossible de supprimer '$dir' apres $MaxRetries essais. Un processus (session encore ouverte, IDE, build) le verrouille probablement toujours. Reessayez plus tard, ou identifiez le processus avec : Get-CimInstance Win32_Process | Where-Object { `$_.CommandLine -match [regex]::Escape('$dir') }"
            }
        }
    }
}
finally {
    Pop-Location
}
