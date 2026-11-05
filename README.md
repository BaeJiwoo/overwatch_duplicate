# FPSPrototype — Training Arena

Unity 6 클라이언트와 Unity 없이 실행되는 .NET 10 서버를 목표로 하는 독창적인 3D 멀티플레이어 FPS 프로토타입입니다. 현재 범위는 **Phase 1: 프로젝트 기반과 맵 자동 생성**입니다. UDP 연결·플레이어 이동·전투 기능은 아직 구현하지 않았습니다.

- [전체 계획과 단계별 Issue](Docs/PLAN.md)
- [실행 및 테스트 절차](Docs/TESTING.md)
- [실제 검증 결과와 미검증 사항](Docs/VALIDATION.md)
- [공통 맵/충돌 계약](Docs/MAP_CONTRACT.md)
- [개발 규칙](AGENTS.md)

## 빠른 시작

필수 버전: Unity **6000.3.11f1**, .NET **10 SDK**. Shared는 **.NET Standard 2.1**입니다.

```powershell
dotnet build FPSPrototype.slnx -c Release
dotnet run --project Tests/FPSPrototype.Tests.csproj -c Release
dotnet run --project Server/FPSPrototype.Server.csproj -c Release -- --validate-map
```

Unity Hub에서 `Client` 폴더를 열고 **FPS Prototype → Generate Training Arena**를 선택합니다. 생성된 Scene에서 Play를 누르면 맵 관찰 카메라를 사용할 수 있습니다. 우클릭+WASD로 자유 비행, Q/E 높이 변경, Shift 가속, Home 전체 조망, End 스폰 시점입니다. 이 카메라는 Phase 1 관찰 도구입니다.

전체 Editor 검증:

```powershell
pwsh -File Tools/Validate-Unity.ps1
```

## Training Arena

80m × 80m 공간에 팀별 스폰 4개, 중앙 교전 지역, 지상 양측 우회로, 높이 4m의 갤러리와 연결 다리, 계단 2개와 경사로 2개, 엄폐와 외곽 벽을 구성합니다. 푸른색/주황색 진영과 SF 재질을 사용하며 외부 유료 에셋이 필요하지 않습니다.

정본 JSON은 `Shared/Map/Data/training-arena.json`입니다. Editor와 서버가 같은 데이터와 단순 기하 코드를 읽습니다. 생성 Scene/자산은 `Client/Assets/Generated/TrainingArena`에 만들어지며 재생성이 가능합니다.

## 다음 단계

[Phase 1 Issue #2](https://github.com/BaeJiwoo/overwatch_duplicate/issues/2)의 PR 검토 이후 [Phase 2 Issue #3](https://github.com/BaeJiwoo/overwatch_duplicate/issues/3)에서 LiteNetLib UDP와 30 TPS 독립 서버를 구현합니다. 모든 단계는 Issue → branch → 구현 → 테스트 → PR → 사용자 검토 순서이며 자동으로 병합하지 않습니다.
