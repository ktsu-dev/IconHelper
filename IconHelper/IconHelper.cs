// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using CommandLine;

using ktsu.Extensions;
using ktsu.Semantics.Paths;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Color = ktsu.Semantics.Color.Color;

internal static class IconHelper
{
	/// <summary>
	/// The encoder settings every output file is written with. Shared so that the gold master
	/// tests assert against the same settings the tool actually ships.
	/// </summary>
	internal static PngEncoder Encoder { get; } = new()
	{
		BitDepth = PngBitDepth.Bit8,
		ColorType = PngColorType.RgbWithAlpha,
		TransparentColorMode = PngTransparentColorMode.Clear,
	};

	private static void Main(string[] args)
		=> Parser.Default.ParseArguments<Arguments>(args)
			.WithParsed(Run)
			.WithNotParsed(errors => Environment.Exit(ExitCodeForParseErrors(errors.Select(e => e.Tag))));

	/// <summary>Every file was processed successfully.</summary>
	internal const int ExitSuccess = 0;

	/// <summary>The supplied arguments did not validate.</summary>
	internal const int ExitInvalidArguments = 1;

	/// <summary>The batch ran to completion but at least one file could not be processed.</summary>
	internal const int ExitSomeFilesFailed = 2;

	/// <summary>
	/// Maps the outcome of a batch to a process exit code. A partial run is distinguished from bad
	/// arguments so a caller can tell "you asked for something impossible" from "some icons failed".
	/// </summary>
	internal static int ExitCodeFor(BatchResult result)
		=> result.Failed > 0 ? ExitSomeFilesFailed : ExitSuccess;

	/// <summary>
	/// Maps command line parse failures to an exit code. Asking for help or the version is a
	/// successful outcome, anything else means the arguments were unusable.
	/// </summary>
	internal static int ExitCodeForParseErrors(IEnumerable<ErrorType> errorTypes)
		=> errorTypes.All(t => t is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError)
			? ExitSuccess
			: ExitInvalidArguments;

	private static void Run(Arguments args)
	{
		if (!args.Validate(out System.Collections.ObjectModel.Collection<string>? errors))
		{
			Console.WriteLine($"Argument validation failed:\n\t{string.Join("\n\t", errors)}");
			Environment.Exit(ExitInvalidArguments);
		}

		// Validate already proved this parses, so the result is not worth re-checking.
		_ = ColorParser.TryParse(args.Color, out Color color);
		BatchResult result = ProcessDirectory(args, color);

		Console.WriteLine(result.Failed == 0
			? $"Done. {result.Written} file(s) written."
			: $"Done. {result.Written} file(s) written, {result.Failed} failed.");

		int exitCode = ExitCodeFor(result);
		if (exitCode != ExitSuccess)
		{
			Environment.Exit(exitCode);
		}
	}

	/// <summary>
	/// Processes every file in the input directory, writing recoloured PNGs to the output directory.
	/// A failure on one file is reported and skipped so that a single bad input cannot abort the
	/// batch, so callers should check <see cref="BatchResult.Failed"/> rather than assuming success.
	/// </summary>
	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "This is a batch tool. Any failure on one file must be reported and skipped rather than abandoning the remaining files, and the failure is surfaced to the console and in the returned BatchResult.")]
	internal static BatchResult ProcessDirectory(Arguments args, Color color)
	{
		Ensure.NotNull(args);

		// Validate resolves these too, so a failure here means the caller skipped validation.
		if (!args.TryResolveInput(out AbsoluteDirectoryPath? inputDirectory, out string? inputError))
		{
			throw new ArgumentException(inputError, nameof(args));
		}

		if (!args.TryResolveOutput(out AbsoluteDirectoryPath? outputDirectory, out string? outputError))
		{
			throw new ArgumentException(outputError, nameof(args));
		}

		Directory.CreateDirectory(outputDirectory);

		int processed = 0;
		int failed = 0;
		System.Collections.ObjectModel.Collection<string> files = Directory.GetFiles(inputDirectory, "*").ToCollection();
		foreach (string? file in files)
		{
			if (file.Contains(".new.png"))
			{
				continue;
			}

			try
			{
				Console.WriteLine($"Processing {file}...");
				using Image<Rgba32> image = Image.Load<Rgba32>(file);

				ProcessImage(image, color, args.Size, args.Padding);

				// Always write a .png extension, since the encoder always writes PNG data. FileName
				// rejects anything carrying a directory separator, and the / operator composes the
				// two into an absolute file path.
				FileName outputFileName = FileName.Create<FileName>($"{Path.GetFileNameWithoutExtension(file)}.png");
				AbsoluteFilePath outputFilePath = outputDirectory / outputFileName;

				image.SaveAsPng(outputFilePath, Encoder);
				processed++;
			}
			catch (Exception e)
			{
				// Deliberately broad. Undecodable files throw one of a handful of ImageSharp types,
				// but locked files, permission problems and malformed images that trip an assertion
				// deeper in the decoder all surface as something else. Any of those should cost one
				// icon, not the whole run, so the type name is included to keep it diagnosable.
				Console.WriteLine($"Failed to process {file}: {e.GetType().Name}: {e.Message}");
				failed++;
			}
		}

		return new BatchResult(processed, failed);
	}

	/// <summary>
	/// Recolours an icon to a flat silhouette in the target colour, trims its transparent margins,
	/// centres it on a square canvas and scales it down to at most <paramref name="size"/> pixels.
	/// The image is mutated in place.
	/// </summary>
	internal static void ProcessImage(Image<Rgba32> image, Color color, int size, int padding)
	{
		Ensure.NotNull(image);

		// The semantic Color stores linear channels as doubles. Encode to sRGB bytes once here rather
		// than per pixel, both for speed and so the tint below stays plain byte arithmetic.
		(byte colorR, byte colorG, byte colorB, byte _) = color.ToBytes();

		// RECOLOURING ALGORITHM
		//
		// Turns an arbitrary icon into a flat silhouette painted in a single target colour,
		// while keeping the anti-aliased edges smooth: flatten to greyscale, normalize the
		// brightness, then multiply through by the colour.
		//
		// Flatten to greyscale. ImageSharp's BlackWhite filter is a colour matrix
		// (KnownFilterMatrices.BlackWhiteFilter) whose red, green and blue rows are all
		// 1.5 with a -1 offset row, and whose alpha row is left at 1. Working in the
		// normalized 0-1 space that means every output channel becomes the same value:
		//
		//     out = clamp01(1.5 * (R + G + B) - 1)          alpha passed through unchanged
		//
		// Because all three outputs are identical the image collapses to a single tonal
		// channel, which is why every read below samples the red channel alone as "the"
		// intensity.
		//
		// For an already-grey pixel of value v this reduces to 4.5v - 1, a steep ramp that
		// clamps to black at v <= 2/9 (~0.222) and to white at v >= 4/9 (~0.444). The output
		// is therefore near-binary: most pixels land on pure black or pure white, with only a
		// narrow band of true midtones along anti-aliased edges. Those midtones are exactly
		// what the normalization and tint below preserve, and they are the reason maxValue is
		// already 255 for most real icons.
		//
		// Note the filter has no alpha awareness beyond passing alpha through, so it also
		// rewrites the colour channels of fully transparent pixels (see the maximum
		// calculation below), which is why it ignores them.
		image.Mutate(x => x.BlackWhite());

		byte maxValue = FindBrightestOpaqueValue(image);

		// Handle the all-black glyph case. A maxValue of 0 means every opaque pixel is pure
		// black, a solid silhouette carrying its shape entirely in the alpha channel.
		// The isBlack flag forces those pixels to full intensity in the pass below so the
		// glyph takes the target colour. Without it the normalization would resolve to
		// intensity 0 and the icon would come out invisible.
		bool isBlack = maxValue == 0;

		PixelBounds bounds = TintAndMeasureBounds(image, maxValue, isBlack, colorR, colorG, colorB);

		if (bounds.IsEmpty)
		{
			// No artwork to crop around, so emit an empty square rather than trying to measure one.
			// The side comes from the source canvas so the downscale-only rule still applies, and
			// every pixel is already rgba(0,0,0,0) by now, so resizing keeps it fully transparent.
			int blankSize = Math.Min(Math.Max(image.Width, image.Height), size);
			image.Mutate(x => x.Resize(blankSize, blankSize));
			return;
		}

		CropSquareAndResize(image, bounds, size, padding);
	}

	/// <summary>
	/// Finds the highest tonal value among the *opaque* pixels. Transparent pixels are excluded
	/// because their colour channels are meaningless. Many encoders leave arbitrary garbage in the
	/// RGB of a fully transparent pixel, which would otherwise skew this maximum.
	/// </summary>
	private static byte FindBrightestOpaqueValue(Image<Rgba32> image)
	{
		byte maxValue = 0;

		image.ProcessPixelRows(accessor =>
		{
			for (int y = 0; y < accessor.Height; y++)
			{
				Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

				for (int x = 0; x < pixelRow.Length; x++)
				{
					ref Rgba32 pixel = ref pixelRow[x];
					if (pixel.A != 0)
					{
						maxValue = Math.Max(maxValue, pixel.R);
					}
				}
			}
		});

		return maxValue;
	}

	/// <summary>
	/// Normalizes the brightness and multiplies through by the target colour, returning the bounding
	/// box of the visible artwork. The two are done in one pass because it is already walking every
	/// pixel, and the crop needs those bounds to trim the transparent margins.
	/// </summary>
	private static PixelBounds TintAndMeasureBounds(
		Image<Rgba32> image,
		byte maxValue,
		bool isBlack,
		byte colorR,
		byte colorG,
		byte colorB)
	{
		// Seeded inverted, so an image with nothing opaque in it leaves them that way and reports
		// itself as empty.
		int top = image.Height;
		int left = image.Width;
		int right = 0;
		int bottom = 0;

		image.ProcessPixelRows(accessor =>
		{
			for (int y = 0; y < accessor.Height; y++)
			{
				Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

				for (int x = 0; x < pixelRow.Length; x++)
				{
					ref Rgba32 pixel = ref pixelRow[x];

					// Normalize by *offset*, not by scale: adding (255 - maxValue) to every
					// pixel lifts the brightest opaque pixel to exactly 255 while preserving
					// the absolute differences between tones, so anti-aliased edges keep
					// their gradient instead of being stretched apart. A source whose
					// brightest pixel is already 255 passes through unchanged.
					byte newValue = (byte)(isBlack ? 255 : 255 - (maxValue - pixel.R));
					if (pixel.A != 0)
					{
						left = Math.Min(left, x);
						top = Math.Min(top, y);
						right = Math.Max(right, x);
						bottom = Math.Max(bottom, y);
					}
					else
					{
						// Zero the colour of fully transparent pixels. Without this they keep
						// whatever RGB the decoder left behind, and the Resize below blends
						// that hidden colour into neighbouring pixels, producing a dark or
						// off-colour halo around the icon. This also discards the overflowed
						// newValue computed above for transparent pixels whose R exceeded
						// maxValue (which was sampled from opaque pixels only).
						newValue = 0;
					}

					// Multiply the target colour by the normalized intensity. Intensity 255
					// yields the colour exactly, intermediate values yield proportionally
					// darker shades of it, which is what keeps edges anti-aliased. Alpha is
					// deliberately left alone so the original transparency is preserved.
					pixel.R = (byte)(newValue / 255f * colorR);
					pixel.G = (byte)(newValue / 255f * colorG);
					pixel.B = (byte)(newValue / 255f * colorB);
				}
			}
		});

		return new PixelBounds(left, top, right, bottom);
	}

	/// <summary>
	/// Crops to the artwork, squares it off, and scales it down to at most
	/// <paramref name="size"/> pixels, insetting the content by <paramref name="padding"/> per side
	/// without changing the final canvas size.
	/// </summary>
	private static void CropSquareAndResize(Image<Rgba32> image, PixelBounds bounds, int size, int padding)
	{
		int newSize = Math.Max(bounds.Width, bounds.Height);

		// We intentionally only shrink the image and not grow it
		int finalSize = Math.Min(newSize, size);
		int finalContentSize = finalSize - (padding * 2);
		Rgba32 paddingColor = Rgba32.ParseHex("00000000");

		image.Mutate(x => x
			.Crop(new()
			{
				Width = bounds.Width,
				Height = bounds.Height,
				X = bounds.Left,
				Y = bounds.Top,
			})
			.Pad(newSize, newSize, paddingColor)
			.Resize(finalContentSize, finalContentSize)
			.Pad(finalSize, finalSize, paddingColor));
	}
}
