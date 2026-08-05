// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper.Test;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using Color = System.Drawing.Color;

[TestClass]
public class ProcessImageTests
{
	private static readonly Rgba32 OpaqueBlack = new(0, 0, 0, 255);
	private static readonly Rgba32 OpaqueWhite = new(255, 255, 255, 255);

	[TestMethod]
	public void SquaresNonSquareArtwork()
	{
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 20, 40, 120, 40, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 128, 0);

		Assert.AreEqual(image.Width, image.Height, "Output canvas should always be square.");
	}

	[TestMethod]
	public void ClampsOutputToTheRequestedMaximumSize()
	{
		using Image<Rgba32> image = TestImages.Blank(300, 300);
		TestImages.FillRect(image, 10, 10, 250, 250, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 32, 0);

		Assert.AreEqual(32, image.Width);
		Assert.AreEqual(32, image.Height);
	}

	[TestMethod]
	public void DoesNotUpscaleArtworkSmallerThanTheRequestedSize()
	{
		using Image<Rgba32> image = TestImages.Blank(64, 64);
		TestImages.FillRect(image, 10, 10, 12, 12, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 512, 0);

		Assert.AreEqual(12, image.Width, "Artwork smaller than the requested size must not be grown.");
		Assert.AreEqual(12, image.Height);
	}

	[TestMethod]
	public void TrimsTransparentMarginsDownToTheArtwork()
	{
		using Image<Rgba32> image = TestImages.Blank(400, 400);
		TestImages.FillRect(image, 300, 12, 40, 40, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 256, 0);

		// The 400x400 canvas is mostly empty, so only the 40x40 shape should survive.
		Assert.AreEqual(40, image.Width, "Transparent margins should be cropped away.");
		Assert.AreEqual(40, image.Height);
	}

	[TestMethod]
	public void CropsExactlyTheArtworkBoundingBox()
	{
		// Regression test for an off-by-one in the crop: `right` and `bottom` are inclusive indices
		// of the last opaque pixel, so the span needs `right - left + 1`. Without it every icon lost
		// its rightmost column and bottom row, and this 30x20 shape squared to 29x29 instead of 30x30.
		using Image<Rgba32> image = TestImages.Blank(60, 60);
		TestImages.FillRect(image, 10, 10, 30, 20, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 512, 0);

		Assert.AreEqual(30, image.Width, "The crop should cover the full width of the artwork.");
		Assert.AreEqual(30, image.Height, "The shorter axis is padded up to the longer one.");
	}

	[TestMethod]
	public void ProducesATransparentSquareWhenTheArtworkHasNoOpaquePixels()
	{
		// Regression test. With nothing opaque to measure, the bounding box is never updated and
		// stays inverted, which used to produce a negative crop width and throw.
		using Image<Rgba32> image = TestImages.Blank(64, 48);

		IconHelper.ProcessImage(image, Color.White, 128, 0);

		// The requested 128 is larger than the source, so the downscale-only rule caps the side at
		// the longest edge of the source canvas.
		Assert.AreEqual(64, image.Width);
		Assert.AreEqual(64, image.Height);
		AssertFullyTransparent(image);
	}

	[TestMethod]
	public void ClampsABlankImageToTheRequestedSize()
	{
		using Image<Rgba32> image = TestImages.Blank(200, 200);

		IconHelper.ProcessImage(image, Color.White, 32, 0);

		Assert.AreEqual(32, image.Width);
		Assert.AreEqual(32, image.Height);
		AssertFullyTransparent(image);
	}

	private static void AssertFullyTransparent(Image<Rgba32> image)
	{
		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				Assert.AreEqual(0, image[x, y].A, $"Pixel ({x},{y}) should be fully transparent.");
			}
		}
	}

	[TestMethod]
	public void KeepsTheOutermostColumnAndRowOfTheArtwork()
	{
		// The off-by-one above was invisible in the dimensions of anti-aliased art but did discard
		// real content. A hard-edged square must survive with its edges intact.
		using Image<Rgba32> image = TestImages.Blank(50, 50);
		TestImages.FillRect(image, 10, 10, 20, 20, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 512, 0);

		Assert.AreEqual(20, image.Width);
		Assert.AreEqual(255, image[0, 0].A, "The first column/row of the artwork should be kept.");
		Assert.AreEqual(255, image[19, 19].A, "The last column/row of the artwork should be kept.");
	}

	[TestMethod]
	public void PaintsOpaqueArtworkInTheTargetColour()
	{
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 20, 20, 40, 40, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.FromArgb(0, 128, 255), 40, 0);

		Rgba32 centre = image[20, 20];
		Assert.AreEqual(0, centre.R);
		Assert.AreEqual(128, centre.G);
		Assert.AreEqual(255, centre.B);
		Assert.AreEqual(255, centre.A, "Opaque source pixels stay opaque.");
	}

	[TestMethod]
	public void PaintsAllBlackArtworkInTheTargetColour()
	{
		// The isBlack branch: a solid black glyph carries its shape purely in the alpha channel,
		// and must still come out fully coloured rather than invisible.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 20, 20, 40, 40, OpaqueBlack);

		IconHelper.ProcessImage(image, Color.FromArgb(0, 255, 0), 40, 0);

		Rgba32 centre = image[20, 20];
		Assert.AreEqual(0, centre.R);
		Assert.AreEqual(255, centre.G, "An all-black glyph should take the full target colour.");
		Assert.AreEqual(0, centre.B);
		Assert.AreEqual(255, centre.A);
	}

	[TestMethod]
	public void NormalizesMidtoneArtworkUpToFullIntensity()
	{
		// A mid-grey of 80 lands inside the BlackWhite ramp, so maxValue ends up strictly
		// between 0 and 255 and the offset normalization has to lift it back to full intensity.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 20, 20, 40, 40, new Rgba32(80, 80, 80, 255));

		IconHelper.ProcessImage(image, Color.FromArgb(200, 100, 50), 40, 0);

		Rgba32 brightest = TestImages.BrightestOpaquePixel(image);
		Assert.AreEqual(200, brightest.R, "The brightest opaque pixel should reach the target colour exactly.");
		Assert.AreEqual(100, brightest.G);
		Assert.AreEqual(50, brightest.B);
	}

	[TestMethod]
	public void FlattensMultiColouredArtworkToASingleHue()
	{
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 30, 30, new Rgba32(255, 0, 0, 255));
		TestImages.FillRect(image, 40, 40, 30, 30, new Rgba32(30, 144, 255, 255));

		IconHelper.ProcessImage(image, Color.FromArgb(0, 0, 255), 60, 0);

		// Tinting with pure blue means no pixel may carry any red or green at all,
		// regardless of what colour it started as.
		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				Rgba32 pixel = image[x, y];
				Assert.AreEqual(0, pixel.R, $"Pixel ({x},{y}) retained a red component after flattening.");
				Assert.AreEqual(0, pixel.G, $"Pixel ({x},{y}) retained a green component after flattening.");
			}
		}
	}

	[TestMethod]
	public void KeepsTheCanvasSizeWhenPaddingIsApplied()
	{
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 20, 20, 128, 128, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 64, 8);

		Assert.AreEqual(64, image.Width, "Padding insets the artwork but must not change the canvas size.");
		Assert.AreEqual(64, image.Height);
	}

	[TestMethod]
	public void PaddingLeavesTheBorderTransparent()
	{
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 20, 20, 128, 128, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.White, 64, 8);

		Assert.AreEqual(0, image[0, 0].A, "The padded border should be fully transparent.");
		Assert.AreEqual(0, image[63, 63].A);
		Assert.AreEqual(255, image[32, 32].A, "The centre of the artwork should remain opaque.");
	}

	[TestMethod]
	public void PreservesTransparencyOfTheSourceArtwork()
	{
		// A shape with a transparent notch cut out of it keeps that hole after processing.
		using Image<Rgba32> image = TestImages.Blank(100, 100);
		TestImages.FillRect(image, 10, 10, 40, 40, OpaqueWhite);
		TestImages.FillRect(image, 20, 20, 10, 10, TestImages.Transparent);

		IconHelper.ProcessImage(image, Color.White, 40, 0);

		Assert.AreEqual(40, image.Width);
		Assert.AreEqual(0, image[15, 15].A, "The transparent notch should survive processing.");
		Assert.AreEqual(255, image[2, 2].A, "The surrounding artwork should remain opaque.");
	}
}
