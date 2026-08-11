// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper;

using ktsu.Semantics.Color;

using Color = ktsu.Semantics.Color.Color;

/// <summary>
/// Parses the <c>--color</c> option into a colour.
/// </summary>
internal static class ColorParser
{
	/// <summary>
	/// Accepts a name from <see cref="NamedColors"/>, case insensitively, or a hex string in
	/// <c>#RGB</c>, <c>#RRGGBB</c> or <c>#RRGGBBAA</c> form. The leading <c>#</c> is optional.
	/// </summary>
	/// <returns><see langword="true"/> when the value was understood.</returns>
	internal static bool TryParse(string? value, out Color color)
	{
		color = default;

		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		string trimmed = value.Trim();

		if (NamedColors.TryGet(trimmed, out color))
		{
			return true;
		}

		try
		{
			color = Color.FromHex(trimmed);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			// FromHex throws this for a hex string of an unsupported length.
			return false;
		}
	}

	/// <summary>
	/// The colour names understood by <see cref="TryParse"/>, for use in error messages.
	/// </summary>
	internal static string KnownNames => string.Join(", ", NamedColors.All.Keys);
}
