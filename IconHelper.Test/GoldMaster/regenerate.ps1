# Copyright (c) ktsu.dev
# All rights reserved.
# Licensed under the MIT license.

# Regenerates the gold master expected images by running the real CLI over each input fixture.
# Run this only after deliberately changing the image pipeline, then review the diff before
# committing. An unreviewed regeneration defeats the point of a gold master.

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$inputDir = Join-Path $root 'Input'
$expectedDir = Join-Path $root 'Expected'
$project = Join-Path $root '..\..\IconHelper\IconHelper.csproj'

# Keep this list in sync with GoldMasterTests.Cases.
$cases = @(
	@{ Input = 'antialiased-circle'; Colour = '#FFFFFF'; Size = 128; Padding = 0 }
	@{ Input = 'antialiased-circle'; Colour = '#FF8800'; Size = 64; Padding = 4 }
	@{ Input = 'solid-black-square'; Colour = '#00FF00'; Size = 32; Padding = 0 }
	@{ Input = 'midtone-grey-shape'; Colour = '#3366CC'; Size = 96; Padding = 8 }
	@{ Input = 'wide-rect'; Colour = '#FFFFFF'; Size = 64; Padding = 0 }
	@{ Input = 'colorful-icon'; Colour = '#FF0000'; Size = 48; Padding = 2 }
	@{ Input = 'tiny-glyph'; Colour = '#00FFFF'; Size = 128; Padding = 0 }
	@{ Input = 'fully-transparent'; Colour = '#FFFFFF'; Size = 32; Padding = 0 }
)

New-Item -ItemType Directory -Force $expectedDir | Out-Null
dotnet build $project -c Release | Out-Null

foreach ($case in $cases) {
	$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("goldmaster-" + [System.Guid]::NewGuid().ToString('N'))
	$stageIn = Join-Path $staging 'in'
	$stageOut = Join-Path $staging 'out'
	New-Item -ItemType Directory -Force $stageIn | Out-Null

	Copy-Item (Join-Path $inputDir "$($case.Input).png") $stageIn

	dotnet run --project $project -c Release --no-build -- `
		-i $stageIn -o $stageOut -c $case.Colour -s $case.Size -p $case.Padding | Out-Null

	$name = "$($case.Input)_$($case.Colour.TrimStart('#'))_$($case.Size)_$($case.Padding).png"
	Copy-Item (Join-Path $stageOut "$($case.Input).png") (Join-Path $expectedDir $name) -Force
	Write-Host "regenerated $name"

	Remove-Item -Recurse -Force $staging
}
