# ktsu.IconHelper

> A .NET command-line tool that batch-normalizes icon images by recoloring, trimming, squaring, and resizing them into consistent PNGs.

[![License](https://img.shields.io/github/license/ktsu-dev/IconHelper.svg?label=License&logo=nuget)](LICENSE.md)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/IconHelper?label=Commits&logo=github)](https://github.com/ktsu-dev/IconHelper/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/IconHelper?label=Contributors&logo=github)](https://github.com/ktsu-dev/IconHelper/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/IconHelper/dotnet.yml?branch=main&label=Build&logo=github)](https://github.com/ktsu-dev/IconHelper/actions)

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

The whole design follows from one goal: reduce artwork of unknown origin to a single-colour
silhouette without destroying the anti-aliased edges that make an icon look smooth at small sizes.
A naive approach, thresholding to pure black and white and painting the result, produces jagged
icons. Each stage below exists to avoid that.

### 1. Flatten to a single tonal channel

The image is loaded as RGBA32 and put through ImageSharp's `BlackWhite` filter. That filter is a
colour matrix whose red, green and blue rows are all `1.5`, with a `-1` offset row and the alpha row
left at `1`. In normalized 0 to 1 terms every output channel becomes the same value:

```
out = clamp01(1.5 * (R + G + B) - 1)
```

Because all three outputs are identical the image collapses to one tonal channel, which is why every
later step reads the red channel alone and treats it as intensity.

For a pixel that is already grey with value `v` this reduces to `4.5v - 1`, a steep ramp that clamps
to black at `v <= 2/9` and to white at `v >= 4/9`. The result is deliberately *near* binary rather
than binary: most pixels land on pure black or pure white, and only a narrow band along
anti-aliased edges keeps genuine midtones. **Those midtones are the anti-aliasing**, and preserving
them is the reason for everything that follows.

Alpha passes through untouched.

### 2. Measure the brightest opaque pixel

A first pass records the highest tonal value across pixels whose alpha is not zero.

Transparent pixels are excluded deliberately. The colour matrix has no alpha awareness, so it
rewrites the colour channels of fully transparent pixels too, and many encoders leave arbitrary
values in the RGB of a transparent pixel to begin with. Including them would skew the maximum
against artwork that is mostly empty canvas, which most icons are.

This has to be a separate pass, because the tint in stage 3 cannot start until the maximum for the
whole image is known.

### 3. Normalize, then tint

A second pass lifts each pixel to full intensity and multiplies through by the target colour:

```
intensity = 255 - (maxValue - red)        // opaque pixels
intensity = 0                             // transparent pixels
channel   = intensity / 255 * targetChannel
```

The normalization is an **offset rather than a scale**, and that choice matters. Adding
`255 - maxValue` to every pixel raises the brightest opaque pixel to exactly 255 while preserving
the absolute differences between neighbouring tones. Scaling instead would stretch those differences
apart and visibly harden the anti-aliased edge. Artwork whose brightest pixel is already 255, which
is most of it after stage 1, passes through unchanged.

Two details are load bearing:

- **All-black artwork is special-cased.** If the brightest opaque pixel is still 0, the glyph is a
  solid black silhouette carrying its shape entirely in the alpha channel. Those pixels are forced
  to full intensity, because normalizing them would resolve to intensity 0 and the icon would come
  out invisible.
- **Transparent pixels have their colour zeroed.** Whatever RGB the decoder left behind would
  otherwise be blended outward by the resize in stage 5, producing a dark or off-colour halo around
  the icon.

Alpha is never modified, so the original transparency survives to the output.

### 4. Trim and square

The same pass that tints also accumulates the bounding box of the non-transparent pixels, since it
is already visiting every pixel. The image is cropped to that box, which discards whatever empty
margin the source had, then padded with transparency on the shorter axis to make it square. Padding
rather than stretching keeps the artwork's aspect ratio intact and centres it.

The bounds are inclusive indices, so the width is `right - left + 1`. Dropping that `+ 1` costs the
rightmost column and bottom row of every icon.

### 5. Resize, pad, and save

The final side is `min(squareSize, --size)`. The minimum is what makes this **downscale only**:
enlarging a small icon would just interpolate detail that was never there, so a `--size` larger than
the artwork leaves it alone.

`--padding` insets the content without changing the canvas. The artwork is resized to
`finalSize - padding * 2` and then padded back out to `finalSize`, so the output is always
`finalSize` square whatever the padding.

The result is written as an 8-bit RGBA PNG, with the encoder set to clear the colour channels of
fully transparent pixels so no invisible colour data is carried into the file.

### Edge cases

An image with no visible pixels has no bounding box to measure, so stages 4 and 5 are skipped and a
fully transparent square is written instead, sized by the same downscale-only rule applied to the
source canvas.

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
