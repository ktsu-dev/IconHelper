# ktsu.IconHelper

> A utility library for working with icons and images in .NET applications.

[![License](https://img.shields.io/github/license/ktsu-dev/IconHelper)](https://github.com/ktsu-dev/IconHelper/blob/main/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/ktsu.IconHelper.svg)](https://www.nuget.org/packages/ktsu.IconHelper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.IconHelper.svg)](https://www.nuget.org/packages/ktsu.IconHelper/)
[![Build Status](https://github.com/ktsu-dev/IconHelper/workflows/build/badge.svg)](https://github.com/ktsu-dev/IconHelper/actions)
[![GitHub Stars](https://img.shields.io/github/stars/ktsu-dev/IconHelper?style=social)](https://github.com/ktsu-dev/IconHelper/stargazers)

## Introduction

IconHelper is a .NET library designed to simplify working with icons and images across different platforms and frameworks. It provides utilities for loading, manipulating, and converting icons between various formats, making it easier to manage application visual assets.

## Features

- **Icon Loading**: Load icons from files, resources, or embedded assets
- **Format Conversion**: Convert between different icon formats (ICO, PNG, SVG, etc.)
- **Resolution Management**: Extract and generate icons at different resolutions
- **Platform Integration**: Work with platform-specific icon formats
- **Color Manipulation**: Apply filters, change colors, or adjust transparency
- **Batch Processing**: Process multiple icons with consistent settings
- **Memory Efficiency**: Optimized for minimal memory usage

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.IconHelper
```

### .NET CLI

```bash
dotnet add package ktsu.IconHelper
```

### Package Reference

```xml
<PackageReference Include="ktsu.IconHelper" Version="x.y.z" />
```

## Usage Examples

### Basic Icon Loading

```csharp
using ktsu.IconHelper;

// Load an icon from a file
var icon = IconLoader.FromFile("path/to/icon.ico");

// Load from embedded resource
var resourceIcon = IconLoader.FromResource("MyNamespace.Resources.appicon.ico");

// Load from base64 string
var base64Icon = IconLoader.FromBase64(base64String);
```

### Icon Conversion

```csharp
using ktsu.IconHelper;

// Convert between formats
var pngBytes = IconConverter.ToPng(iconInstance);
var icoBytes = IconConverter.ToIco(bitmapInstance);
var svgContent = IconConverter.ToSvg(iconInstance);

// Save directly to file
IconConverter.SaveAsPng(iconInstance, "output.png");
```

### Resolution Extraction

```csharp
using ktsu.IconHelper;

// Get specific sizes from multi-resolution icon
var icon = IconLoader.FromFile("multisize.ico");
var icon16 = IconResolution.Extract(icon, 16, 16);
var icon32 = IconResolution.Extract(icon, 32, 32);
var icon64 = IconResolution.Extract(icon, 64, 64);

// Create a multi-resolution icon
var multiIcon = IconResolution.Combine(new[] { icon16, icon32, icon64 });
IconLoader.SaveToFile(multiIcon, "combined.ico");
```

### Color Manipulation

```csharp
using ktsu.IconHelper;

// Adjust icon colors
var icon = IconLoader.FromFile("original.ico");

// Change specific color
var replacedColorIcon = IconColorizer.ReplaceColor(icon, Color.Blue, Color.Red);

// Apply tint
var tintedIcon = IconColorizer.ApplyTint(icon, Color.FromArgb(128, 255, 0, 0));

// Invert colors
var invertedIcon = IconColorizer.Invert(icon);

// Adjust transparency
var transparentIcon = IconColorizer.SetTransparency(icon, 0.5f);
```

### Platform-Specific Usage

```csharp
using ktsu.IconHelper;
using ktsu.IconHelper.Platforms;

// Windows-specific icon handling
var windowsIcon = WindowsIconHelper.ExtractFromExe("application.exe");
var fileTypeIcon = WindowsIconHelper.GetFileTypeIcon(".pdf");

// macOS-specific icon handling
var macIcon = MacIconHelper.CreateFromImage("image.png");

// Cross-platform icon conversion
var platformIcon = PlatformIconConverter.ConvertToCurrent(genericIcon);
```

## API Reference

### `IconLoader` Class

Provides methods for loading icons from various sources.

#### Methods

| Name | Parameters | Return Type | Description |
|------|------------|-------------|-------------|
| `FromFile` | `string path` | `Icon` | Loads an icon from a file path |
| `FromResource` | `string resourceName` | `Icon` | Loads an icon from an embedded resource |
| `FromBase64` | `string base64String` | `Icon` | Creates an icon from a base64 encoded string |
| `FromStream` | `Stream stream` | `Icon` | Loads an icon from a stream |
| `SaveToFile` | `Icon icon, string path` | `void` | Saves an icon to a file |

### `IconConverter` Class

Provides methods for converting icons between different formats.

#### Methods

| Name | Parameters | Return Type | Description |
|------|------------|-------------|-------------|
| `ToPng` | `Icon icon` | `byte[]` | Converts an icon to PNG format |
| `ToIco` | `Bitmap bitmap` | `byte[]` | Converts a bitmap to ICO format |
| `ToSvg` | `Icon icon` | `string` | Converts an icon to SVG format |
| `SaveAsPng` | `Icon icon, string path` | `void` | Saves an icon as a PNG file |
| `SaveAsIco` | `Bitmap bitmap, string path` | `void` | Saves a bitmap as an ICO file |

### `IconResolution` Class

Provides methods for working with multi-resolution icons.

#### Methods

| Name | Parameters | Return Type | Description |
|------|------------|-------------|-------------|
| `Extract` | `Icon icon, int width, int height` | `Icon` | Extracts an icon at the specified resolution |
| `Combine` | `IEnumerable<Icon> icons` | `Icon` | Combines multiple icons into a multi-resolution icon |
| `GetAvailableSizes` | `Icon icon` | `Size[]` | Gets all available sizes in a multi-resolution icon |

### `IconColorizer` Class

Provides methods for manipulating icon colors.

#### Methods

| Name | Parameters | Return Type | Description |
|------|------------|-------------|-------------|
| `ReplaceColor` | `Icon icon, Color oldColor, Color newColor` | `Icon` | Replaces a specific color in an icon |
| `ApplyTint` | `Icon icon, Color tint` | `Icon` | Applies a color tint to an icon |
| `Invert` | `Icon icon` | `Icon` | Inverts the colors of an icon |
| `SetTransparency` | `Icon icon, float alpha` | `Icon` | Adjusts the transparency of an icon |

## Contributing

Contributions are welcome! Here's how you can help:

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please make sure to update tests as appropriate and adhere to the existing coding style.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
