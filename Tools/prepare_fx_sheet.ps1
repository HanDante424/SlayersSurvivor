<#
이펙트 스프라이트 시트를 게임용으로 정리합니다. (Python 없이 Windows PowerShell 에서 실행)

- 칸 경계를 직접 지정할 수 있음 (행/열 높이가 제각각인 AI 생성 시트용)
- 모든 칸을 같은 크기로 늘리거나 줄여 균일한 그리드로 다시 배치
- 거의 투명한 잡티(AlphaCut 미만)를 완전히 투명하게

usage:
  powershell -File Tools\prepare_fx_sheet.ps1 -In <src.png> -Out <dst.png> -Rows 2 -Cols 8
  powershell -File Tools\prepare_fx_sheet.ps1 -In <src.png> -Out <dst.png> -Rows 4 -Cols 6 `
      -RowEdges "0,209,424,595,724" -ColEdges "0,363,724,1087,1448,1811,2172" -Inset 1

RowEdges/ColEdges: 칸 경계 (개수 = Rows+1 / Cols+1). 생략하면 균등 분할.
Inset: 각 칸 안쪽으로 잘라낼 픽셀 수 (시트에 그려진 격자선 제거용).
#>
param(
    [Parameter(Mandatory)] [string]$In,
    [Parameter(Mandatory)] [string]$Out,
    [Parameter(Mandatory)] [int]$Rows,
    [Parameter(Mandatory)] [int]$Cols,
    [string]$RowEdges,   # "0,209,424" 처럼 쉼표 구분 (powershell -File 에서도 동작하도록 문자열)
    [string]$ColEdges,
    [int]$Inset = 0,
    [int]$AlphaCut = 40
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$src = New-Object System.Drawing.Bitmap (Resolve-Path $In).Path
$W = $src.Width; $H = $src.Height
$re = if ($RowEdges) { [int[]]($RowEdges -split ',') } else { [int[]](0..$Rows | ForEach-Object { [math]::Round($_ * $H / $Rows) }) }
$ce = if ($ColEdges) { [int[]]($ColEdges -split ',') } else { [int[]](0..$Cols | ForEach-Object { [math]::Round($_ * $W / $Cols) }) }
if ($re.Count -ne $Rows + 1 -or $ce.Count -ne $Cols + 1) { throw "RowEdges/ColEdges count mismatch" }

# 출력 칸 크기 = 가장 큰 칸 (4의 배수로 올림)
$cellW = 0; $cellH = 0
for ($c = 0; $c -lt $Cols; $c++) { $cellW = [math]::Max($cellW, $ce[$c + 1] - $ce[$c]) }
for ($r = 0; $r -lt $Rows; $r++) { $cellH = [math]::Max($cellH, $re[$r + 1] - $re[$r]) }
$cellW = [int]([math]::Ceiling($cellW / 4) * 4); $cellH = [int]([math]::Ceiling($cellH / 4) * 4)

$dst = New-Object System.Drawing.Bitmap ($cellW * $Cols), ($cellH * $Rows), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.Clear([System.Drawing.Color]::Transparent)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$attr = New-Object System.Drawing.Imaging.ImageAttributes
$attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)   # 칸 가장자리 번짐 방지

for ($r = 0; $r -lt $Rows; $r++) {
    for ($c = 0; $c -lt $Cols; $c++) {
        $sx = $ce[$c] + $Inset; $sy = $re[$r] + $Inset
        $sw = $ce[$c + 1] - $ce[$c] - 2 * $Inset
        $sh = $re[$r + 1] - $re[$r] - 2 * $Inset
        $dRect = New-Object System.Drawing.Rectangle ($c * $cellW), ($r * $cellH), $cellW, $cellH
        $g.DrawImage($src, $dRect, $sx, $sy, $sw, $sh, [System.Drawing.GraphicsUnit]::Pixel, $attr)
    }
}
$g.Dispose(); $src.Dispose()

# 잡티 제거
$rect = New-Object System.Drawing.Rectangle 0, 0, $dst.Width, $dst.Height
$data = $dst.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, $dst.PixelFormat)
$bytes = New-Object byte[] ($data.Stride * $data.Height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$cleared = 0
for ($i = 3; $i -lt $bytes.Length; $i += 4) {
    if ($bytes[$i] -ne 0 -and $bytes[$i] -lt $AlphaCut) {
        $bytes[$i] = 0; $bytes[$i - 1] = 0; $bytes[$i - 2] = 0; $bytes[$i - 3] = 0; $cleared++
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$dst.UnlockBits($data)

$outPath = [System.IO.Path]::GetFullPath($Out)
$dst.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $outPath  $($dst.Width)x$($dst.Height)  cell ${cellW}x${cellH}  cleared $cleared px"
$dst.Dispose()
