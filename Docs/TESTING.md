# 실행과 테스트

필수 도구: .NET 10 SDK, 활성화된 Unity 6000.3.11f1, Git. Unity는 프로젝트 루트가 아닌 `Client` 폴더를 연다. Unity Hub 라이선스 활성화는 Editor 실행에 필요하다.

## .NET

저장소 루트에서 실행한다.

```powershell
dotnet build FPSPrototype.slnx -c Release
dotnet run --project Tests/FPSPrototype.Tests.csproj -c Release
dotnet run --project Server/FPSPrototype.Server.csproj -c Release -- --validate-map
```

테스트는 실행형 harness이며 실패 시 종료 코드 1을 반환한다. `dotnet test`만 실행하면 이 검사를 수행하지 않는다. 정상 결과는 `RESULT: 29 passed, 0 failed`다. 맵 검증 CLI는 객체/스폰 개수와 SHA-256을 출력한 뒤 종료한다. 현재 단계에서 계속 대기하는 UDP 서버는 구현하지 않았다.

테스트 내용: 맵 데이터 계약, 잘못된 입력 거부, 팀 스폰, AABB와 4방향 Ramp의 표면/ray/내부 검사, wedge 정점 winding, 우회로 몸체 여유 공간, 다리 연결, 계단 높이, 외곽 경계, Unity 참조 부재.

## Unity Editor

```powershell
pwsh -File Tools/Validate-Unity.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'
```

스크립트는 Editor를 별도로 실행하여 C# import/컴파일, 두 번 생성, GUID/기하 fingerprint 비교, 각 Collider의 9개 표면 샘플을 Shared raycast와 대조, Ramp 옆면/빈 공간 대조, Play Mode 진입, 스폰 overlap/지면 ray 검사, 세 시점의 PNG 캡처를 수행한다. 결과는 `Artifacts/phase1-validation.json`과 `Artifacts/unity-validation.log`에 저장한다. 유효한 passed 보고서가 없으면 성공으로 반환하지 않는다. 최대 6분을 기다린다.

프로젝트가 이미 다른 Editor에서 열려 있으면 그 Editor를 종료하거나 메뉴의 `FPS Prototype > Validate Phase 1 (includes Play Mode)`를 사용한다. 수동 검증 전에 작업 중인 씬을 저장한다.

관찰 방법:

1. Unity Hub에서 `Client` 프로젝트를 연다.
2. `FPS Prototype > Generate Training Arena`를 실행한다.
3. `Assets/Generated/TrainingArena/TrainingArena.unity`가 열린다.
4. Play를 누른다. 우클릭을 누른 채 WASD 이동, Q/E 상승·하강, Shift 가속, Home 전체 조망, End 스폰 시점이다.
5. 현재 카메라는 맵 검사용 자유 비행이며 서버와 연결되지 않는다. FPS 이동/점프/총기는 후속 Phase에서 구현한다.

Unity command-line의 `-executeMethod`를 사용한다. Play Mode 완료 후 코드에서 종료하므로 `-quit`을 함께 주지 않는다. PNG 캡처가 필요하므로 `-nographics`도 사용하지 않는다. [Unity CLI 문서](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/run-unity)

## Editor를 실행할 수 없을 때

Unity 참조 라이브러리 설치만 있다면 다음으로 C# API 호환성을 추가 검사할 수 있다.

```powershell
dotnet build Tools/UnityCompileCheck/UnityCompileCheck.csproj -c Release
# 다른 설치 경로:
dotnet build Tools/UnityCompileCheck/UnityCompileCheck.csproj -c Release '-p:UnityManagedDir=D:/Unity/Editor/Data/Managed'
```

이 검사는 .NET 컴파일러가 설치된 Unity DLL을 참조하는 검사다. Unity의 package import, asmdef pipeline, Scene 생성, 렌더링, Physics 또는 Play Mode 실행 성공을 증명하지 않는다. 원인과 미검증 항목을 `Docs/VALIDATION.md`에 남겨야 한다.

## 최종 2클라이언트 수용 시나리오 (Phase 7)

| 번호 | 실제 실행 | 합격 기준 | Phase 1 상태 |
|---|---|---|---|
| 1 | Dedicated Server 실행 | UDP listen, 30 TPS, Unity 없음 | 미구현 |
| 2 | Unity A 실행/접속 | 세션 승인, 팀/ID 부여 | 미구현 |
| 3 | Unity B 실행/접속 | 별도 세션/ID 부여 | 미구현 |
| 4 | 두 플레이어 맵 생성 | A/B에서 두 캐릭터 확인 | 미구현 |
| 5 | 양쪽에서 이동 | 같은 서버 위치로 동기화 | 미구현 |
| 6 | 벽/계단/경사로 이동 | 관통 없이 이동/접지 | 이동 솔버 미구현 |
| 7 | 상대 사격 | 서버만 HP 감소 결정 | 미구현 |
| 8 | HP 0 | 사망 1회/점수 반영 | 미구현 |
| 9 | 리스폰 대기 | 서버 timer 후 복원 | 미구현 |
| 10 | 서버 종료 | A/B 모두 연결 해제 표시 | 미구현 |

실제 Unity A/B 검사는 headless 프로토콜 테스트와 별개로 기록한다. 화면/로그/버전/명령과 손실·지연 설정을 함께 보관한다.
