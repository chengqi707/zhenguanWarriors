# 贞观勇士 APP Icon 生成器
# 生成 512x512 唐风图标：朱红底 + 金色六边形
# 运行方式: powershell -File tools/generate_icon.ps1

Add-Type -AssemblyName System.Drawing

$size = 512
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.SmoothingMode = "HighQuality"

# 背景色 - 朱红 #B7261E
$bg = [System.Drawing.Color]::FromArgb(255, 183, 38, 30)
$gfx.FillRectangle([System.Drawing.Brush][System.Drawing.SolidBrush]::new($bg), 0, 0, $size, $size)

# 外圈渐变效果 - 深色边缘
$darkRed = [System.Drawing.Color]::FromArgb(60, 0, 0, 0)
$gfx.FillEllipse([System.Drawing.Brush][System.Drawing.SolidBrush]::new($darkRed), 0, 0, $size, $size)

# 六边形（金色 #E6BF33）
$cx = $size / 2
$cy = $size / 2
$r = $size * 0.35
$gold = [System.Drawing.Color]::FromArgb(255, 230, 191, 51)
$pen = [System.Drawing.Pen]::new($gold, 8)
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

# 六边形顶点 (pointy-top)
$pts = New-Object System.Drawing.PointF[] 6
for ($i = 0; $i -lt 6; $i++) {
    $angle = [Math]::PI * (90 - 60 * $i) / 180
    $pts[$i] = New-Object System.Drawing.PointF(
        $cx + $r * [Math]::Cos($angle),
        $cy + $r * [Math]::Sin($angle))
}
$gfx.DrawPolygon($pen, $pts)

# 六边形填充（半透明金色）
$goldFill = [System.Drawing.Color]::FromArgb(60, 230, 191, 51)
$gfx.FillPolygon([System.Drawing.Brush][System.Drawing.SolidBrush]::new($goldFill), $pts)

# 内六角星装饰
$r2 = $size * 0.18
$pts2 = New-Object System.Drawing.PointF[] 6
for ($i = 0; $i -lt 6; $i++) {
    $angle = [Math]::PI * (90 - 60 * $i) / 180
    $pts2[$i] = New-Object System.Drawing.PointF(
        $cx + $r2 * [Math]::Cos($angle),
        $cy + $r2 * [Math]::Sin($angle))
}
$pen2 = [System.Drawing.Pen]::new($gold, 3)
$gfx.DrawPolygon($pen2, $pts2)

# 底部光晕
$glow = [System.Drawing.Color]::FromArgb(30, 255, 255, 255)
$gfx.FillEllipse([System.Drawing.Brush][System.Drawing.SolidBrush]::new($glow),
    $cx - $size*0.2, $cy + $size*0.15, $size*0.4, $size*0.1)

$gfx.Dispose()

# 输出
$outputDir = "$PSScriptRoot/../zhenguanWarriors/Assets/AppIcon"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$bmp.Save("$outputDir/icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "图标已生成: $outputDir/icon.png"
