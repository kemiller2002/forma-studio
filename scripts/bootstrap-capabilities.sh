#!/usr/bin/env bash
set -euo pipefail

ROOT="$(pwd)"
CE_SHA="4590d2fe6f7e80b339117d3fbee5803f2dd39122"

run_ce() {
  local command="$1"
  local temp
  temp="$(mktemp -d)"
  curl -fsSL "https://github.com/kemiller2002/communication-engineering/archive/${CE_SHA}.tar.gz"     | tar -xz -C "$temp"
  node "$temp/communication-engineering-${CE_SHA}/bin/communication-engineering.mjs" "$command" --root "$ROOT" ${2:-}
  rm -rf "$temp"
}

echo "Installing ROS 3.1.4"
npx --yes --package=@echelon-foundry/repository-operating-system@3.1.4 ros init

echo "Installing SDE 1.3.0"
npx --yes @echelon-foundry/sde@1.3.0 init

echo "Installing Visual Engineering 1.0.0"
npx --yes @echelon-foundry/visual-engineering@1.0.0 init

echo "Installing Communication Engineering from pinned current source"
run_ce init

echo "Installing Limen 0.6.2"
npx --yes @echelon-foundry/typescript-wasm-kernel@0.6.2 init

echo "Verifying capability installations"
npx --yes --package=@echelon-foundry/repository-operating-system@3.1.4 ros verify --strict
npx --yes @echelon-foundry/sde@1.3.0 verify
npx --yes @echelon-foundry/visual-engineering@1.0.0 verify --strict
run_ce verify "--strict"
npx --yes @echelon-foundry/typescript-wasm-kernel@0.6.2 verify --strict

echo "Capability bootstrap complete. ROS registry/validation should run after the generated state is committed."
