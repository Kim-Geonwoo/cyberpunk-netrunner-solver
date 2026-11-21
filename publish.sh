#!/usr/bin/env bash
# Windows용 단일 exe 두 가지를 publish/ 아래에 만든다.
#   framework-dependent: 작다. .NET 10 데스크톱 런타임이 필요하다.
#   self-contained:      런타임을 포함한다. 아무 Windows 10(2004 이상)·11 PC에서 바로 실행된다.
set -euo pipefail
cd "$(dirname "$0")"

project=src/NetrunnerSolver.App/NetrunnerSolver.App.csproj
mkdir -p publish

# win-x64·단일 파일 복원은 잠금 파일에 RID 항목과 SDK 버전을 따르는 ILLink를 더한다.
# 커밋된 packages.lock.json을 건드리지 않도록 발행용 잠금 파일은 각 프로젝트의 obj/에 따로 둔다.
lock=-p:NuGetLockFilePath=obj/publish.packages.lock.json

# 같은 커밋·같은 SDK면 어느 PC에서 빌드해도 같은 exe가 나오도록 소스 경로를 정규화한다.
ci=-p:ContinuousIntegrationBuild=true

dotnet publish "$project" -c Release -r win-x64 --self-contained false "$lock" "$ci" \
  -p:PublishSingleFile=true -p:DebugType=none \
  -o publish/framework-dependent

dotnet publish "$project" -c Release -r win-x64 --self-contained true "$lock" "$ci" \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o publish/self-contained

# 배포한 exe가 어느 소스에서 나왔는지 확인할 수 있게 커밋·SDK·해시를 남긴다.
# 같은 커밋을 같은 SDK로 다시 빌드해 해시를 비교하면 된다.
dirty=""
if [ -n "$(git status --porcelain --untracked-files=no 2>/dev/null)" ]; then
  dirty=" dirty"
fi
{
  echo "commit $(git rev-parse HEAD 2>/dev/null || echo unknown)$dirty"
  echo "sdk $(dotnet --version)"
  sha256sum publish/framework-dependent/NetrunnerSolver.App.exe publish/self-contained/NetrunnerSolver.App.exe
} > publish/SHA256SUMS.txt

for dir in publish/framework-dependent publish/self-contained; do
  echo "$dir: $(ls "$dir")  $(du -sh "$dir" | cut -f1)"
done
cat publish/SHA256SUMS.txt
