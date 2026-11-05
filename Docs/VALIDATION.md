# Phase 1 실제 검증 결과

검증일: **2026-11-05 (Asia/Seoul)**. Phase 1 구현은 PR 검토 대상이며, 아래 미검증 항목이 남아 있으므로 전체 완료로 표시하지 않는다.

## 환경

- Windows 11 x64
- .NET SDK 10.0.403 / .NET runtime 10.0.14
- Unity Editor 6000.3.11f1 설치 확인
- Shared: netstandard2.1 / C# 9
- 맵: 80m × 80m, 객체 222개, 스폰 8개
- Map SHA-256 (UTF-8 / LF): `cf19ef292e38f64395f0a4ff7511c00fce792607fc1394c9dbc75e69b7fd45ff`

## 실행 확인

| 검사 | 결과 | 실제 확인 범위 |
|---|---|---|
| `dotnet build FPSPrototype.slnx -c Release -m:1` | 통과, 경고/오류 0 | Shared/Server/Tests 컴파일 |
| .NET 테스트 실행 | **29 passed, 0 failed** | 잘못된 맵 거부, 순수 C# 기하, 스폰/경로/계단/외벽 |
| 서버 `--validate-map` | 통과, 종료 코드 0 | Unity 없이 동일 JSON 로드/검증/해시 |
| `dotnet build Tools/UnityCompileCheck/UnityCompileCheck.csproj -c Release -m:1` | 통과, 경고/오류 0 | 설치된 Unity DLL을 참조한 C# API 컴파일 검사 |
| Unity Editor 실제 실행 시도 | **실패, 종료 코드 198** | 라이선스 단계에서 종료, 프로젝트 import 전 |
| GitHub Actions / Ubuntu 24.04 | **통과** | solution build, 29개 테스트, 서버 맵 검증 |

원격 CI 근거: [Shared and server checks #2](https://github.com/BaeJiwoo/overwatch_duplicate/actions/runs/41951214895), 구현 커밋 `f5cfdf982e4f3f79060b12f912123ab0f005a0ba`. 원격 CI도 Unity Editor/Play Mode는 실행하지 않았다.

우회로 몸체 여유 공간 테스트가 최초에 실패하여 외곽 건물을 1m 안쪽으로 이동했고, 같은 테스트가 통과하는 것을 확인했다.

로컬 실행 원본 로그는 `Artifacts/dotnet-build.log`, `Artifacts/dotnet-tests.log`, `Artifacts/server-map-validation.log`, `Artifacts/unity-api-compile.log`, `Artifacts/unity-validation.log`에 있다. 환경 정보가 포함된 로컬 로그 전체는 커밋하지 않는다.

## Unity 실행 제한

실제 Editor 실행 로그의 원인:

```text
No valid Unity Editor license found. Please activate your license.
Application will terminate with return code 198
```

따라서 다음은 **미검증**이다.

- Unity Package Manager import와 asmdef 기반 컴파일
- Scene/Material/Mesh/Collider/SpawnPoint/MapConfiguration 실제 생성
- 두 번 생성했을 때 기하/자산 GUID 일치
- Unity Physics와 Shared 기하의 표면·옆면 대조
- Play Mode의 관찰 카메라, 바닥 ray, 스폰 overlap 검사
- 조명·색상·렌더링의 시각 확인 및 실제 스크린샷

검증 코드는 `Client/Assets/Editor/Phase1Validation.cs`와 `Tools/Validate-Unity.ps1`에 작성했다. Unity Hub에서 유효한 라이선스를 활성화한 후 `pwsh -File Tools/Validate-Unity.ps1`을 실행한다. 이 기록은 실제 보고서와 화면을 확인한 후 갱신해야 한다. 오프라인 C# 컴파일 결과를 Editor/Play Mode 성공으로 대체하지 않는다.

## 아직 구현하지 않은 범위

LiteNetLib UDP, 임시 세션, 30 TPS 실행 루프, 서버 플레이어 이동 솔버, FPS 점프/사격/탄약/HP/리스폰, 예측·보간·재조정, Docker 및 Unity A/B 통합 플레이는 Phase 2~7 Issue의 범위다. 서버 CLI의 맵 검증 성공은 이 기능들의 실행 성공을 의미하지 않는다.
