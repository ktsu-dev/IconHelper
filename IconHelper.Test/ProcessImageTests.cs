// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper.Test;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using ktsu.Semantics.Color;

using Color = ktsu.Semantics.Color.Color;

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

		IconHelper.ProcessImage(image, NamedColors.White, 128, 0);

		Assert.AreEqual(image.Width, image.Height, "Output canvas should always be square.");
	}

	[TestMethod]
	public void ClampsOutputToTheRequestedMaximumSize()
	{
		using Image<Rgba32> image = TestImages.Blank(300, 300);
		TestImages.FillRect(image, 10, 10, 250, 250, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 32, 0);

		Assert.AreEqual(32, image.Width);
		Assert.AreEqual(32, image.Height);
	}

	[TestMethod]
	public void DoesNotUpscaleArtworkSmallerThanTheRequestedSize()
	{
		using Image<Rgba32> image = TestImages.Blank(64, 64);
		TestImages.FillRect(image, 10, 10, 12, 12, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 512, 0);

		Assert.AreEqual(12, image.Width, "Artwork smaller than the requested size must not be grown.");
		Assert.AreEqual(12, image.Height);
	}

	[TestMethod]
	public void TrimsTransparentMarginsDownToTheArtwork()
	{
		using Image<Rgba32> image = TestImages.Blank(400, 400);
		TestImages.FillRect(image, 300, 12, 40, 40, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 256, 0);

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

		IconHelper.ProcessImage(image, NamedColors.White, 512, 0);

		Assert.AreEqual(30, image.Width, "The crop should cover the full width of the artwork.");
		Assert.AreEqual(30, image.Height, "The shorter axis is padded up to the longer one.");
	}

	[TestMethod]
	public void ProducesATransparentSquareWhenTheArtworkHasNoOpaquePixels()
	{
		// Regression test. With nothing opaque to measure, the bounding box is never updated and
		// stays inverted, which used to produce a negative crop width and throw.
		using Image<Rgba32> image = TestImages.Blank(64, 48);

		IconHelper.ProcessImage(image, NamedColors.White, 128, 0);

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

		IconHelper.ProcessImage(image, NamedColors.White, 32, 0);

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

		IconHelper.ProcessImage(image, NamedColors.White, 512, 0);

		Assert.AreEqual(20, image.Width);
		Assert.AreEqual(255, image[0, 0].A, "The first column/row of the artwork should be kept.");
		Assert.AreEqual(255, image[19, 19].A, "The last column/row of the artwork should be kept.");
	}

	[TestMethod]
	public void PaintsOpaqueArtworkInTheTargetColour()
	{
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 20, 20, 40, 40, OpaqueWhite);

		IconHelper.ProcessImage(image, Color.FromBytes(0, 128, 255), 40, 0);

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

		IconHelper.ProcessImage(image, Color.FromBytes(0, 255, 0), 40, 0);

		Rgba32 centre = image[20, 20];
		Assert.AreEqual(0, centre.R);
		Assert.AreEqual(255, centre.G, "An all-black glyph should take the full target colour.");
		Assert.AreEqual(0, centre.B);
		Assert.AreEqual(255, centre.A);
	}

	[TestMethod]
	public void NormalizesMidtoneArtworkUpToFullCoverage()
	{
		// A mid-grey of 80 lands inside the BlackWhite ramp at 105, so maxValue ends up strictly
		// between 0 and 255 and the offset normalization has to lift it back to full intensity.
		// Since intensity is now what drives alpha, "full intensity" means "fully covered".
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 20, 20, 40, 40, new Rgba32(80, 80, 80, 255));

		IconHelper.ProcessImage(image, Color.FromBytes(200, 100, 50), 40, 0);

		Rgba32 centre = image[20, 20];
		Assert.AreEqual(255, centre.A, "The brightest opaque pixel should reach full coverage.");
		Assert.AreEqual(200, centre.R, "The colour channels carry the target colour flat.");
		Assert.AreEqual(100, centre.G);
		Assert.AreEqual(50, centre.B);
	}

	[TestMethod]
	public void FoldsBrightnessIntoTheAlphaChannel()
	{
		// The core of the coverage output. A white patch and a mid-grey patch are equally opaque in
		// the source, so under the old tint they differed only in how dark the colour came out.
		// Now they differ in alpha instead, and the colour is identical across both.
		//
		// The grey of 80 passes through the BlackWhite ramp to 105, and the white patch pins
		// maxValue at 255, so the grey normalizes to 105 and 255 * 105 / 255 is 105 of coverage.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 20, 20, OpaqueWhite);
		TestImages.FillRect(image, 40, 40, 20, 20, new Rgba32(80, 80, 80, 255));

		IconHelper.ProcessImage(image, Color.FromBytes(200, 100, 50), 512, 0);

		// The crop covers both patches, so the white one starts at (0,0) and the grey at (30,30).
		Rgba32 white = image[5, 5];
		Rgba32 grey = image[35, 35];

		Assert.AreEqual(255, white.A, "A fully lit pixel should be fully covered.");
		Assert.AreEqual(105, grey.A, "A midtone pixel should become partial coverage, not a darker colour.");

		foreach (Rgba32 pixel in new[] { white, grey })
		{
			Assert.AreEqual(200, pixel.R, "Brightness must not survive in the colour channels.");
			Assert.AreEqual(100, pixel.G);
			Assert.AreEqual(50, pixel.B);
		}
	}

	[TestMethod]
	public void MultipliesSourceAlphaIntoTheCoverage()
	{
		// Coverage is the product of brightness and the source alpha, so a half transparent white
		// pixel is half covered even though it is at full brightness.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 20, 20, OpaqueWhite);
		TestImages.FillRect(image, 10, 10, 20, 10, new Rgba32(255, 255, 255, 128));

		IconHelper.ProcessImage(image, Color.FromBytes(0, 128, 255), 512, 0);

		Assert.AreEqual(128, image[5, 5].A, "Source alpha should carry through into the coverage.");
		Assert.AreEqual(255, image[5, 15].A, "The fully opaque half is unaffected.");
	}

	[TestMethod]
	public void DropsUnlitArtworkFromTheCoverage()
	{
		// A black region sitting alongside a white one normalizes to intensity 0, which is now zero
		// coverage rather than an opaque black patch. It must therefore also fall outside the crop,
		// or the canvas would be padded out around artwork that is no longer visible.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 20, 20, OpaqueWhite);
		TestImages.FillRect(image, 10, 40, 20, 20, OpaqueBlack);

		IconHelper.ProcessImage(image, Color.FromBytes(0, 255, 0), 512, 0);

		Assert.AreEqual(20, image.Width, "The crop should ignore the unlit region entirely.");
		Assert.AreEqual(20, image.Height);
		Assert.AreEqual(255, image[5, 5].A, "The lit region survives at full coverage.");
	}

	[TestMethod]
	public void PaintsEveryPixelTheFlatTargetColour()
	{
		// Nothing in the output may modulate the colour channels: whatever the source tones were,
		// every pixel comes out as exactly the target colour, with the shape only in the alpha.
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 30, 30, new Rgba32(255, 255, 255, 255));
		TestImages.FillRect(image, 20, 20, 30, 30, new Rgba32(80, 80, 80, 255));
		TestImages.FillRect(image, 30, 30, 20, 20, new Rgba32(96, 96, 96, 255));

		IconHelper.ProcessImage(image, Color.FromBytes(200, 100, 50), 512, 0);

		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				Rgba32 pixel = image[x, y];
				Assert.AreEqual(200, pixel.R, $"Pixel ({x},{y}) does not carry the flat target colour.");
				Assert.AreEqual(100, pixel.G, $"Pixel ({x},{y}) does not carry the flat target colour.");
				Assert.AreEqual(50, pixel.B, $"Pixel ({x},{y}) does not carry the flat target colour.");
			}
		}
	}

	[TestMethod]
	public void FlattensMultiColouredArtworkToASingleHue()
	{
		using Image<Rgba32> image = TestImages.Blank(80, 80);
		TestImages.FillRect(image, 10, 10, 30, 30, new Rgba32(255, 0, 0, 255));
		TestImages.FillRect(image, 40, 40, 30, 30, new Rgba32(30, 144, 255, 255));

		IconHelper.ProcessImage(image, Color.FromBytes(0, 0, 255), 60, 0);

		// Painting with pure blue means no pixel may carry any red or green at all,
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

		IconHelper.ProcessImage(image, NamedColors.White, 64, 8);

		Assert.AreEqual(64, image.Width, "Padding insets the artwork but must not change the canvas size.");
		Assert.AreEqual(64, image.Height);
	}

	[TestMethod]
	public void PaddingLeavesTheBorderTransparent()
	{
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 20, 20, 128, 128, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 64, 8);

		Assert.AreEqual(0, image[0, 0].A, "The padded border should be fully transparent.");
		Assert.AreEqual(0, image[63, 63].A);
		Assert.AreEqual(255, image[32, 32].A, "The centre of the artwork should remain opaque.");
	}

	[TestMethod]
	public void PaddingValidAgainstTheRequestedSizeStillWorksOnSmallerArtwork()
	{
		// Regression test. Arguments.Validate only checks padding against --size, but the canvas
		// actually used is min(trimmedSquareSize, size) because sizing is downscale-only. Artwork
		// that trims smaller than --size therefore gets a canvas the validated padding does not fit
		// on: here 45 < 100/2 validates, but the real canvas is 80, so the content size used to come
		// out as 80 - 90 = -10 and Resize threw.
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 10, 10, 80, 80, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 100, 45);

		Assert.AreEqual(80, image.Width, "The downscale-only canvas is unchanged by the padding clamp.");
		Assert.AreEqual(80, image.Height);
	}

	[TestMethod]
	public void PaddingTooLargeForTheCanvasStillLeavesVisibleContent()
	{
		// The clamp has to leave at least one pixel of content: a canvas padded to nothing would be
		// a silently blank icon, which is no better than the throw it replaces.
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 10, 10, 80, 80, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 100, 45);

		Assert.AreEqual(255, image[40, 40].A, "The centre of the canvas should still carry artwork.");
		Assert.AreEqual(0, image[0, 0].A, "The clamped padding should still inset the artwork.");
	}

	[TestMethod]
	public void PaddingThatFitsIsAppliedExactlyAndNotClamped()
	{
		// The guard against the clamp reaching inputs that were always valid. 8 fits on the 80 pixel
		// canvas, so the content must be inset by exactly 8 per side and no more: pixel 7 is padding
		// and pixel 8 is the first row of artwork.
		using Image<Rgba32> image = TestImages.Blank(200, 200);
		TestImages.FillRect(image, 10, 10, 80, 80, OpaqueWhite);

		IconHelper.ProcessImage(image, NamedColors.White, 100, 8);

		Assert.AreEqual(80, image.Width);
		Assert.AreEqual(0, image[7, 40].A, "The last padding column should be transparent.");
		Assert.AreEqual(255, image[8, 40].A, "The artwork should start exactly at the padding offset.");
	}

	[TestMethod]
	public void PreservesTransparencyOfTheSourceArtwork()
	{
		// A shape with a transparent notch cut out of it keeps that hole after processing.
		using Image<Rgba32> image = TestImages.Blank(100, 100);
		TestImages.FillRect(image, 10, 10, 40, 40, OpaqueWhite);
		TestImages.FillRect(image, 20, 20, 10, 10, TestImages.Transparent);

		IconHelper.ProcessImage(image, NamedColors.White, 40, 0);

		Assert.AreEqual(40, image.Width);
		Assert.AreEqual(0, image[15, 15].A, "The transparent notch should survive processing.");
		Assert.AreEqual(255, image[2, 2].A, "The surrounding artwork should remain opaque.");
	}
}
