#!/usr/bin/env bash
# 검사 → 빌드 → 시험. 윈도우에서는 전체 솔루션, 그 밖에서는 윈도우 전용 프로젝트를 뺀 ci.slnf.
set -euo pipefail
cd "$(dirname "$0")"

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) target="CyberpunkNetrunnerSolver.slnx" ;;
  *) target="ci.slnf" ;;
esac

echo "== verify: $target"
dotnet restore "$target"
dotnet format "$target" --verify-no-changes --no-restore
dotnet build "$target" -c Release --no-restore -warnaserror
dotnet test "$target" -c Release --no-build
