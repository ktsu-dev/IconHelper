// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper.Test;

using System;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Diagnostics go to stderr and progress to stdout, so a script that discards the progress lines
/// still sees why a run exited non-zero. These swap the process-wide console writers, so they must
/// not run alongside other tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ConsoleStreamTests
{
	private static Arguments ArgumentsFor(string input, string output) => new()
	{
		InputPath = input,
		OutputPath = output,
		Size = 32,
		Padding = 0,
	};

	private static (int ExitCode, string Out, string Error) Capture(Arguments args)
	{
		TextWriter originalOut = Console.Out;
		TextWriter originalError = Console.Error;
		using StringWriter output = new();
		using StringWriter error = new();
		try
		{
			Console.SetOut(output);
			Console.SetError(error);
			int exitCode = IconHelper.Execute(args);
			return (exitCode, output.ToString(), error.ToString());
		}
		finally
		{
			Console.SetOut(originalOut);
			Console.SetError(originalError);
		}
	}

	[TestMethod]
	public void InvalidInputIsReportedOnStandardError()
	{
		using TempDirectory temp = new();

		(int exitCode, string output, string error) = Capture(ArgumentsFor(temp.UncreatedSubdirectory("missing"), temp.Combine("out")));

		Assert.AreEqual(IconHelper.ExitInvalidArguments, exitCode);
		Assert.Contains("Argument validation failed", error);
		Assert.DoesNotContain("Argument validation failed", output);
	}

	[TestMethod]
	public void UnusableOutputFolderIsReportedOnStandardError()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		Directory.CreateDirectory(input);
		File.WriteAllText(Path.Join(input, "blocker"), "not a folder");

		// The output folder sits under a file, so it cannot be created once the batch starts.
		(int exitCode, string output, string error) = Capture(ArgumentsFor(input, Path.Join(input, "blocker", "out")));

		Assert.AreEqual(IconHelper.ExitInvalidArguments, exitCode);
		Assert.Contains("Argument validation failed", error);
		Assert.DoesNotContain("Argument validation failed", output);
	}

	[TestMethod]
	public void FileFailuresAreReportedOnStandardErrorAndProgressOnStandardOutput()
	{
		using TempDirectory temp = new();
		string input = temp.Combine("in");
		Directory.CreateDirectory(input);
		File.WriteAllText(Path.Join(input, "broken.png"), "x");
		using (Image<Rgba32> image = TestImages.Blank(64, 64))
		{
			TestImages.FillRect(image, 16, 16, 32, 32, new Rgba32(255, 255, 255, 255));
			image.SaveAsPng(Path.Join(input, "save.png"));
			image.SaveAsBmp(Path.Join(input, "save.bmp"));
		}

		(int exitCode, string output, string error) = Capture(ArgumentsFor(input, temp.Combine("out")));

		Assert.AreEqual(IconHelper.ExitSomeFilesFailed, exitCode);
		Assert.Contains("Failed to process", error);
		Assert.Contains("broken.png", error);
		Assert.Contains("was already written from", error);
		Assert.DoesNotContain("Failed to process", output);
		Assert.Contains("Processing", output);
		Assert.Contains("Done.", output);
		Assert.DoesNotContain("Processing", error);
		Assert.DoesNotContain("Done.", error);
	}
}
