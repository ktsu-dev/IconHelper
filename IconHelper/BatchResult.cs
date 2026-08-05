// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.IconHelper;

/// <summary>
/// The outcome of processing a directory of icons.
/// </summary>
/// <param name="Written">The number of files successfully written to the output directory.</param>
/// <param name="Failed">The number of files that could not be processed and were skipped.</param>
internal readonly record struct BatchResult(int Written, int Failed);
