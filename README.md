# ktsu.IconHelper

> A .NET command-line tool that batch-normalizes icon images by recoloring, trimming, squaring, and resizing them into consistent PNGs.

[![License](https://img.shields.io/github/license/ktsu-dev/IconHelper.svg?label=License&logo=github)](LICENSE.md)
[![GitHub release](https://img.shields.io/github/v/release/ktsu-dev/IconHelper?label=Release&logo=github)](https://github.com/ktsu-dev/IconHelper/releases)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/IconHelper?label=Commits&logo=github)](https://github.com/ktsu-dev/IconHelper/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/IconHelper?label=Contributors&logo=github)](https://github.com/ktsu-dev/IconHelper/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/IconHelper/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/IconHelper/actions)

## Introduction

`ktsu.IconHelper` is a small console application for preparing icon sets. Icon packs downloaded from
different sources rarely agree on colour, padding, or canvas size, which makes them look inconsistent
when placed side by side in a UI. IconHelper takes a directory of images, converts each one to a
monochrome silhouette tinted with a colour of your choosing, trims away the transparent margins,
centres the artwork on a square canvas, and writes out a uniformly sized PNG.

It is built on [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp), so it runs anywhere
.NET does and needs no native image libraries or platform-specific dependencies.

## Features

- **Batch Processing**: Processes every file in an input directory in a single run
- **Colour Tinting**: Flattens each image to a silhouette and tints it with any HTML/CSS colour value
- **Automatic Trimming**: Detects the bounding box of non-transparent pixels and crops to it
- **Square Centring**: Pads the trimmed artwork to a square canvas so icons align consistently
- **Configurable Padding**: Insets the artwork by a fixed number of pixels per side without changing the output dimensions
- **Downscale-Only Resizing**: Shrinks artwork to a maximum size but never upscales, so nothing is blurred
- **Alpha Preservation**: Writes 8-bit RGBA PNGs with transparency intact
- **Resilient**: Reports and skips any file it cannot process, so one bad input never aborts the batch

## Installation

IconHelper is not published as a NuGet package. Build it from source.

### Build from source

```bash
git clone https://github.com/ktsu-dev/IconHelper.git
cd IconHelper
dotnet build -c Release
```

### Run directly

```bash
dotnet run --project IconHelper/IconHelper.csproj -- --input ./icons --output ./out
```

### Publish a standalone executable

```bash
dotnet publish IconHelper/IconHelper.csproj -c Release -o ./publish
./publish/IconHelper --input ./icons --output ./out
```

The tool targets **.NET 9**, so a matching (or newer) SDK/runtime is required.

## Usage Examples

### Basic Example

Recolour every image in `./icons` to white and write the results to `./out`:

```bash
IconHelper --input ./icons --output ./out
```

### Choosing a Colour

Colours are parsed with [`ktsu.Semantics.Color`](https://github.com/ktsu-dev/Semantics). Hex values in
`#RGB`, `#RRGGBB` and `#RRGGBBAA` form are accepted, with the leading `#` optional, along with a small
set of colour names:

```bash
# Six digit hex
IconHelper -i ./icons -o ./out -c "#FF8800"

# Three digit shorthand, equivalent to #FF8800
IconHelper -i ./icons -o ./out -c "#F80"

# Eight digit hex, with alpha
IconHelper -i ./icons -o ./out -c "#FF8800AA"

# Named colour
IconHelper -i ./icons -o ./out -c "orange"
```

The known names are `black`, `white`, `red`, `green`, `blue`, `yellow`, `cyan`, `magenta`, `gray`,
`grey`, `orange`, `purple` and `transparent`, matched case insensitively. Any other colour must be
given as hex. An unrecognised value is rejected by validation with the list of names, rather than
being silently misread.

### Setting a Maximum Size

Icons larger than 64x64 are scaled down to fit. Smaller icons are left at their natural size:

```bash
IconHelper -i ./icons -o ./out -s 64
```

### Adding Padding

Inset the artwork by 8 pixels on each side. The output canvas stays the same size, and the artwork
inside it is scaled down to make room:

```bash
IconHelper -i ./icons -o ./out -s 128 -p 8
```

### Full Example

```bash
IconHelper --input ./raw-icons --output ./themed-icons --color "#E0E0E0" --size 96 --padding 6
```

Output while running:

```
Processing ./raw-icons/save.png...
Processing ./raw-icons/open.png...
Processing ./raw-icons/notes.txt...
Failed to process ./raw-icons/notes.txt: UnknownImageFormatException: Image cannot be loaded...
Done. 2 file(s) written, 1 failed.
```

## How It Works

Each file is processed through the following pipeline:

1. **Load** the image as RGBA32. Any file that cannot be read or decoded is reported and skipped.
2. **Desaturate** using ImageSharp's black-and-white filter, reducing the image to a silhouette.
3. **Measure** the brightest opaque pixel. If every opaque pixel is black, the silhouette is treated
   as fully opaque instead. This is what allows solid black glyphs to be recoloured.
4. **Tint** each pixel by scaling the requested colour by that pixel's normalized brightness.
   Fully transparent pixels are zeroed so they do not bleed colour into the edges.
5. **Trim** to the bounding box of the non-transparent pixels.
6. **Square** the result by padding the shorter axis with transparency.
7. **Resize** to `min(trimmedSize, --size)`, then inset by `--padding` pixels per side and pad back
   out to the final canvas size.
8. **Save** as an 8-bit RGBA PNG in the output directory, reusing the input file's base name with a
   `.png` extension. The output directory is created if it does not already exist.

An image with no visible pixels has no artwork to measure, so steps 5 to 7 are skipped and a fully
transparent square is written instead, sized by the same downscale-only rule applied to the source
canvas.

Files whose names contain `.new.png` are skipped, so re-running the tool over a directory that
already contains its own output will not reprocess those files.

## Command-Line Reference

| Short | Long | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `-i` | `--input` | Yes | n/a | Path to the directory containing the input files |
| `-o` | `--output` | Yes | n/a | Path to the directory where modified files are written |
| `-c` | `--color` | No | `#FFFFFF` | The colour to tint the icon with, as hex or a known name |
| `-s` | `--size` | No | `128` | The maximum size, in pixels, of the output icon |
| `-p` | `--padding` | No | `0` | Pixels of padding per side. Must be less than `size / 2`. Does not change the output dimensions |

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Every file was processed successfully. Also returned for `--help` and `--version` |
| `1` | The arguments were unusable, for example an `--input` directory that does not exist, an unrecognised `--color`, or `padding >= size / 2` |
| `2` | The batch ran to completion but at least one file could not be processed |

Code `2` means the run finished and the remaining icons were still written. Check the summary line
for how many succeeded.

## Notes and Limitations

- Output is always PNG, and the extension is rewritten to match, so `logo.jpg` becomes `logo.png`. If
  the input directory holds two files with the same base name but different extensions, the later one
  overwrites the earlier.
- Input formats are whatever ImageSharp can decode (PNG, JPEG, BMP, GIF, TGA, TIFF, WebP, PBM, QOI).
  Vector formats such as SVG are not supported.
- The tool only ever shrinks artwork. Passing a `--size` larger than the source icon leaves it at its
  original size.
- Colour information in the source is discarded, so every icon becomes a single-colour silhouette.
- Every failure is reported and skipped, so the run always continues to the end and exits with
  code `2` if anything failed.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
