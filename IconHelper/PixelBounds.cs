// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper;

/// <summary>
/// The bounding box of the non transparent pixels in an image, in inclusive pixel indices.
/// </summary>
/// <param name="Left">Index of the leftmost column containing an opaque pixel.</param>
/// <param name="Top">Index of the topmost row containing an opaque pixel.</param>
/// <param name="Right">Index of the rightmost column containing an opaque pixel.</param>
/// <param name="Bottom">Index of the bottommost row containing an opaque pixel.</param>
internal readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom)
{
	/// <summary>
	/// True when the image contained no opaque pixel at all. The bounds are seeded inverted, so
	/// nothing having widened them leaves them that way.
	/// </summary>
	internal bool IsEmpty => Right < Left || Bottom < Top;

	/// <summary>
	/// The bounds are inclusive, so the span they describe is one wider than the difference between
	/// them. Omitting that is what used to drop the rightmost column of every icon.
	/// </summary>
	internal int Width => Right - Left + 1;

	/// <summary>The inclusive height, see <see cref="Width"/>.</summary>
	internal int Height => Bottom - Top + 1;
}
