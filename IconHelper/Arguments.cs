// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using CommandLine;

using ktsu.Semantics.Paths;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes - this class is instantiated by the CommandLineParser
internal sealed class Arguments
{
	[Option('i', "input", Required = true, HelpText = "The path to the directory containing the input files.")]
	public string InputPath { get; set; } = string.Empty;
	[Option('o', "output", Required = true, HelpText = "The path to the directory where you want the modified files to be written.")]
	public string OutputPath { get; set; } = string.Empty;

	[Option('c', "color", Required = false, HelpText = "The color to use for the icon, as a hex value or a known color name. Defaults to #FFFFFF.")]
	public string Color { get; set; } = "#FFFFFF";

	[Option('s', "size", Required = false, HelpText = "The maximum size of the icon. Defaults to 128.")]
	public int Size { get; set; } = 128;

	[Option('p', "padding", Required = false, HelpText = "The number of pixels per size to pad the output image. Must be < (size / 2). Will not change the output size. Defaults to 0.")]
	public int Padding { get; set; } = 0;

	internal bool Validate(out Collection<string> errors)
	{
		errors = [];
		if (Padding >= Size / 2)
		{
			errors.Add("Padding must be less than half the size of the image.");
		}

		if (!TryResolveInput(out _, out string? inputError))
		{
			errors.Add(inputError);
		}

		if (!TryResolveOutput(out _, out string? outputError))
		{
			errors.Add(outputError);
		}

		if (!ColorParser.TryParse(Color, out _))
		{
			errors.Add($"'{Color}' is not a color. Use a hex value such as #RRGGBB, #RGB or #RRGGBBAA, or one of: {ColorParser.KnownNames}.");
		}

		return errors.Count == 0;
	}

	/// <summary>
	/// Resolves <see cref="InputPath"/> to an absolute directory that must already exist.
	/// </summary>
	internal bool TryResolveInput(
		[NotNullWhen(true)] out AbsoluteDirectoryPath? directory,
		[NotNullWhen(false)] out string? error)
	{
		if (!TryResolveDirectory(InputPath, "--input", out directory, out error))
		{
			return false;
		}

		string raw = directory;
		if (File.Exists(raw))
		{
			error = $"--input is a file, not a directory: {raw}";
			directory = null;
			return false;
		}

		if (!Directory.Exists(raw))
		{
			error = $"--input directory does not exist: {raw}";
			directory = null;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Resolves <see cref="OutputPath"/> to an absolute directory. It need not exist yet, because it
	/// is created on demand, but it must not already be a file.
	/// </summary>
	internal bool TryResolveOutput(
		[NotNullWhen(true)] out AbsoluteDirectoryPath? directory,
		[NotNullWhen(false)] out string? error)
	{
		if (!TryResolveDirectory(OutputPath, "--output", out directory, out error))
		{
			return false;
		}

		string raw = directory;
		if (File.Exists(raw))
		{
			error = $"--output is a file, not a directory: {raw}";
			directory = null;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Turns a raw command line value into an <see cref="AbsoluteDirectoryPath"/>. Relative values are
	/// resolved against the working directory first, because the semantic type only accepts absolute
	/// paths, and it is the semantic type that rejects the malformed ones.
	/// </summary>
	private static bool TryResolveDirectory(
		string value,
		string option,
		[NotNullWhen(true)] out AbsoluteDirectoryPath? directory,
		[NotNullWhen(false)] out string? error)
	{
		directory = null;
		error = null;

		if (string.IsNullOrWhiteSpace(value))
		{
			error = $"{option} is required.";
			return false;
		}

		try
		{
			directory = AbsoluteDirectoryPath.Create<AbsoluteDirectoryPath>(Path.GetFullPath(value));
			return true;
		}
		catch (ArgumentException e)
		{
			error = $"{option} is not a usable directory path: {e.Message}";
			return false;
		}
		catch (NotSupportedException e)
		{
			error = $"{option} is not a usable directory path: {e.Message}";
			return false;
		}
		catch (IOException e)
		{
			// Path.GetFullPath throws PathTooLongException, which derives from IOException.
			error = $"{option} is not a usable directory path: {e.Message}";
			return false;
		}
	}
}
