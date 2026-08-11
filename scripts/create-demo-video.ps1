<#
.SYNOPSIS
    Compose a product-tour video from real Ultrawide Monitor captures.

    The script intentionally uses the real application screenshots captured from
    the installed build, then adds motion-design typography, camera movement and
    cross-fades. It does not invent application UI, so the result stays faithful
    to the product.
#>

[CmdletBinding()]
param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "La commande $FilePath a échoué avec le code $LASTEXITCODE."
    }
}

function Copy-IfPresent {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Capture introuvable : $Source"
    }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$videoRoot = Join-Path $root "artifacts\video"
$refRoot = Join-Path $videoRoot "refs"
$sceneRoot = Join-Path $videoRoot "scenes"
New-Item -ItemType Directory -Path $videoRoot,$refRoot,$sceneRoot -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $videoRoot "UltrawideMonitor-Product-Tour.mp4"
}

$magick = (Get-Command magick -ErrorAction Stop).Source
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$font = "C:/Windows/Fonts/segoeui.ttf"
$fontSemi = "C:/Windows/Fonts/seguisb.ttf"

$tempRoot = Join-Path $env:TEMP "UltrawideMonitorVideo"
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

# The two live captures are created from the installed build before invoking this
# script. The remaining references are the user's original application captures.
$logoSource = "C:\Users\RED\Downloads\logo\UltrawideMonitor.png"
if (-not (Test-Path -LiteralPath $logoSource)) {
    $logoSource = Join-Path $root "src\UltrawideToys.App\assets\ultrawidemonitor.png"
}

Copy-IfPresent $logoSource (Join-Path $refRoot "logo.png")
Copy-IfPresent (Join-Path $root "artifacts\video_refs_app_zones.png") (Join-Path $refRoot "zones.png")
Copy-IfPresent (Join-Path $root "artifacts\video_refs_app_editor.png") (Join-Path $refRoot "editor.png")
Copy-IfPresent (Join-Path $root "artifacts\video_refs_app_settings.png") (Join-Path $refRoot "settings-live.png")
Copy-IfPresent "C:\Users\RED\AppData\Local\Temp\codex-clipboard-fe6ef1cc-9822-45bf-9d95-73283f30679b.png" (Join-Path $refRoot "card.png")
Copy-IfPresent "C:\Users\RED\AppData\Local\Temp\codex-clipboard-09ca84ad-eaeb-44a9-96a1-4e1876bd0254.png" (Join-Path $refRoot "appearance.png")

$logoSmall = Join-Path $tempRoot "logo-small.png"
Invoke-External $magick @((Join-Path $refRoot "logo.png"),"-resize","250x250",$logoSmall)

function New-GradientBackground {
    param([string]$Destination)
    Invoke-External $magick @(
        "-size","1920x1080",
        "gradient:#071225-#0e355e",
        "-colorspace","sRGB",
        $Destination
    )
}

function Add-Text {
    param(
        [string]$InputPath,
        [string]$OutputPath,
        [string]$Text,
        [int]$PointSize,
        [string]$Gravity,
        [string]$Geometry,
        [string]$Fill = "#f6f9ff",
        [string]$FontPath = $fontSemi
    )
    Invoke-External $magick @(
        $InputPath,
        "-font",$FontPath,
        "-pointsize",$PointSize.ToString(),
        "-fill",$Fill,
        "-gravity",$Gravity,
        "-annotate",$Geometry,$Text,
        $OutputPath
    )
}

function New-SceneWithScreenshot {
    param(
        [string]$Screenshot,
        [string]$Destination,
        [string]$Eyebrow,
        [string]$Title,
        [string]$Body,
        [string]$Resize = "1200x770"
    )

    $bg = Join-Path $tempRoot ((Split-Path $Destination -Leaf) + ".bg.png")
    $shot = Join-Path $tempRoot ((Split-Path $Destination -Leaf) + ".shot.png")
    New-GradientBackground $bg

    Invoke-External $magick @(
        $Screenshot,
        "-resize",$Resize,
        "-bordercolor","#4c8dca",
        "-border","2",
        $shot
    )

    # Soft shadow behind the real application capture.
    $shadow = Join-Path $tempRoot ((Split-Path $Destination -Leaf) + ".shadow.png")
    Invoke-External $magick @($shot,"-channel","A","-blur","0x16","+channel","-fill","#00000088","-colorize","100",$shadow)

    $composed = Join-Path $tempRoot ((Split-Path $Destination -Leaf) + ".composed.png")
    Invoke-External $magick @(
        $bg,$shadow,
        "-gravity","East","-geometry","+145+0","-composite",
        $shot,
        "-gravity","East","-geometry","+160+0","-composite",
        $composed
    )

    $withText = Join-Path $tempRoot ((Split-Path $Destination -Leaf) + ".text.png")
    Invoke-External $magick @(
        $composed,
        "-font",$font,
        "-pointsize","25",
        "-fill","#62b4ff",
        "-gravity","West",
        "-annotate","+120-210",$Eyebrow,
        "-font",$fontSemi,
        "-pointsize","54",
        "-fill","#f6f9ff",
        "-annotate","+120-120",$Title,
        "-font",$font,
        "-pointsize","27",
        "-fill","#c1d2e7",
        "-annotate","+120+10",$Body,
        $withText
    )

    Invoke-External $magick @(
        $withText,
        "-fill","#2e8fff",
        "-draw","roundrectangle 120,735 300,743 4,4",
        $Destination
    )
}

# Scene 01 — brand lock-up.
$scene1 = Join-Path $sceneRoot "01-intro.png"
New-GradientBackground $scene1
Invoke-External $magick @(
    $scene1,$logoSmall,
    "-gravity","North","-geometry","+0+215","-composite",
    "-font",$fontSemi,"-pointsize","78","-fill","#f6f9ff","-gravity","Center",
    "-annotate","+0+205","Ultrawide Monitor",
    "-font",$font,"-pointsize","30","-fill","#a9c6e9",
    "-annotate","+0+325","Le gestionnaire de zones pour écrans larges et ultralarges",
    "-fill","#2e8fff","-draw","roundrectangle 760,790 1160,798 4,4",
    $scene1
)

# Scene 02 — real Zones page capture.
$scene2 = Join-Path $sceneRoot "02-zones.png"
New-SceneWithScreenshot (Join-Path $refRoot "zones.png") $scene2 `
    "VOS ÉCRANS, EN UN COUP D'ŒIL" `
    "Organisez chaque écran" `
    "Un aperçu clair de la résolution, du DPI et de la disposition active." `
    "900x610"

# Scene 03 — real editor overlay capture, darkened to keep the focus on the zones.
$scene3 = Join-Path $sceneRoot "03-editor.png"
$editorBg = Join-Path $tempRoot "editor-bg.png"
Invoke-External $magick @(
    (Join-Path $refRoot "editor.png"),
    "-resize","1920x540^","-gravity","Center","-extent","1920x540",
    "-blur","0x1.2",
    "-fill","#061b31","-colorize","25%",
    $editorBg
)
New-GradientBackground $scene3
Invoke-External $magick @(
    $scene3,$editorBg,
    "-gravity","South","-geometry","+0+0","-composite",
    "-font",$font,"-pointsize","24","-fill","#62b4ff","-gravity","West",
    "-annotate","+120-390","ÉDITEZ EN DIRECT",
    "-font",$fontSemi,"-pointsize","54","-fill","#f6f9ff",
    "-annotate","+120-300","Déplacez les séparateurs",
    "-font",$font,"-pointsize","27","-fill","#c1d2e7",
    "-annotate","+120-210","Les dimensions et pourcentages suivent chaque mouvement, sans espace entre les zones.",
    "-fill","#2e8fff","-draw","roundrectangle 120,770 420,778 4,4",
    $scene3
)

# Scene 04 — a clean motion-design diagram that explains the snapping behavior.
$scene4 = Join-Path $sceneRoot "04-snap.png"
New-GradientBackground $scene4
Invoke-External $magick @(
    $scene4,
    "-fill","#0b2440","-stroke","#2e8fff","-strokewidth","3",
    "-draw","roundrectangle 720,235 1160,770 26,26",
    "-fill","#154a75","-stroke","#75c5ff","-strokewidth","2",
    "-draw","roundrectangle 744,285 866,720 16,16",
    "-fill","#1d5e8e","-draw","roundrectangle 872,285 1008,720 16,16",
    "-fill","#226e9e","-draw","roundrectangle 1014,285 1136,720 16,16",
    "-font",$fontSemi,"-pointsize","54","-fill","#f6f9ff","-gravity","West",
    "-annotate","+120-180","Vos fenêtres se calent instantanément",
    "-font",$font,"-pointsize","27","-fill","#c1d2e7",
    "-annotate","+120-90","Maximiser, double-cliquer ou utiliser Windows + flèches : chaque fenêtre rejoint sa zone.",
    "-font",$fontSemi,"-pointsize","21","-fill","#d9edff","-gravity","Center",
    "-annotate","+0-5","NAVIGATEUR",
    "-annotate","+0+155","TRAVAIL",
    "-annotate","+0+315","MESSAGERIE",
    $scene4
)

# Scene 05 — appearance and language, using the real settings page.
$scene5 = Join-Path $sceneRoot "05-settings.png"
New-SceneWithScreenshot (Join-Path $refRoot "appearance.png") $scene5 `
    "UNE INTERFACE QUI VOUS RESSEMBLE" `
    "Une interface à votre image" `
    "Le thème et la langue restent cohérents avec votre environnement Windows." `
    "900x610"

# Scene 06 — closing lock-up.
$scene6 = Join-Path $sceneRoot "06-outro.png"
New-GradientBackground $scene6
Invoke-External $magick @(
    $scene6,$logoSmall,
    "-gravity","North","-geometry","+0+190","-composite",
    "-font",$fontSemi,"-pointsize","68","-fill","#f6f9ff","-gravity","Center",
    "-annotate","+0+185","Travaillez mieux sur grand écran",
    "-font",$font,"-pointsize","30","-fill","#a9c6e9",
    "-annotate","+0+300","Ultrawide Monitor · Gil Breysse (RED)",
    "-fill","#2e8fff","-draw","roundrectangle 760,760 1160,768 4,4",
    "-font",$font,"-pointsize","25","-fill","#d8e8fb",
    "-annotate","+0+850","ultrawide-monitor",
    $scene6
)

# Render a gentle camera move for each still. Cross-fades are added below so the
# presentation feels like motion design rather than a sequence of screenshots.
$fps = 30
$durations = @(4.0,5.0,5.0,5.0,4.5,4.0)
$clips = @()
for ($i = 0; $i -lt 6; $i++) {
    $scene = Join-Path $sceneRoot ("0{0}-" -f ($i + 1))
    $sceneFile = Get-ChildItem $sceneRoot -Filter (("0{0}-*.png" -f ($i + 1))) | Select-Object -First 1
    $clip = Join-Path $tempRoot ("scene-{0}.mp4" -f ($i + 1))
    $frames = [int]($durations[$i] * $fps)
    $zoom = if (($i % 2) -eq 0) { "min(zoom+0.00055,1.055)" } else { "max(zoom-0.00035,1.0)" }
    $fadeStart = [math]::Max(0.1, $durations[$i] - 0.35)
    Invoke-External $ffmpeg @(
        "-y","-loglevel","error","-loop","1","-i",$sceneFile.FullName,
        "-vf","zoompan=z='$zoom':d=${frames}:s=1920x1080:fps=${fps},fade=t=in:st=0:d=0.35,fade=t=out:st=${fadeStart}:d=0.35,format=yuv420p",
        "-frames:v",$frames.ToString(),"-an",$clip
    )
    $clips += $clip
}

$concatList = Join-Path $tempRoot "clips.txt"
$lines = $clips | ForEach-Object { "file '$($_.Replace("'", "'\''"))'" }
[IO.File]::WriteAllLines($concatList, $lines, (New-Object Text.UTF8Encoding($false)))
Invoke-External $ffmpeg @(
    "-y","-loglevel","error","-f","concat","-safe","0","-i",$concatList,
    "-an","-c:v","libx264","-preset","medium","-crf","19","-movflags","+faststart",$OutputPath
)

Write-Output "Vidéo créée : $OutputPath"
