# 전체 개발 계획

목표는 독창적인 SF 전장 Training Arena에서 2~4명이 플레이하는 서버 권위형 FPS다. 현재 PR은 Phase 1에 한정한다. 전체 추적: [로드맵 #1](https://github.com/BaeJiwoo/overwatch_duplicate/issues/1).

| 단계 | Issue | 구현 범위 | 다음 단계로 넘어가는 기준 |
|---|---|---|---|
| 1 | [#2](https://github.com/BaeJiwoo/overwatch_duplicate/issues/2) | 구조/Unity 설정/공통 맵/자동 생성 | .NET 검사, Editor 생성·재생성·Play Mode, 사용자 PR 검토 |
| 2 | [#3](https://github.com/BaeJiwoo/overwatch_duplicate/issues/3) | LiteNetLib UDP/30 TPS/세션/연결 종료 | 독립 서버와 2~4개 클라이언트의 join/leave/timeout 검증 |
| 3 | [#4](https://github.com/BaeJiwoo/overwatch_duplicate/issues/4) | 팀 생성/입력 sequence/Snapshot/기본 이동 | 두 Unity 클라이언트의 생성·이동·제거 일치 |
| 4 | [#5](https://github.com/BaeJiwoo/overwatch_duplicate/issues/5) | WASD/마우스/점프/중력/벽·계단·경사 충돌 | 서버 이동 검증과 실제 맵 양방향 이동 |
| 5 | [#6](https://github.com/BaeJiwoo/overwatch_duplicate/issues/6) | 서버 소총/탄약/재장전/HP/리스폰/점수 | 두 클라이언트 전투와 서버 악성 입력 방어 검사 |
| 6 | [#7](https://github.com/BaeJiwoo/overwatch_duplicate/issues/7) | 보간/예측/재조정/손실/제한된 rewind | RTT 0~200ms, loss 0~10%에서 수렴·권위 유지 |
| 7 | [#8](https://github.com/BaeJiwoo/overwatch_duplicate/issues/8) | Docker/UDP 배포/통합 테스트/운영 문서 | 실제 서버 + Unity A/B 전체 시나리오 통과 |

각 단계는 이전 단계의 검증 결과와 사용자 PR 검토를 전제로 한다. Phase 6 안에서도 보간 → 예측/재조정 → 손실 처리 → rewind 순서로 진행한다. 테스트가 실패하거나 실행 환경이 없으면 해당 완료 조건을 열린 상태로 유지한다.

## 프로젝트 구조

```text
FPSPrototype/
├── Client/                  Unity 6.3 프로젝트
│   ├── Assets/Editor/       맵 생성과 Editor/Play Mode 검사
│   ├── Assets/Scripts/      맵 설정, 스폰, 관찰 카메라
│   ├── Packages/            Shared 로컬 패키지 참조
│   └── ProjectSettings/
├── Server/                  .NET 10; Phase 1은 맵 검증 CLI
├── Shared/                  netstandard2.1 / UPM, 같은 C# 소스
│   ├── Network/             프로토콜 버전 예약
│   ├── Game/                공통 크기/정책
│   └── Map/                 DTO/검증/단순 기하/정본 JSON
├── Tests/                   외부 테스트 패키지 없는 실행형 검사
├── Tools/                   Unity 검증 및 오프라인 컴파일 검사
├── Docs/
├── AGENTS.md
└── README.md
```

## 기술 경계

- Unity 6000.3.11f1, 기본 렌더 파이프라인, Standard/Procedural Skybox 셰이더. 유료/외부 아트 에셋 불필요.
- Shared는 C# 9 / .NET Standard 2.1이다. Unity가 .NET 10 코드를 직접 실행한다고 가정하지 않는다. [Unity API 호환성](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/overview-of-dot-net-in-unity/dotnet-profile-support)
- 서버는 .NET 10을 사용하고 Unity 라이브러리를 참조하지 않는다. Shared를 ProjectReference로, Unity는 로컬 UPM 패키지로 참조한다. [Unity 로컬 패키지 경로](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/dependencies-lp/upm-localpath)
- LiteNetLib의 실제 버전 선택·잠금·패키징·라이선스 고지는 Phase 2에서 진행한다. Phase 1에는 동작하지 않는 가짜 UDP 서버를 두지 않는다.
- JSON은 양쪽 실행 환경에서 같은 필드 DTO로 읽으며 공유 검증기를 통과해야 한다. 서버는 System.Text.Json, Unity는 JsonUtility를 사용한다. 현재 맵은 외부 클라이언트가 전송하는 데이터가 아닌 신뢰된 배포 자산이다.
- JSON 원본 바이트의 SHA-256, schema version, protocol version을 Phase 2 접속 handshake에서 비교한다.

## 서버와 프로토콜 설계 예정

단조 시계 기반 30 TPS 서버 루프에 peer 이벤트와 입력 큐를 전달한다. 네트워크 이벤트 처리와 게임 상태 변경은 단일 소유 스레드로 직렬화한다. catch-up tick 상한과 지연 계측을 두고 과부하 시 무한 누적을 방지한다.

클라이언트는 이동 축, yaw/pitch, jump/fire/reload 버튼, 입력 sequence만 보낸다. 서버가 입력 범위·빈도·순서·세션 소유권을 검증하고 위치·속도·HP·탄약·점수와 마지막 처리 sequence를 Snapshot에 기록한다. 최종 패킷 형식은 Phase 2~3 Issue에서 고정한다.

임시 세션 ID는 암호학적 난수이며 연결된 peer에 결합한다. 이것은 계정 인증이나 UDP 암호화를 의미하지 않는다. 초기 범위는 LAN 프로토타입이다. timeout 이후 입력은 중립화하고 재접속에는 새 세션을 발급한다.

플레이어의 이동 몸체는 폭 0.7m, 높이 1.8m이며 눈 높이는 1.6m다. 플레이어 간 이동 충돌은 없고 사격 hitbox는 별도다. Phase 4에서 sweep/slide/step-up과 ramp 이동을 구현하고, Phase 6에서 같은 이동 함수를 예측에 재사용한다.

사격은 서버 tick의 발사 간격/탄약/재장전/생존 상태로 제한한다. 서버 눈 위치와 검증된 시점으로 ray를 발사해 가장 가까운 맵/타깃을 비교한다. 팀 피해와 자해는 기본 off다. 리스폰은 서버 timer가 처리하며 상태와 속도를 초기화한다.

## 네트워크 개선 예정

기본 Snapshot 표시를 먼저 검증한다. 이후 원격 플레이어는 시간 buffer로 보간하고, 로컬 플레이어는 ACK 이후 입력을 replay하여 재조정한다. 입력은 제한된 최근 이력을 중복 전송한다. 오래된 Snapshot/중복 입력/sequence wrap/재접속 세대와 큐 크기 한도를 명시적으로 처리한다.

RTT 0/50/100/200ms 및 loss 0/1/5/10% 조합에서 움직임 수렴, 사격 중복 방지, 버퍼 상한, tick 부하를 기록한다. rewind는 최대 200ms의 검증된 서버 시간만 허용하며 사망/리스폰 경계를 넘는 history 오용을 방지한다. 정적 맵 occlusion은 계속 유지한다.

## GitHub 운영

Issue → `issue-번호-이름` branch → 구현 → 테스트 → PR → 사용자 검토 순서다. 자동 merge와 자동 merge 예약은 사용하지 않는다. 빈 저장소의 main은 PR 기준점용 최소 README로 초기화했으며 구현은 Phase 1 브랜치에만 둔다. PR에는 실제 결과, 미검증 항목, 실행 절차를 포함한다.
