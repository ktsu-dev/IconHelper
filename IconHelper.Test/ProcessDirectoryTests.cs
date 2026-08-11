// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper.Test;

using System.IO;

using CommandLine;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using ktsu.Semantics.Color;

[TestClass]
public class ProcessDirectoryTests
{
	private static Arguments ArgumentsFor(string input, string output) => new()
	{
		InputPath = input,
		OutputPath = output,
		Size = 32,
		Padding = 0,
	};

	private static void WritePng(string path, int size)
	{
		using Image<Rgba32> image = TestImages.Blank(size, size);
		TestImages.FillRect(image, size / 4, size / 4, size / 2, size / 2, new Rgba32(255, 255, 255, 255));
		image.SaveAsPng(path);
	}

	[TestMethod]
	public void CreatesTheOutputDirectoryWhenItDoesNotExist()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		Directory.CreateDirectory(input);
		WritePng(Path.Combine(input, "icon.png"), 64);

		string output = temp.UncreatedSubdirectory("does-not-exist-yet");
		Assert.IsFalse(Directory.Exists(output), "Precondition: the output directory must not exist.");

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.IsTrue(Directory.Exists(output), "The output directory should be created automatically.");
		Assert.AreEqual(1, result.Written);
	}

	[TestMethod]
	public void RewritesTheOutputExtensionToPng()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);

		using (Image<Rgba32> jpeg = TestImages.Blank(64, 64))
		{
			TestImages.FillRect(jpeg, 16, 16, 32, 32, new Rgba32(255, 255, 255, 255));
			jpeg.SaveAsJpeg(Path.Combine(input, "photo.jpg"));
		}

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(1, result.Written);
		Assert.IsTrue(File.Exists(Path.Combine(output, "photo.png")), "A .jpg input should produce a .png output.");
		Assert.IsFalse(File.Exists(Path.Combine(output, "photo.jpg")), "The original extension should not be reused.");
	}

	[TestMethod]
	public void SkipsFilesAlreadyMarkedAsGenerated()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);
		WritePng(Path.Combine(input, "keep.png"), 64);
		WritePng(Path.Combine(input, "already.new.png"), 64);

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(1, result.Written, "Files containing '.new.png' should be skipped.");
		Assert.IsTrue(File.Exists(Path.Combine(output, "keep.png")));
		Assert.IsFalse(File.Exists(Path.Combine(output, "already.new.png")));
	}

	[TestMethod]
	public void ContinuesAfterAFileThatCannotBeDecoded()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);
		File.WriteAllText(Path.Combine(input, "notes.txt"), "definitely not an image");
		WritePng(Path.Combine(input, "good.png"), 64);

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(1, result.Written, "The undecodable file should be skipped but the valid one still processed.");
		Assert.AreEqual(1, result.Failed, "The undecodable file should be counted as a failure.");
		Assert.IsTrue(File.Exists(Path.Combine(output, "good.png")));
	}

	[TestMethod]
	public void ReportsAndSkipsAFileThatCannotBeOpened()
	{
		// A locked file fails with an IOException rather than one of the ImageSharp decode
		// exceptions. It should cost one icon, not the whole batch.
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);

		// Named so that the locked file is enumerated before the valid one.
		string locked = Path.Combine(input, "a-locked.png");
		WritePng(locked, 64);
		WritePng(Path.Combine(input, "z-good.png"), 64);

		using (FileStream hold = new(locked, FileMode.Open, FileAccess.Read, FileShare.None))
		{
			BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

			Assert.AreEqual(1, result.Written, "The valid icon should still be written.");
			Assert.AreEqual(1, result.Failed, "The locked file should be counted as a failure.");
			Assert.IsTrue(File.Exists(Path.Combine(output, "z-good.png")));
			Assert.IsFalse(File.Exists(Path.Combine(output, "a-locked.png")));
		}
	}

	[TestMethod]
	public void ExitCodeIsSuccessWhenNothingFailed()
	{
		Assert.AreEqual(IconHelper.ExitSuccess, IconHelper.ExitCodeFor(new BatchResult(4, 0)));
	}

	[TestMethod]
	public void ExitCodeIsSuccessForAnEmptyRun()
	{
		// Nothing to do is not a failure.
		Assert.AreEqual(IconHelper.ExitSuccess, IconHelper.ExitCodeFor(new BatchResult(0, 0)));
	}

	[TestMethod]
	public void ExitCodeReportsFailureWhenAnyFileFailed()
	{
		Assert.AreEqual(IconHelper.ExitSomeFilesFailed, IconHelper.ExitCodeFor(new BatchResult(9, 1)));
		Assert.AreEqual(IconHelper.ExitSomeFilesFailed, IconHelper.ExitCodeFor(new BatchResult(0, 3)));
	}

	[TestMethod]
	public void HelpAndVersionRequestsExitSuccessfully()
	{
		Assert.AreEqual(
			IconHelper.ExitSuccess,
			IconHelper.ExitCodeForParseErrors([ErrorType.HelpRequestedError]));
		Assert.AreEqual(
			IconHelper.ExitSuccess,
			IconHelper.ExitCodeForParseErrors([ErrorType.VersionRequestedError]));
	}

	[TestMethod]
	public void UnusableArgumentsExitWithTheInvalidArgumentsCode()
	{
		Assert.AreEqual(
			IconHelper.ExitInvalidArguments,
			IconHelper.ExitCodeForParseErrors([ErrorType.MissingRequiredOptionError]));
		Assert.AreEqual(
			IconHelper.ExitInvalidArguments,
			IconHelper.ExitCodeForParseErrors([ErrorType.UnknownOptionError]));
	}

	[TestMethod]
	public void ARealErrorAlongsideAHelpRequestStillFails()
	{
		Assert.AreEqual(
			IconHelper.ExitInvalidArguments,
			IconHelper.ExitCodeForParseErrors([ErrorType.HelpRequestedError, ErrorType.UnknownOptionError]));
	}

	[TestMethod]
	public void ReportsNoFailuresWhenEveryFileSucceeds()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);
		WritePng(Path.Combine(input, "icon.png"), 64);

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(1, result.Written);
		Assert.AreEqual(0, result.Failed);
	}

	[TestMethod]
	public void AFullyTransparentFileDoesNotStopTheBatch()
	{
		// Regression test. A blank image used to throw ArgumentOutOfRangeException, which is not one
		// of the three ImageSharp exception types the per-file catch handles, so a single unusable
		// file took down the whole run and every icon queued behind it was lost.
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);

		// Named so that the blank file is enumerated first.
		using (Image<Rgba32> blank = TestImages.Blank(64, 64))
		{
			blank.SaveAsPng(Path.Combine(input, "a-blank.png"));
		}

		WritePng(Path.Combine(input, "z-good.png"), 64);

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(2, result.Written, "Both the blank file and the valid icon should be written.");
		Assert.IsTrue(
			File.Exists(Path.Combine(output, "z-good.png")),
			"The valid icon queued behind the blank one should still be written.");

		using Image<Rgba32> blankOutput = Image.Load<Rgba32>(Path.Combine(output, "a-blank.png"));
		Assert.AreEqual(32, blankOutput.Width, "The blank output is clamped to the requested size.");
		Assert.AreEqual(32, blankOutput.Height);
	}

	[TestMethod]
	public void ReturnsZeroForAnEmptyInputDirectory()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(0, result.Written);
	}

	[TestMethod]
	public void CountsEveryFileItWrites()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);
		for (int i = 0; i < 4; i++)
		{
			WritePng(Path.Combine(input, $"icon{i}.png"), 64);
		}

		BatchResult result = IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		Assert.AreEqual(4, result.Written);
		Assert.AreEqual(4, Directory.GetFiles(output).Length);
	}

	[TestMethod]
	public void WritesEightBitRgbaPngFiles()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		string output = temp.Combine("out");
		Directory.CreateDirectory(input);
		WritePng(Path.Combine(input, "icon.png"), 64);

		IconHelper.ProcessDirectory(ArgumentsFor(input, output), NamedColors.White);

		using Image written = Image.Load(Path.Combine(output, "icon.png"));
		PngMetadata png = written.Metadata.GetPngMetadata();

		Assert.AreEqual(PngBitDepth.Bit8, png.BitDepth);
		Assert.AreEqual(PngColorType.RgbWithAlpha, png.ColorType);
	}
}
