#!/usr/bin/env bash
set -euo pipefail

echo "Installing ROS 3.1.4"
npx --yes --package=@echelon-foundry/repository-operating-system@3.1.4 ros init

echo "Installing SDE 1.3.0"
npx --yes @echelon-foundry/sde@1.3.0 init

echo "Installing Visual Engineering 1.0.0"
npx --yes @echelon-foundry/visual-engineering@1.0.0 init

echo "Installing Communication Engineering from pinned current source"
npm exec --yes --package=github:kemiller2002/communication-engineering#4590d2fe6f7e80b339117d3fbee5803f2dd39122 communication-engineering -- init

echo "Installing Limen 0.6.2"
npx --yes @echelon-foundry/typescript-wasm-kernel@0.6.2 init

echo "Verifying capability installations"
npx --yes --package=@echelon-foundry/repository-operating-system@3.1.4 ros verify --strict
npx --yes @echelon-foundry/sde@1.3.0 verify
npx --yes @echelon-foundry/visual-engineering@1.0.0 verify --strict
npm exec --yes --package=github:kemiller2002/communication-engineering#4590d2fe6f7e80b339117d3fbee5803f2dd39122 communication-engineering -- verify --strict
npx --yes @echelon-foundry/typescript-wasm-kernel@0.6.2 verify --strict

echo "Capability bootstrap complete. ROS registry/validation should run after the generated state is committed."
