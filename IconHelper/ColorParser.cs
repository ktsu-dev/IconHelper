// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.IconHelper;

using System.Linq;

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
	/// A fully transparent colour is rejected, see <see cref="IsFullyTransparent"/>.
	/// </summary>
	/// <returns><see langword="true"/> when the value was understood.</returns>
	internal static bool TryParse(string? value, out Color color)
	{
		if (!TryParseAnyAlpha(value, out color) || IsTransparent(color))
		{
			color = default;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Whether the value names a colour whose alpha is zero, such as <c>transparent</c> or
	/// <c>#FF880000</c>. The output alpha carries the coverage and the colour's own alpha is
	/// ignored, so such a colour would only contribute its RGB and paint the icon opaque.
	/// </summary>
	internal static bool IsFullyTransparent(string? value) =>
		TryParseAnyAlpha(value, out Color color) && IsTransparent(color);

	/// <summary>
	/// The colour names understood by <see cref="TryParse"/>, for use in error messages.
	/// </summary>
	internal static string KnownNames => string.Join(", ", NamedColors.All.Where(named => !IsTransparent(named.Value)).Select(named => named.Key));

	private static bool IsTransparent(Color color) => color.ToBytes().A == 0;

	private static bool TryParseAnyAlpha(string? value, out Color color)
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
}
