$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskBrandDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\FlyyOptimizer.App\Assets'
[System.IO.Directory]::CreateDirectory($taskBrandDirectory) | Out-Null
$taskImages = @()
foreach ($taskSize in @(16,24,32,48,64,128,256)) {
    $taskBitmap = [System.Drawing.Bitmap]::new($taskSize,$taskSize)
    $taskGraphics = [System.Drawing.Graphics]::FromImage($taskBitmap)
    $taskShape = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $taskBlue = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#0062CC'))
    $taskWhite = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $taskMemory = [System.IO.MemoryStream]::new()
    try {
        $taskGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $taskGraphics.ScaleTransform($taskSize/64.0,$taskSize/64.0)
        $taskShape.AddArc(0,0,32,32,180,90); $taskShape.AddArc(32,0,32,32,270,90)
        $taskShape.AddArc(32,32,32,32,0,90); $taskShape.AddArc(0,32,32,32,90,90)
        $taskShape.CloseFigure()
        $taskGraphics.FillPath($taskBlue,$taskShape)
        [System.Drawing.PointF[]]$taskPoints = @([System.Drawing.PointF]::new(16,16),[System.Drawing.PointF]::new(51,16),[System.Drawing.PointF]::new(42,25),[System.Drawing.PointF]::new(26,25),[System.Drawing.PointF]::new(26,30),[System.Drawing.PointF]::new(40,30),[System.Drawing.PointF]::new(31,39),[System.Drawing.PointF]::new(26,39),[System.Drawing.PointF]::new(26,48),[System.Drawing.PointF]::new(16,48))
        $taskGraphics.FillPolygon($taskWhite,$taskPoints)
        $taskBitmap.Save($taskMemory,[System.Drawing.Imaging.ImageFormat]::Png)
        $taskImages += [pscustomobject]@{Size=$taskSize;Bytes=$taskMemory.ToArray()}
    } finally { $taskGraphics.Dispose(); $taskBitmap.Dispose(); $taskShape.Dispose(); $taskBlue.Dispose(); $taskWhite.Dispose(); $taskMemory.Dispose() }
}
$taskIconFile = [System.IO.File]::Create((Join-Path $taskBrandDirectory 'Flyy.ico'))
$taskWriter = [System.IO.BinaryWriter]::new($taskIconFile)
try {
    $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskImages.Count)
    $taskOffset = 6 + 16 * $taskImages.Count
    foreach ($taskImage in $taskImages) {
        $taskDimension = if ($taskImage.Size -eq 256) {0} else {$taskImage.Size}
        $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([uint16]0)
        $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32)
        $taskWriter.Write([uint32]$taskImage.Bytes.Length); $taskWriter.Write([uint32]$taskOffset)
        $taskOffset += $taskImage.Bytes.Length
    }
    foreach ($taskImage in $taskImages) { $taskWriter.Write([byte[]]$taskImage.Bytes) }
} finally { $taskWriter.Dispose(); $taskIconFile.Dispose() }
Write-Output 'Flyy.ico generado: 16, 24, 32, 48, 64, 128 y 256 px.'
