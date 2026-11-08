#!/usr/bin/env bash
# 검사 → 빌드 → 시험. 윈도우에서는 전체 솔루션, 그 밖에서는 윈도우 전용 프로젝트를 뺀 ci.slnf.
set -euo pipefail
cd "$(dirname "$0")"

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) target="CyberpunkNetrunnerSolver.slnx" ;;
  *) target="ci.slnf" ;;
esac

echo "== verify: $target"
# CI(GitHub Actions는 CI=true)에서는 packages.lock.json과 다르면 실패한다. 로컬에서는 잠금 파일을 갱신한다.
dotnet restore "$target" ${CI:+--locked-mode}
dotnet format "$target" --verify-no-changes --no-restore
dotnet build "$target" -c Release --no-restore -warnaserror
dotnet test "$target" -c Release --no-build
