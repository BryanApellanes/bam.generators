#!/usr/bin/env bash
# Packs bam.generators.decorators.runtime (netstandard2.0 + net10.0) into ~/.bam/build/pack/Release/.
# Pass --push with NUGET_API_KEY set to push the package to nuget.org.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$here/bam.generators.decorators.runtime/bam.generators.decorators.runtime.csproj"
out="$HOME/.bam/build/pack/Release"

dotnet pack "$project" -c Release -nologo
package="$(ls -t "$out"/bam.generators.decorators.runtime.*.nupkg | head -1)"
echo "packed: $package"

if [ "${1:-}" = "--push" ]; then
  : "${NUGET_API_KEY:?NUGET_API_KEY is not set}"
  dotnet nuget push "$package" --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
fi
