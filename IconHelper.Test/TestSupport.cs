// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper.Test;

using System;
using System.Globalization;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// A scratch directory that deletes itself when the test finishes.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
	internal string FullPath { get; }

	internal TempDirectory()
	{
		FullPath = Path.Combine(Path.GetTempPath(), "ktsu.IconHelper.Test", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(FullPath);
	}

	internal string Combine(string relativePath) => Path.Combine(FullPath, relativePath);

	/// <summary>Returns a path inside this directory without creating the directory itself.</summary>
	internal string UncreatedSubdirectory(string name) => Path.Combine(FullPath, name);

	public void Dispose()
	{
		try
		{
			Directory.Delete(FullPath, recursive: true);
		}
		catch (IOException)
		{
			// A cleanup failure must never mask the assertion result of a test.
		}
		catch (UnauthorizedAccessException)
		{
			// As above.
		}
	}
}

internal static class TestImages
{
	internal static readonly Rgba32 Transparent = new(0, 0, 0, 0);

	/// <summary>Creates a fully transparent canvas.</summary>
	internal static Image<Rgba32> Blank(int width, int height) => new(width, height, Transparent);

	/// <summary>Draws an axis-aligned filled rectangle, overwriting whatever is underneath.</summary>
	internal static void FillRect(Image<Rgba32> image, int x, int y, int width, int height, Rgba32 colour)
	{
		for (int row = y; row < y + height; row++)
		{
			for (int column = x; column < x + width; column++)
			{
				image[column, row] = colour;
			}
		}
	}

	/// <summary>
	/// Parses a <c>#RRGGBB</c> string. Deliberately local rather than using
	/// <see cref="System.Drawing.ColorTranslator"/> so the tests do not depend on that type
	/// being available on every platform.
	/// </summary>
	internal static System.Drawing.Color ParseHexColour(string hex)
	{
		ReadOnlySpan<char> trimmed = hex.AsSpan().TrimStart('#');
		int r = int.Parse(trimmed[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		int g = int.Parse(trimmed.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		int b = int.Parse(trimmed.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return System.Drawing.Color.FromArgb(r, g, b);
	}

	/// <summary>Finds the brightest opaque pixel, measured by the sum of its colour channels.</summary>
	internal static Rgba32 BrightestOpaquePixel(Image<Rgba32> image)
	{
		Rgba32 brightest = Transparent;
		int best = -1;
		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				Rgba32 pixel = image[x, y];
				if (pixel.A == 0)
				{
					continue;
				}

				int score = pixel.R + pixel.G + pixel.B;
				if (score > best)
				{
					best = score;
					brightest = pixel;
				}
			}
		}

		return brightest;
	}
}

internal static class ImageAssert
{
	/// <summary>
	/// Asserts two images are identical in size and in every RGBA channel of every pixel.
	/// Reports the first few differing pixels so a gold master failure is diagnosable.
	/// </summary>
	internal static void PixelsAreEqual(Image<Rgba32> expected, Image<Rgba32> actual, string context)
	{
		Assert.AreEqual(expected.Width, actual.Width, $"{context}: image width differs.");
		Assert.AreEqual(expected.Height, actual.Height, $"{context}: image height differs.");

		int differences = 0;
		System.Text.StringBuilder samples = new();
		for (int y = 0; y < expected.Height; y++)
		{
			for (int x = 0; x < expected.Width; x++)
			{
				Rgba32 want = expected[x, y];
				Rgba32 got = actual[x, y];
				if (want.Equals(got))
				{
					continue;
				}

				differences++;
				if (differences <= 5)
				{
					samples.Append(CultureInfo.InvariantCulture, $"\n\t({x},{y}) expected rgba({want.R},{want.G},{want.B},{want.A}) but was rgba({got.R},{got.G},{got.B},{got.A})");
				}
			}
		}

		Assert.AreEqual(0, differences, $"{context}: {differences} pixel(s) differ from the gold master.{samples}");
	}
}
