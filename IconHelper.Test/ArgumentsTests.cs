// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper.Test;

using System.Collections.ObjectModel;
using System.IO;

using ktsu.Semantics.Paths;

[TestClass]
public class ArgumentsTests
{
	/// <summary>
	/// Builds arguments whose paths are valid, so that only the property under test can fail.
	/// </summary>
	private static Arguments ValidArguments(TempDirectory temp)
	{
		string input = temp.Combine("in");
		Directory.CreateDirectory(input);
		return new Arguments
		{
			InputPath = input,
			OutputPath = temp.Combine("out"),
		};
	}

	[TestMethod]
	public void DefaultsAreWhiteAt128PixelsWithNoPadding()
	{
		Arguments args = new();

		Assert.AreEqual("#FFFFFF", args.Color);
		Assert.AreEqual(128, args.Size);
		Assert.AreEqual(0, args.Padding);
		Assert.AreEqual(string.Empty, args.InputPath);
		Assert.AreEqual(string.Empty, args.OutputPath);
	}

	[TestMethod]
	public void ValidateAcceptsWellFormedArguments()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsTrue(valid, $"Expected valid arguments but got: {string.Join(", ", errors)}");
		Assert.IsEmpty(errors);
	}

	[TestMethod]
	public void ValidateAcceptsPaddingBelowHalfTheSize()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Size = 64;
		args.Padding = 31;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsTrue(valid, "Padding of 31 is below half of 64 and should be accepted.");
		Assert.IsEmpty(errors);
	}

	[TestMethod]
	public void ValidateRejectsPaddingEqualToHalfTheSize()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Size = 64;
		args.Padding = 32;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "Padding equal to half the size leaves no content and should be rejected.");
		Assert.HasCount(1, errors);
		Assert.AreEqual("Padding must be less than half the size of the image.", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsNegativePadding()
	{
		// The upper bound never caught this: -5 >= 64 is false. The run then produced an output
		// larger than the documented min(squareSize, size) canvas and exited 0 to say it worked.
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Size = 128;
		args.Padding = -5;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "Negative padding grows the canvas instead of insetting content.");
		Assert.HasCount(1, errors);
		Assert.AreEqual("Padding must not be negative.", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsASizeThatIsNotPositive()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Size = 0;
		args.Padding = 0;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "A size of zero leaves no canvas at all.");
		Assert.HasCount(1, errors, $"Expected only the size error but got: {string.Join(", ", errors)}");
		Assert.AreEqual("Size must be greater than zero.", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsANegativeSizeAndPaddingTogether()
	{
		// The combination the upper bound was least able to catch: -20 >= -10 / 2 is false, so
		// both values passed validation untouched and the pair reached the pipeline.
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Size = -10;
		args.Padding = -20;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "Neither a negative size nor a negative padding is usable.");
		Assert.HasCount(2, errors, $"Expected a size and a padding error but got: {string.Join(", ", errors)}");
	}

	[TestMethod]
	public void ValidateUsesIntegerDivisionForOddSizes()
	{
		// 33 / 2 == 16 under integer division, so 16 is rejected and 15 accepted.
		using TempDirectory temp = new();
		Arguments rejected = ValidArguments(temp);
		rejected.Size = 33;
		rejected.Padding = 16;

		Arguments accepted = ValidArguments(temp);
		accepted.Size = 33;
		accepted.Padding = 15;

		Assert.IsFalse(rejected.Validate(out _), "Padding of 16 is not less than 33 / 2 == 16.");
		Assert.IsTrue(accepted.Validate(out _), "Padding of 15 is less than 33 / 2 == 16.");
	}

	[TestMethod]
	public void ValidateRejectsAnInputDirectoryThatDoesNotExist()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.InputPath = temp.UncreatedSubdirectory("nowhere");

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "A missing input directory should be caught by validation, not at enumeration time.");
		Assert.HasCount(1, errors);
		Assert.Contains("--input directory does not exist", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsAnInputThatIsAFile()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		string file = temp.Combine("not-a-directory.png");
		File.WriteAllText(file, "x");
		args.InputPath = file;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid);
		Assert.HasCount(1, errors);
		Assert.Contains("--input is a file, not a directory", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsAnOutputThatIsAFile()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		string file = temp.Combine("occupied.png");
		File.WriteAllText(file, "x");
		args.OutputPath = file;

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid);
		Assert.HasCount(1, errors);
		Assert.Contains("--output is a file, not a directory", errors[0]);
	}

	[TestMethod]
	public void ValidateAcceptsAnOutputDirectoryThatDoesNotExistYet()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.OutputPath = temp.UncreatedSubdirectory("created-later");

		Assert.IsTrue(args.Validate(out _), "The output directory is created on demand, so it need not exist yet.");
	}

	[TestMethod]
	public void ValidateRejectsEmptyPaths()
	{
		Arguments args = new();

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid);
		Assert.HasCount(2, errors, $"Both paths should be reported: {string.Join(", ", errors)}");
	}

	[TestMethod]
	public void ValidateRejectsAnUnparseableColor()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.Color = "not-a-color";

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid);
		Assert.HasCount(1, errors);
		Assert.Contains("is not a color", errors[0]);
	}

	[TestMethod]
	public void ValidateReportsEveryProblemAtOnce()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.InputPath = temp.UncreatedSubdirectory("nowhere");
		args.Size = 32;
		args.Padding = 99;
		args.Color = "nonsense";

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid);
		Assert.HasCount(3, errors, $"Expected padding, input and color errors but got: {string.Join(", ", errors)}");
	}

	[TestMethod]
	public void ResolvingInputTurnsARelativePathIntoAnAbsoluteOne()
	{
		using TempDirectory temp = new();
		Arguments args = ValidArguments(temp);
		args.InputPath = ".";

		Assert.IsTrue(args.TryResolveInput(out AbsoluteDirectoryPath? resolved, out string? error), error);
		Assert.AreEqual(Directory.GetCurrentDirectory(), (string)resolved);
	}
}
