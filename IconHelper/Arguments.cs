namespace ktsu.IconHelper;

using CommandLine;

internal class Arguments
{
	[Option('i', "input", Required = true, HelpText = "The path to the directory containing the input files.")]
	public string InputPath { get; set; } = string.Empty;
	[Option('o', "output", Required = true, HelpText = "The path to the directory where you want the modified files to be written.")]
	public string OutputPath { get; set; } = string.Empty;

	[Option('c', "color", Required = false, HelpText = "The color to use for the icon. Defaults to #FFFFFF.")]
	public string Color { get; set; } = "#FFFFFF";

	[Option('s', "size", Required = false, HelpText = "The maximum size of the icon. Defaults to 128.")]
	public int Size { get; set; } = 128;
}
