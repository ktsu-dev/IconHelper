// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper.Test;

using System.Collections.ObjectModel;

[TestClass]
public class ArgumentsTests
{
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
	public void ValidateAcceptsDefaultArguments()
	{
		Arguments args = new();

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsTrue(valid, "Default arguments should validate.");
		Assert.AreEqual(0, errors.Count);
	}

	[TestMethod]
	public void ValidateAcceptsPaddingBelowHalfTheSize()
	{
		Arguments args = new() { Size = 64, Padding = 31 };

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsTrue(valid, "Padding of 31 is below half of 64 and should be accepted.");
		Assert.AreEqual(0, errors.Count);
	}

	[TestMethod]
	public void ValidateRejectsPaddingEqualToHalfTheSize()
	{
		Arguments args = new() { Size = 64, Padding = 32 };

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "Padding equal to half the size leaves no content and should be rejected.");
		Assert.AreEqual(1, errors.Count);
		Assert.AreEqual("Padding must be less than half the size of the image.", errors[0]);
	}

	[TestMethod]
	public void ValidateRejectsPaddingGreaterThanHalfTheSize()
	{
		Arguments args = new() { Size = 32, Padding = 100 };

		bool valid = args.Validate(out Collection<string> errors);

		Assert.IsFalse(valid, "Padding larger than half the size should be rejected.");
		Assert.AreEqual(1, errors.Count);
	}

	[TestMethod]
	public void ValidateUsesIntegerDivisionForOddSizes()
	{
		// 33 / 2 == 16 under integer division, so 16 is rejected and 15 accepted.
		Arguments rejected = new() { Size = 33, Padding = 16 };
		Arguments accepted = new() { Size = 33, Padding = 15 };

		Assert.IsFalse(rejected.Validate(out _), "Padding of 16 is not less than 33 / 2 == 16.");
		Assert.IsTrue(accepted.Validate(out _), "Padding of 15 is less than 33 / 2 == 16.");
	}
}
