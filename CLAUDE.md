# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore, build and test
dotnet restore
dotnet build
dotnet test

# Build release
dotnet build -c Release

# Run a single test
dotnet test --filter "FullyQualifiedName~OutputMatchesTheGoldMaster"

# Run the tool against a directory of icons
dotnet run --project IconHelper/IconHelper.csproj -- --input ./icons --output ./out

# Publish a standalone executable
dotnet publish IconHelper/IconHelper.csproj -c Release -o ./publish
```

## Project Structure

This is a .NET **console application** (`IconHelper`), not a library, and it is **not published to
NuGet**. It batch-processes icon images: recolouring them to a single-colour silhouette, trimming
transparent margins, squaring the canvas, and resizing to a maximum dimension.

The solution uses:

- **ktsu.Sdk** + **ktsu.Sdk.App** - Custom SDKs providing shared build configuration for applications
- Single target framework: `net9.0` (note `TargetFrameworks` is explicitly blanked in the csproj to
  override the SDK's default multi-targeting)
- Central package management via `Directory.Packages.props`

### Key Files

- `IconHelper/IconHelper.cs` - `Main`, `Run` (CLI wiring), `ProcessDirectory` (file I/O loop) and
  `ProcessImage` (the pixel pipeline). The latter two are `internal` specifically so the tests can
  drive them.
- `IconHelper/Arguments.cs` - CommandLineParser options class and its `Validate` method
- `IconHelper/BatchResult.cs` - the written and failed counts returned by `ProcessDirectory`
- `IconHelper/AssemblyInfo.cs` - `InternalsVisibleTo("ktsu.IconHelper.Test")`
- `IconHelper.Test/` - MSTest suite, including the gold master fixtures
- `Directory.Packages.props` - Central package versions
- `global.json` - Pins the .NET SDK, ktsu SDK versions, and the test runner

### Dependencies

- **SixLabors.ImageSharp** - All image decoding, pixel manipulation, and PNG encoding
- **CommandLineParser** - Attribute-driven CLI argument parsing
- **ktsu.Extensions** - Used for `ToCollection()` on the file enumeration
- **ktsu.Semantics.Paths** - `AbsoluteDirectoryPath`, `AbsoluteFilePath` and `FileName` for the input
  and output directories, replacing raw strings and `Path.Join`
- **ktsu.Semantics.Color** - colour parsing, replacing `System.Drawing.ColorTranslator`
- **ktsu.Semantics.Strings** - referenced directly only because `SemanticString.Create`, which the
  path types inherit, lives there and the ktsu analyzer requires direct references
- **Polyfill** - Backfills newer BCL APIs. Pinned centrally at 11.0.1, which is the floor
  ktsu.Semantics.Strings requires

## Architecture

The program is a single-pass batch processor with no abstraction layers, which is appropriate for its size.
`Main` hands parsing to `CommandLineParser`, and `Run` does the work:

```
Parse args → Validate → enumerate input dir → per file:
  Load<Rgba32> → BlackWhite() → find max opaque luminance → tint by colour
  → crop to alpha bounding box → pad to square → resize → pad to final size → SaveAsPng
```

The recolouring algorithm is documented step-by-step in inline comments in `IconHelper.cs`. Read
those before changing the pixel maths. Two details in particular:

- **Two `ProcessPixelRows` passes.** The first finds the brightest opaque pixel (`maxValue`). The
  second applies the tint *and* accumulates the alpha bounding box. They cannot be merged, because
  the tint depends on `maxValue` being known up front.
- **The all-black special case.** If `maxValue == 0` every opaque pixel is treated as full intensity.
  Without this, solid black glyphs would tint to black and appear blank.

Sizing is deliberately downscale-only: `finalSize = Math.Min(trimmedSquareSize, args.Size)`. Padding
is applied by shrinking the *content* (`finalSize - padding * 2`) and padding back out, so the output
canvas is always `finalSize` square regardless of padding.

Files containing `.new.png` in their name are skipped, so re-running over an output directory is safe.

An image with no opaque pixels has no bounding box to measure, so `ProcessImage` short-circuits and
emits a fully transparent square sized by the downscale-only rule against the source canvas.

`ProcessDirectory` catches **all** exceptions per file, reports them with the exception type name and
carries on, returning a `BatchResult` of written and failed counts. The broad catch is deliberate and
carries a targeted `SuppressMessage` for CA1031 explaining why. Do not narrow it back to specific
ImageSharp types, that is exactly what used to let a locked or malformed file kill an entire run.

### Semantic types

Paths and colours are semantic types rather than strings.

- `Arguments.TryResolveInput` and `TryResolveOutput` turn the raw option strings into
  `AbsoluteDirectoryPath`, resolving relative values against the working directory first because the
  semantic type only accepts absolute paths. `Validate` calls both, so a missing input directory is a
  clean exit 1 instead of the unhandled `DirectoryNotFoundException` it used to be.
- Output file paths are composed with the `/` operator, `outputDirectory / FileName.Create(...)`,
  which also rejects a file name carrying a directory separator.
- Semantic strings define an implicit conversion to `string`, so pass them straight to BCL APIs
  rather than calling `ToString()`.
- `ColorParser.TryParse` accepts a `NamedColors` name or a hex value. `Color` stores **linear**
  channels as doubles, so `ProcessImage` calls `ToBytes()` once up front rather than per pixel.
  `FromHex(...).ToBytes()` round-trips byte for byte, which is why swapping the parser left every
  gold master unchanged.

The named colour set is 13 entries, where `System.Drawing.ColorTranslator` understood roughly 140 CSS
names. That narrowing is deliberate and pinned by `ColorParserTests.RejectsCssNamesOutsideTheKnownSet`.

### Exit codes

`ExitSuccess` (0), `ExitInvalidArguments` (1) and `ExitSomeFilesFailed` (2) are constants on
`IconHelper`. Two pure helpers decide them so the logic is testable without spawning a process:

- `ExitCodeFor(BatchResult)` maps a finished batch, non-zero `Failed` gives 2
- `ExitCodeForParseErrors(IEnumerable<ErrorType>)` maps CommandLineParser failures. `--help` and
  `--version` arrive here as errors but must still exit 0, which is the case that helper exists for

`Main` wires the second one through `WithNotParsed`. Without it a missing required option printed the
help text and exited 0.

### Known Rough Edges

Do not "fix" these silently, they are documented in the README as limitations:

- Output extensions are rewritten to `.png`, so two inputs sharing a base name (`a.png`, `a.jpg`)
  collide and the later one wins.
- A run that fails every single file still exits `2`, the same as a run that failed one file. The
  exit status says "something failed", the summary line says how much.

### Fixed Bugs Worth Knowing About

Both are covered by regression tests. Do not reintroduce them.

- **Bounding-box off-by-one.** The crop used `right - left`, but `right` and `bottom` are *inclusive*
  indices of the last opaque pixel, so the span needs `+ 1`. Every icon used to lose its rightmost
  column and bottom row. Pinned by `ProcessImageTests.CropsExactlyTheArtworkBoundingBox` and
  `KeepsTheOutermostColumnAndRowOfTheArtwork`.
- **Fully transparent input crashed the batch.** With nothing opaque to measure, the bounds stay
  inverted and the computed crop width went negative, throwing `ArgumentOutOfRangeException`. That is
  not one of the three caught types, so one blank file aborted the whole run. `ProcessImage` now
  detects the inverted bounds and emits an empty square instead. Pinned by
  `ProcessImageTests.ProducesATransparentSquareWhenTheArtworkHasNoOpaquePixels` and
  `ProcessDirectoryTests.AFullyTransparentFileDoesNotStopTheBatch`.

## Testing

`IconHelper.Test` is an MSTest project (`MSTest.Sdk`, Microsoft Testing Platform) targeting `net10.0`
while the app targets `net9.0`. It reaches the app's `internal` members via `InternalsVisibleTo`.

- `ArgumentsTests` - option defaults and the padding-versus-size validation rule
- `ProcessImageTests` - the pixel pipeline in isolation: squaring, downscale-only clamping, trimming,
  tinting, the all-black branch, midtone normalization, colour flattening, padding and the blank
  image case
- `ProcessDirectoryTests` - the I/O layer: output directory creation, `.png` extension rewriting,
  `.new.png` skipping, per-file error recovery for both decode failures and locked files, the
  written and failed counts, and the PNG encoder settings
- `GoldMasterTests` - characterization tests

### Gold master

`IconHelper.Test/GoldMaster/` holds committed input fixtures and the expected output for a matrix of
`(input, colour, size, padding)` cases. Each case runs the real `ProcessDirectory` path and compares
the result pixel-for-pixel against the committed expectation.

These lock in *current* behaviour. Any deliberate change to the pipeline is expected to break them.
To update:

```powershell
./IconHelper.Test/GoldMaster/regenerate.ps1
```

Then **review the image diff before committing**. Regenerating without looking defeats the purpose.
Keep the case list in `regenerate.ps1` in sync with `GoldMasterTests.Cases`. Two guard tests fail if
a fixture or expectation is left unreferenced by either side.

## CI/CD

GitHub Actions via `.github/workflows/dotnet.yml`. The pipeline clones the
[KtsuBuild](https://github.com/ktsu-dev/KtsuBuild) repository at its latest tag and runs
`KtsuBuild.CLI ci`. This repo does **not** contain a local `scripts/PSBuild.psm1`. The workflow also
runs SonarQube Cloud analysis (when `SONAR_TOKEN` is set), generates winget manifests on release, and
submits a dependency graph for security scanning.

Version increments are controlled by commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

Do not manually edit the auto-generated files: `VERSION.md`, `CHANGELOG.md`, `LATEST_CHANGELOG.md`,
`LICENSE.md`, `AUTHORS.md`.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with justifications
when needed, with preprocessor defines only as fallback. Make the smallest, most targeted
suppressions possible.

The existing suppression in `Arguments.cs` is the pattern to follow, a narrowly scoped
`#pragma warning disable CA1812` with an inline comment explaining that CommandLineParser
instantiates the class reflectively.
