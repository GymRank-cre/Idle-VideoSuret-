# publier-build.ps1 : publie le dossier webgl/ sur une branche orpheline « builds » à UN SEUL commit, pour ne plus faire
# grossir l'historique du dépôt avec chaque build WebGL (~17 Mo à chaque fois).
#
# *** NE PAS LANCER SANS L'ACCORD EXPLICITE DE SÉBASTIEN. *** Ce script n'a jamais été exécuté.
#
# Ce qu'il fait (et seulement cela) :
#   1. vérifie que le dossier webgl/ existe et contient Build/ et index.html ;
#   2. crée un dossier temporaire PROPRE (hors du dépôt de travail) et y copie webgl/ ;
#   3. y initialise un dépôt neuf, avec UN commit, sur la branche « builds » (historique orphelin) ;
#   4. pousse ce commit vers `origin` sur la branche « builds » UNIQUEMENT, avec --force-with-lease
#      (la force ne s'applique qu'à cette branche jetable, jamais à la branche de travail ni à main).
# Il ne touche ni à la branche de travail, ni à unity/, ni à vercel.json, ne crée aucune pull request,
# ne déploie rien, et n'utilise aucun secret : l'authentification est celle de git sur ce PC.
#
# Usage (après accord de Sébastien) :
#   powershell -ExecutionPolicy Bypass -File "E:\PROJET 1\sync\unity\tools\publier-build.ps1" -Confirm
# Sans -Confirm, le script affiche seulement ce qu'il ferait (mode « à blanc »).

param(
    [switch]$Confirm,
    [string]$Branch = "builds",
    [string]$Source = "E:\PROJET 1\sync\webgl",
    [string]$Repo   = "E:\PROJET 1\sync"
)

$ErrorActionPreference = "Stop"

function Fail($message) { Write-Host "ERREUR : $message" -ForegroundColor Red; exit 1 }

# --- 1. contrôles
if (-not (Test-Path (Join-Path $Source "index.html"))) { Fail "index.html introuvable dans $Source" }
if (-not (Test-Path (Join-Path $Source "Build")))      { Fail "dossier Build introuvable dans $Source" }
if ($Branch -in @("main", "master")) { Fail "refus : la branche cible ne peut pas être $Branch" }

$remote = (git -C $Repo remote get-url origin).Trim()
if (-not $remote) { Fail "pas de remote origin dans $Repo" }
$size = [math]::Round(((Get-ChildItem $Source -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)

Write-Host "Source        : $Source ($size Mo)"
Write-Host "Dépôt distant : $remote"
Write-Host "Branche cible : $Branch (historique orphelin, un seul commit, force-with-lease sur CETTE branche seulement)"

if (-not $Confirm) {
    Write-Host ""
    Write-Host "Mode à blanc : rien n'a été fait. Relancer avec -Confirm après accord de Sébastien." -ForegroundColor Yellow
    exit 0
}

# --- 2. dossier temporaire propre
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("publier-build-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Copy-Item -Path (Join-Path $Source "*") -Destination $tmp -Recurse -Force

    # --- 3. dépôt neuf, un seul commit sur la branche orpheline
    git -C $tmp init --quiet
    git -C $tmp checkout --quiet --orphan $Branch
    git -C $tmp add -A
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm"
    git -C $tmp commit --quiet -m "Build WebGL du $stamp (branche orpheline, un seul commit)"
    git -C $tmp remote add origin $remote

    # --- 4. envoi : uniquement la branche « builds » (on lit d'abord l'état distant pour que --force-with-lease fonctionne
    #        aussi aux publications suivantes ; si la branche n'existe pas encore, le fetch échoue sans conséquence)
    git -C $tmp fetch --quiet origin "+${Branch}:refs/remotes/origin/${Branch}" 2>$null
    git -C $tmp push --force-with-lease origin "${Branch}:${Branch}"
    if ($LASTEXITCODE -ne 0) { Fail "le push a échoué (code $LASTEXITCODE)" }
    Write-Host "Publié sur origin/$Branch." -ForegroundColor Green
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
