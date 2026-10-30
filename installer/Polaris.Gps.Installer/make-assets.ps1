<#
  Builds the installer assets into OutDir; run by the .wixproj before each build (target GenerateInstallerAssets).
  - splash.bmp  493x312: the application splash, full page of the language dialog.
  - dialog.bmp  493x312: WixUIDialogBmp (exit, maintenance... pages): splash strip on the left, white on the right,
                where WiX writes black text.
  - banner.bmp  493x58:  WixUIBannerBmp (top of the inner pages): white, with the small application icon on the right.
  - author.bmp, navigator-icon.bmp, navigator-app.bmp: pictures of the Mendikat and Mendikat Navigator pages.
  - button-web.bmp, button-donate.<lang>.bmp, button-play.<lang>.bmp: faces of the web, donation and Google Play
    buttons.
  - licencia.<lang>.rtf: the licence texts (License\licencia.<lang>.txt, UTF-8) as RTF for the licence page.
    Text format: "# " title, "## " heading, other lines are paragraphs.
#>
param(
    [Parameter(Mandatory)] [string] $Splash,
    [Parameter(Mandatory)] [string] $Icon,
    [Parameter(Mandatory)] [string] $Author,
    [Parameter(Mandatory)] [string] $NavigatorIcon,
    [Parameter(Mandatory)] [string] $NavigatorApp,
    [Parameter(Mandatory)] [string] $LicenseDir,
    [Parameter(Mandatory)] [string] $OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null

$source = [System.Drawing.Image]::FromFile((Resolve-Path $Splash))
try {
    # Draws the region (x, y, width, height) of the splash into the target rectangle of a new bitmap.
    function New-Asset([int] $width, [int] $height, [scriptblock] $draw, [string] $name) {
        $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::White)
        & $draw $graphics
        $graphics.Dispose()
        $bitmap.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Bmp)
        $bitmap.Dispose()
    }
    function Copy-Region($graphics, [int] $tx, [int] $ty, [int] $tw, [int] $th, [int] $sx, [int] $sy, [int] $sw, [int] $sh) {
        $graphics.DrawImage($source, (New-Object System.Drawing.Rectangle $tx, $ty, $tw, $th),
            (New-Object System.Drawing.Rectangle $sx, $sy, $sw, $sh), [System.Drawing.GraphicsUnit]::Pixel)
    }

    $w = $source.Width; $h = $source.Height
    # Full page: whole width, centred band with the page proportions (cuts the thin dark edges).
    $bandHeight = [int]($w * 312 / 493)
    New-Asset 493 312 { param($g) Copy-Region $g 0 0 493 312 0 ([int](($h - $bandHeight) / 2)) $w $bandHeight } 'splash.bmp'

    # Left strip with the hiker (the left 21% of the splash), 164 px wide like the standard WiX bitmap.
    $stripHeight = $h - 16
    $stripWidth = [int]($stripHeight * 164 / 312)
    New-Asset 493 312 { param($g) Copy-Region $g 0 0 164 312 ([int]($w * 0.04)) 8 $stripWidth $stripHeight } 'dialog.bmp'

    # Banner: white, with only the small application icon at the right edge, clear of the page title and description.
    $iconImage = [System.Drawing.Image]::FromFile((Resolve-Path $Icon))
    try {
        New-Asset 493 58 { param($g) $g.DrawImage($iconImage, (New-Object System.Drawing.Rectangle 443, 8, 42, 42)) } 'banner.bmp'
    }
    finally {
        $iconImage.Dispose()
    }
}
finally {
    $source.Dispose()
}

# Pictures of the Mendikat and Mendikat Navigator pages, at the pixel size of their controls (1 dialog unit = 4/3 px)
# and on the dialog grey, so transparent corners blend in.
function Save-Picture([string] $path, [int] $width, [int] $height, [string] $name) {
    $image = [System.Drawing.Image]::FromFile((Resolve-Path $path))
    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::FromArgb(240, 240, 240))
        # Fit inside, keeping proportions, centred
        $scale = [Math]::Min($width / $image.Width, $height / $image.Height)
        $w = [int]($image.Width * $scale); $h = [int]($image.Height * $scale)
        $graphics.DrawImage($image, (New-Object System.Drawing.Rectangle ([int](($width - $w) / 2)), ([int](($height - $h) / 2)), $w, $h))
        $bitmap.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Bmp)
    }
    finally {
        $graphics.Dispose(); $bitmap.Dispose(); $image.Dispose()
    }
}
Save-Picture $Author 88 88 'author.bmp'
Save-Picture $NavigatorIcon 53 53 'navigator-icon.bmp'
Save-Picture $NavigatorApp 107 240 'navigator-app.bmp'

# Faces of the big buttons of the Mendikat and Mendikat Navigator pages, at the pixel size of the control
# (1 dialog unit = 4/3 px): coloured background, white Segoe MDL2 icon and bold text, centred; the text shrinks until
# it fits. Bitmaps because MSI button texts are Windows-1252 (no heart symbol).
function Save-ButtonFace([string] $glyph, [string] $text, [System.Drawing.Color] $back, [int] $width, [int] $height, [string] $name) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $iconFont = New-Object System.Drawing.Font 'Segoe MDL2 Assets', 16, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::GenericTypographic
    $gap = 10
    $textFont = $null
    try {
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.Clear($back)
        $iconSize = $graphics.MeasureString($glyph, $iconFont, 1000, $format)
        for ($size = 15; ; $size--) {
            if ($textFont) { $textFont.Dispose() }
            $textFont = New-Object System.Drawing.Font 'Segoe UI', $size, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $textSize = $graphics.MeasureString($text, $textFont, 1000, $format)
            if ($size -le 11 -or $iconSize.Width + $gap + $textSize.Width -le $width - 16) { break }
        }
        $x = ($width - $iconSize.Width - $gap - $textSize.Width) / 2
        $brush = [System.Drawing.Brushes]::White
        $graphics.DrawString($glyph, $iconFont, $brush, $x, ($height - $iconSize.Height) / 2 + 1, $format)
        $graphics.DrawString($text, $textFont, $brush, $x + $iconSize.Width + $gap, ($height - $textSize.Height) / 2, $format)
        $bitmap.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Bmp)
    }
    finally {
        if ($textFont) { $textFont.Dispose() }
        $iconFont.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
}
$globe = [string][char]0xE774; $heart = [string][char]0xEB52; $download = [string][char]0xE896
$blue = [System.Drawing.Color]::FromArgb(0, 103, 192)
$red = [System.Drawing.Color]::FromArgb(200, 40, 60)
$orange = [System.Drawing.Color]::FromArgb(230, 100, 20)
# Mendikat page: 250x26 dialog units
Save-ButtonFace $globe 'https://www.mendikat.net' $blue 333 35 'button-web.bmp'
# Non-ASCII letters as char codes: Windows PowerShell reads this BOM-less script as ANSI.
$donate = @{
    es = "$([char]0xA1)Dona ahora!"
    ca = 'Feu un donatiu!'
    eu = 'Egin dohaintza!'
    fr = 'Faites un don !'
    pt = "Fa$([char]0xE7)a um donativo!"
}
foreach ($language in $donate.Keys) { Save-ButtonFace $heart $donate[$language] $red 333 35 "button-donate.$language.bmp" }
# Mendikat Navigator page: 170x26 dialog units
$play = @{
    es = "Desc$([char]0xE1)rgala en Google Play"
    ca = 'Descarrega-la a Google Play'
    eu = 'Deskargatu Google Playn'
    fr = "T$([char]0xE9)l$([char]0xE9)chargez-la sur Google Play"
    pt = 'Descarrega-a no Google Play'
}
foreach ($language in $play.Keys) { Save-ButtonFace $download $play[$language] $orange 227 35 "button-play.$language.bmp" }
