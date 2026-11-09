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

dotnet publish "$project" -c Release -r win-x64 --self-contained false "$lock" \
  -p:PublishSingleFile=true -p:DebugType=none \
  -o publish/framework-dependent

dotnet publish "$project" -c Release -r win-x64 --self-contained true "$lock" \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o publish/self-contained

for dir in publish/framework-dependent publish/self-contained; do
  echo "$dir: $(ls "$dir")  $(du -sh "$dir" | cut -f1)"
done
