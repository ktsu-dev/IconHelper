namespace ktsu.IconHelper;

using System.Collections.ObjectModel;

using CommandLine;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes - this class is instantiated by the CommandLineParser
internal sealed class Arguments
{
	[Option('i', "input", Required = true, HelpText = "The path to the directory containing the input files.")]
	public string InputPath { get; set; } = string.Empty;
	[Option('o', "output", Required = true, HelpText = "The path to the directory where you want the modified files to be written.")]
	public string OutputPath { get; set; } = string.Empty;

	[Option('c', "color", Required = false, HelpText = "The color to use for the icon. Defaults to #FFFFFF.")]
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

		return errors.Count == 0;
	}
}
