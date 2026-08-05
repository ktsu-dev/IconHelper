// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper.Test;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Characterization tests: each case runs a committed input fixture through the real
/// <see cref="IconHelper.ProcessDirectory"/> path and compares the result pixel-for-pixel against a
/// committed expected image.
/// <para>
/// These lock in the *current* output of the tool. A deliberate change to the algorithm is expected
/// to break them. Regenerate the expected images with
/// <c>IconHelper.Test/GoldMaster/regenerate.ps1</c> and review the diff before committing.
/// </para>
/// </summary>
[TestClass]
public class GoldMasterTests
{
	private static string GoldMasterRoot => Path.Combine(AppContext.BaseDirectory, "GoldMaster");

	internal static string InputDirectory => Path.Combine(GoldMasterRoot, "Input");

	internal static string ExpectedDirectory => Path.Combine(GoldMasterRoot, "Expected");

	/// <summary>
	/// The gold master matrix. Each row is an input fixture plus the settings to process it with.
	/// The expected file name is derived from them.
	/// </summary>
	internal static readonly (string Input, string Colour, int Size, int Padding)[] Cases =
	[
		("antialiased-circle", "#FFFFFF", 128, 0),
		("antialiased-circle", "#FF8800", 64, 4),
		("solid-black-square", "#00FF00", 32, 0),
		("midtone-grey-shape", "#3366CC", 96, 8),
		("wide-rect", "#FFFFFF", 64, 0),
		("colorful-icon", "#FF0000", 48, 2),
		("tiny-glyph", "#00FFFF", 128, 0),
		("fully-transparent", "#FFFFFF", 32, 0),
	];

	internal static string ExpectedFileName(string input, string colour, int size, int padding)
		=> string.Create(CultureInfo.InvariantCulture, $"{input}_{colour.TrimStart('#')}_{size}_{padding}.png");

	public static IEnumerable<object[]> CaseData =>
		Cases.Select(c => new object[] { c.Input, c.Colour, c.Size, c.Padding });

	[TestMethod]
	[DynamicData(nameof(CaseData))]
	public void OutputMatchesTheGoldMaster(string input, string colour, int size, int padding)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(colour);

		string inputFile = Path.Combine(InputDirectory, $"{input}.png");
		string expectedFile = Path.Combine(ExpectedDirectory, ExpectedFileName(input, colour, size, padding));

		Assert.IsTrue(File.Exists(inputFile), $"Missing gold master input fixture: {inputFile}");
		Assert.IsTrue(
			File.Exists(expectedFile),
			$"Missing gold master expectation: {expectedFile}. Run IconHelper.Test/GoldMaster/regenerate.ps1 to create it.");

		using TempDirectory temp = new();
		string caseInput = temp.Combine("in");
		string caseOutput = temp.Combine("out");
		Directory.CreateDirectory(caseInput);
		File.Copy(inputFile, Path.Combine(caseInput, $"{input}.png"));

		Arguments args = new()
		{
			InputPath = caseInput,
			OutputPath = caseOutput,
			Color = colour,
			Size = size,
			Padding = padding,
		};

		Assert.IsTrue(args.Validate(out _), "The gold master case itself should use valid arguments.");

		BatchResult result = IconHelper.ProcessDirectory(args, TestImages.ParseHexColour(colour));
		Assert.AreEqual(1, result.Written, "The fixture should have produced exactly one output file.");

		string actualFile = Path.Combine(caseOutput, $"{input}.png");
		using Image<Rgba32> expected = Image.Load<Rgba32>(expectedFile);
		using Image<Rgba32> actual = Image.Load<Rgba32>(actualFile);

		string context = $"{input} @ {colour} size={size} padding={padding}";
		try
		{
			ImageAssert.PixelsAreEqual(expected, actual, context);
		}
		catch (AssertFailedException)
		{
			// Preserve the actual output outside the self-deleting temp directory so a failure
			// can be inspected visually rather than only as pixel coordinates.
			string rescue = Path.Combine(Path.GetTempPath(), "ktsu.IconHelper.Test.failures");
			Directory.CreateDirectory(rescue);
			File.Copy(actualFile, Path.Combine(rescue, ExpectedFileName(input, colour, size, padding)), overwrite: true);
			throw;
		}
	}

	[TestMethod]
	public void EveryInputFixtureIsExercisedByAtLeastOneCase()
	{
		string[] fixtures = [.. Directory.GetFiles(InputDirectory, "*.png").Select(Path.GetFileNameWithoutExtension)!];

		Assert.AreNotEqual(0, fixtures.Length, "No gold master input fixtures were found.");

		string[] unused = [.. fixtures.Where(f => !Cases.Any(c => c.Input == f)).Order()];

		Assert.AreEqual(
			0,
			unused.Length,
			$"These input fixtures are committed but never processed by a gold master case: {string.Join(", ", unused)}");
	}

	[TestMethod]
	public void EveryExpectedImageIsClaimedByACase()
	{
		string[] expectedFiles = [.. Directory.GetFiles(ExpectedDirectory, "*.png").Select(Path.GetFileName)!];
		string[] claimed = [.. Cases.Select(c => ExpectedFileName(c.Input, c.Colour, c.Size, c.Padding))];

		string[] orphans = [.. expectedFiles.Where(f => !claimed.Contains(f)).Order()];

		Assert.AreEqual(
			0,
			orphans.Length,
			$"These expected images are committed but no case references them: {string.Join(", ", orphans)}");
	}

	[TestMethod]
	public void TheGoldMasterMatrixIsNotEmpty()
	{
		// Guards against the parameterised test silently passing because the case list was emptied.
		Assert.AreNotEqual(0, Cases.Length);
	}
}
