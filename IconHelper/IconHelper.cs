// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper;

using System.Drawing;
using System.IO;

using CommandLine;

using ktsu.Extensions;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using PointF = SixLabors.ImageSharp.PointF;

internal static class IconHelper
{
	private static void Main(string[] args)
		=> Parser.Default.ParseArguments<Arguments>(args).WithParsed(Run);

	private static void Run(Arguments args)
	{
		if (!args.Validate(out var errors))
		{
			Console.WriteLine($"Argument validation failed:\n\t{string.Join("\n\t", errors)}");
			Environment.Exit(1);
		}

		var color = ColorTranslator.FromHtml(args.Color);
		var files = Directory.GetFiles(args.InputPath, "*").ToCollection();
		foreach (var file in files)
		{
			if (file.Contains(".new.png"))
			{
				continue;
			}

			try
			{
				Console.WriteLine($"Processing {file}...");
				var image = Image.Load<Rgba32>(file);

				var top = image.Height;
				var left = image.Width;
				var right = 0;
				var bottom = 0;

				image.Mutate(x => x.BlackWhite());

				//find the highest tonal value
				byte maxValue = 0;

				image.ProcessPixelRows(accessor =>
				{
					for (var y = 0; y < accessor.Height; y++)
					{
						var pixelRow = accessor.GetRowSpan(y);

						for (var x = 0; x < pixelRow.Length; x++)
						{
							ref var pixel = ref pixelRow[x];
							if (pixel.A != 0)
							{
								maxValue = Math.Max(maxValue, pixel.R);
							}
						}
					}
				});

				var isBlack = maxValue == 0;
				maxValue = isBlack ? (byte)255 : maxValue;

				image.ProcessPixelRows(accessor =>
				{
					for (var y = 0; y < accessor.Height; y++)
					{
						var pixelRow = accessor.GetRowSpan(y);

						for (var x = 0; x < pixelRow.Length; x++)
						{
							ref var pixel = ref pixelRow[x];
							var newValue = (byte)(isBlack ? 255 : 255 - (maxValue - pixel.R));
							if (pixel.A != 0)
							{
								left = Math.Min(left, x);
								top = Math.Min(top, y);
								right = Math.Max(right, x);
								bottom = Math.Max(bottom, y);
							}
							else
							{
								newValue = 0;
							}

							pixel.R = (byte)(newValue / 255f * color.R);
							pixel.G = (byte)(newValue / 255f * color.G);
							pixel.B = (byte)(newValue / 255f * color.B);
						}
					}
				});

				var minWidth = right - left;
				var minHeight = bottom - top;
				var newSize = Math.Max(minWidth, minHeight);
				var center = new PointF(left + (minWidth / 2f), top + (minHeight / 2f));

				// We intentionally only shrink the image and not grow it
				var finalSize = Math.Min(newSize, args.Size);
				var finalContentSize = finalSize - (args.Padding * 2);
				var paddingColor = Rgba32.ParseHex("00000000");

				image.Mutate(x => x
					.Crop(new()
					{
						Width = minWidth,
						Height = minHeight,
						X = left,
						Y = top,
					})
					.Pad(newSize, newSize, paddingColor)
					.Resize(finalContentSize, finalContentSize)
					.Pad(finalSize, finalSize, paddingColor));

				var outputFilePath = Path.Join(args.OutputPath, Path.GetFileName(file));

				image.SaveAsPng(outputFilePath, new()
				{
					BitDepth = PngBitDepth.Bit8,
					ColorType = PngColorType.RgbWithAlpha,
					TransparentColorMode = PngTransparentColorMode.Clear,
				});
			}
			catch (ImageProcessingException e)
			{
				Console.WriteLine($"Failed to process {file}: {e.Message}");
				continue;
			}
			catch (InvalidImageContentException e)
			{
				Console.WriteLine($"Failed to process {file}: {e.Message}");
				continue;
			}
			catch (UnknownImageFormatException e)
			{
				Console.WriteLine($"Failed to process {file}: {e.Message}");
				continue;
			}
		}

		Console.WriteLine($"Done");
	}
}
