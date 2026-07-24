param(
  [Parameter(Mandatory = $true)]
  [string]$InputPath,
  [int]$Width = 1920,
  [int]$Height = 1080,
  [int]$LightQuality = 90,
  [int]$DarkQuality = 88,
  [double]$FocusX = 0.68,
  [double]$FocusY = 0.45
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$Assets = Join-Path $Root "assets"
$ResolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$LightOut = Join-Path $Assets "background-light.jpg"
$DarkOut = Join-Path $Assets "background-dark.jpg"
$GenerationInput = $ResolvedInput
$TempInput = $null

if ([string]::Equals($ResolvedInput, $LightOut, [System.StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($ResolvedInput, $DarkOut, [System.StringComparison]::OrdinalIgnoreCase)) {
  $TempInput = Join-Path ([System.IO.Path]::GetTempPath()) ("harley-theme-source-" + [Guid]::NewGuid().ToString("N") + [System.IO.Path]::GetExtension($ResolvedInput))
  Copy-Item -LiteralPath $ResolvedInput -Destination $TempInput -Force
  $GenerationInput = $TempInput
}

@(
  "System.Drawing",
  "System.Drawing.Common",
  "System.Drawing.Primitives",
  "System.Runtime",
  "System.Runtime.InteropServices",
  "System.Linq",
  "System.Private.Windows.GdiPlus",
  "System.Private.Windows.Core"
) | ForEach-Object {
  try {
    Add-Type -AssemblyName $_ -ErrorAction SilentlyContinue
  } catch {
  }
}

$ReferencedAssemblies = [AppDomain]::CurrentDomain.GetAssemblies() |
  Where-Object {
    $_.Location -and
    $_.GetName().Name -in @(
      "System.Drawing",
      "System.Drawing.Common",
      "System.Drawing.Primitives",
      "System.Runtime",
      "System.Runtime.InteropServices",
      "System.Linq",
      "System.Private.Windows.GdiPlus",
      "System.Private.Windows.Core"
    )
  } |
  Select-Object -ExpandProperty Location -Unique

$GdiPlusPath = Join-Path $PSHOME "System.Private.Windows.GdiPlus.dll"
if ((Test-Path -LiteralPath $GdiPlusPath) -and ($ReferencedAssemblies -notcontains $GdiPlusPath)) {
  $ReferencedAssemblies = @($ReferencedAssemblies) + $GdiPlusPath
}

$WindowsCorePath = Join-Path $PSHOME "System.Private.Windows.Core.dll"
if ((Test-Path -LiteralPath $WindowsCorePath) -and ($ReferencedAssemblies -notcontains $WindowsCorePath)) {
  $ReferencedAssemblies = @($ReferencedAssemblies) + $WindowsCorePath
}

# 避免重复加载类型定义
if (-not ([System.Management.Automation.PSTypeName]'HarleyThemeImage').Type) {
  Add-Type -ReferencedAssemblies $ReferencedAssemblies -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

public static class HarleyThemeImage {
  public static void Generate(string input, string lightOut, string darkOut, int width, int height, long lightQuality, long darkQuality, double fx, double fy) {
    using (var source = new Bitmap(input))
    using (var cover = ResizeCover(source, width, height, fx, fy)) {
      SaveJpeg(cover, lightOut, ClampQuality(lightQuality));
      using (var dark = DarkVariant(cover)) SaveJpeg(dark, darkOut, ClampQuality(darkQuality));
    }
  }

  private static Bitmap ResizeCover(Bitmap source, int targetWidth, int targetHeight, double fx, double fy) {
    double sourceRatio = (double)source.Width / source.Height;
    double targetRatio = (double)targetWidth / targetHeight;
    int cropWidth;
    int cropHeight;

    if (sourceRatio > targetRatio) {
      cropHeight = source.Height;
      cropWidth = (int)Math.Round(source.Height * targetRatio);
    } else {
      cropWidth = source.Width;
      cropHeight = (int)Math.Round(source.Width / targetRatio);
    }

    int cropX = (int)Math.Round((source.Width - cropWidth) * Clamp01(fx));
    int cropY = (int)Math.Round((source.Height - cropHeight) * Clamp01(fy));
    cropX = Math.Max(0, Math.Min(cropX, source.Width - cropWidth));
    cropY = Math.Max(0, Math.Min(cropY, source.Height - cropHeight));

    var target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
    using (var g = Graphics.FromImage(target)) {
      g.CompositingQuality = CompositingQuality.HighQuality;
      g.InterpolationMode = InterpolationMode.HighQualityBicubic;
      g.SmoothingMode = SmoothingMode.HighQuality;
      g.DrawImage(source,
        new Rectangle(0, 0, targetWidth, targetHeight),
        new Rectangle(cropX, cropY, cropWidth, cropHeight),
        GraphicsUnit.Pixel);
    }
    return target;
  }

  private static Bitmap DarkVariant(Bitmap source) {
    var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
    using (var g = Graphics.FromImage(bitmap)) {
      g.DrawImageUnscaled(source, 0, 0);
    }

    var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
    var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
    try {
      int bytes = Math.Abs(data.Stride) * bitmap.Height;
      byte[] buffer = new byte[bytes];
      Marshal.Copy(data.Scan0, buffer, 0, bytes);

      for (int y = 0; y < bitmap.Height; y++) {
        int row = y * data.Stride;
        double bottom = Math.Max(0.0, ((double)y / bitmap.Height) - 0.68) / 0.32;
        for (int x = 0; x < bitmap.Width; x++) {
          int i = row + x * 3;
          double b = buffer[i];
          double g = buffer[i + 1];
          double r = buffer[i + 2];
          double left = 1.0 - Math.Min(1.0, x / (bitmap.Width * 0.58));

          double veil = 0.46 * left + 0.18 + 0.16 * bottom;
          r = r * (0.62 - 0.18 * veil) + 8;
          g = g * (0.62 - 0.18 * veil) + 7;
          b = b * (0.66 - 0.16 * veil) + 10;

          buffer[i] = ClampByte(b);
          buffer[i + 1] = ClampByte(g);
          buffer[i + 2] = ClampByte(r);
        }
      }

      Marshal.Copy(buffer, 0, data.Scan0, bytes);
    } finally {
      bitmap.UnlockBits(data);
    }

    return bitmap;
  }

  private static void SaveJpeg(Bitmap bitmap, string output, long quality) {
    var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");
    using (var parameters = new EncoderParameters(1)) {
      parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
      bitmap.Save(output, encoder, parameters);
    }
  }

  private static double Clamp01(double value) {
    if (value < 0) return 0;
    if (value > 1) return 1;
    return value;
  }

  private static long ClampQuality(long value) {
    if (value < 60) return 60;
    if (value > 96) return 96;
    return value;
  }

  private static byte ClampByte(double value) {
    if (value < 0) return 0;
    if (value > 255) return 255;
    return (byte)Math.Round(value);
  }
}
"@
}

try {
  [HarleyThemeImage]::Generate($GenerationInput, $LightOut, $DarkOut, $Width, $Height, $LightQuality, $DarkQuality, $FocusX, $FocusY)
  Write-Host "Generated runtime assets: assets/background-light.jpg and assets/background-dark.jpg"
} finally {
  if ($TempInput -and (Test-Path -LiteralPath $TempInput)) {
    Remove-Item -LiteralPath $TempInput -Force
  }
}
