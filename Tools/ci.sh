#!/usr/bin/env bash
# STEP 1. 기반 도구 - CI 검증 스크립트.
# Unity 없이 CLI 에서 도는 검증 게이트 (ARCHITECTURE.md 9, 11).
# 로컬에서도 그대로 실행한다: bash Tools/ci.sh
set -euo pipefail

repoRoot="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repoRoot"

echo "===================================================="
echo " SYNTHESIS CI - STEP 1 게이트"
echo "===================================================="

# ---- 1) Core 는 UnityEngine 을 참조하지 않는다 (ARCHITECTURE.md 1, 3 - 절대 규칙) ----
# using 지시문만 검사한다(주석 속 'UnityEngine.Random 금지' 같은 문구는 오탐이므로 제외).
# 완전수식 참조는 아래 2단계 netstandard2.1 빌드가 UnityEngine 미해결로 최종 차단한다.
echo ""
echo "[1/4] Core UnityEngine 미참조 검사"
if grep -rnE "^[[:space:]]*using[[:space:]]+UnityEngine" --include=*.cs Shared/Synthesis.Core ; then
    echo "  FAIL: Shared/Synthesis.Core 가 UnityEngine 을 using 합니다. Core 는 순수 C# 이어야 합니다."
    exit 1
fi
echo "  OK: Core 에 UnityEngine using 없음"

# ---- 2) 빌드 ----
echo ""
echo "[2/4] dotnet build"
dotnet build Synthesis.slnx -c Release --nologo -v q

# ---- 3) 테스트 (결정성, 파서 일치, 고정소수점) ----
echo ""
echo "[3/4] dotnet test"
dotnet test Synthesis.slnx -c Release --nologo -v q

# ---- 4) 불변식 린터 (INV-01 부터 INV-10) ----
echo ""
echo "[4/4] 불변식 린터"
dotnet run --project Tools/Linter/Synthesis.Linter.csproj -c Release -- ./Data

echo ""
echo "===================================================="
echo " CI 게이트 통과"
echo "===================================================="
