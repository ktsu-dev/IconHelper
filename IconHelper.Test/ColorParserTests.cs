// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper.Test;

using Color = ktsu.Semantics.Color.Color;

[TestClass]
public class ColorParserTests
{
	private static (byte R, byte G, byte B, byte A) Parse(string value)
	{
		Assert.IsTrue(ColorParser.TryParse(value, out Color color), $"'{value}' should parse.");
		return color.ToBytes();
	}

	[TestMethod]
	public void ParsesSixDigitHex()
	{
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 136, 0, 255), Parse("#FF8800"));
		Assert.AreEqual<(byte, byte, byte, byte)>((51, 102, 204, 255), Parse("#3366CC"));
	}

	[TestMethod]
	public void ParsesHexWithoutTheLeadingHash()
	{
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 136, 0, 255), Parse("FF8800"));
	}

	[TestMethod]
	public void ParsesThreeDigitShorthand()
	{
		// #F80 expands to #FF8800.
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 136, 0, 255), Parse("#F80"));
	}

	[TestMethod]
	public void ParsesEightDigitHexWithAlpha()
	{
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 136, 0, 170), Parse("#FF8800AA"));
	}

	[TestMethod]
	public void ParsesKnownColorNamesCaseInsensitively()
	{
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 255, 255, 255), Parse("White"));
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 255, 255, 255), Parse("white"));
		Assert.AreEqual<(byte, byte, byte, byte)>((255, 0, 0, 255), Parse("RED"));
	}

	[TestMethod]
	public void IgnoresSurroundingWhitespace()
	{
		Assert.AreEqual<(byte, byte, byte, byte)>((0, 255, 0, 255), Parse("  #00FF00  "));
	}

	[TestMethod]
	public void RejectsCssNamesOutsideTheKnownSet()
	{
		// Deliberate behaviour change. System.Drawing.ColorTranslator understood roughly 140 CSS
		// names, ktsu.Semantics NamedColors understands 13. Anything else must now be given as hex.
		Assert.IsFalse(ColorParser.TryParse("CornflowerBlue", out _));
		Assert.IsFalse(ColorParser.TryParse("RebeccaPurple", out _));
	}

	[TestMethod]
	public void RejectsGarbage()
	{
		Assert.IsFalse(ColorParser.TryParse("not-a-color", out _));
		Assert.IsFalse(ColorParser.TryParse("#GGGGGG", out _));
		Assert.IsFalse(ColorParser.TryParse("#FFFF", out _), "Four digit hex is not a supported length.");
	}

	[TestMethod]
	public void RejectsEmptyInput()
	{
		Assert.IsFalse(ColorParser.TryParse(null, out _));
		Assert.IsFalse(ColorParser.TryParse("", out _));
		Assert.IsFalse(ColorParser.TryParse("   ", out _));
	}

	[TestMethod]
	public void KnownNamesAreListedForErrorMessages()
	{
		// The keys are stored lower case, and lookup is case insensitive.
		StringAssert.Contains(ColorParser.KnownNames, "white");
		StringAssert.Contains(ColorParser.KnownNames, "black");
		StringAssert.Contains(ColorParser.KnownNames, "transparent");
	}
}
