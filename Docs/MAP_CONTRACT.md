# Training Arena 맵 계약 v1

정본: `Shared/Map/Data/training-arena.json`. 맵 ID는 `training-arena`, 이름은 Training Arena다. Editor 생성기는 이 파일만 읽어 Scene, GameObjects, Ramp Mesh, Materials, Colliders, SpawnPoints, Lighting, MapConfiguration을 만든다. 서버 프로젝트는 같은 파일을 빌드 출력의 `Maps/training-arena.json`으로 복사한다.

## 좌표와 필드

미터 단위, Y-up, +Z-forward, Unity와 같은 좌표계다. `position`은 객체 경계 상자의 중심이고 `size`는 전체 폭/높이/깊이다. `rotation`은 도 단위 Euler 값이다. 스폰의 `position`만 플레이어 발 위치다.

| 필드 | 의미 |
|---|---|
| schemaVersion, id, displayName | 버전과 맵 식별 |
| arenaSize | X/Z 80m, 허용 구조물 최대 높이 24m |
| materials[] | ID, RGB, metallic, smoothness, emission |
| objects[].id, group | 고유 객체 ID, 생성 계층 그룹 |
| objects[].shape | Box 또는 Ramp |
| objects[].colliderType | Aabb, Ramp, None |
| position, rotation, size | 시각 형상과 충돌에 공동 사용 |
| objects[].material | materials ID 참조 |
| spawns[].id, team, position, yaw | 팀 0/1의 발 위치와 바라보는 방향 |

별도 colliderSize를 두지 않는다. 시각 크기와 충돌 크기를 의도적으로 하나의 size로 묶어 불일치를 방지한다. None은 바닥 표시선, 발광 패널 같은 장식에만 사용한다.

## 지원 기하

**Box / Aabb:** 회전 (0,0,0)만 허용한다. Unity는 Cube + BoxCollider, 서버/Shared는 중심 ± size/2의 AABB로 취급한다.

**Ramp / Ramp:** X/Z 회전은 0, yaw는 0/90/180/270도만 허용한다. 로컬 -Z 끝은 낮고 +Z 끝은 높다. 로컬 점의 실체 내부 조건은 다음과 같다.

```text
abs(x) <= width / 2
abs(z) <= length / 2
-height / 2 <= y <= height / 2
y <= z * height / length
```

같은 정점/삼각형 목록으로 Unity의 정적 wedge MeshCollider를 생성한다. Shared는 위의 반공간을 이용해 ray, 점 포함, 표면 높이를 계산한다. 일반적인 임의 MeshCollider 지원을 의미하지 않는다. 이동 sweep/접지/경사 이동은 Phase 4 범위이며 현재 함수만으로 FPS 이동이 완성됐다고 간주하지 않는다.

**계단:** 0.25m 높이 × 0.75m 진행 길이의 AABB 16개로 4m 고지대에 연결한다. 예정 step height는 0.3m다. 계단 양방향 이동의 실제 검증은 Phase 4다.

## 배치

- 바닥은 80m × 80m이며 표면 Y=0이다. 둘레는 8m 높이 벽으로 닫는다.
- 팀 0은 Z=-32, 팀 1은 Z=+32에 스폰 4개씩 둔다. 스폰 발 위치 Y=0.05다.
- 중앙 reactor와 낮은 엄폐, 좌우 baffle이 시선을 분리한다.
- X=±27의 갤러리와 Z=±14의 다리는 표면 Y=4다. 다리 아래도 이동할 수 있다.
- 서남/동북 경사로와 동남/서북 계단이 지상과 2층을 연결한다.
- 양쪽 외곽 지상 우회로는 플레이어 몸체 여유 폭을 자동 검사한다.
- 푸른 팀/주황 팀 표시, 금속·콘크리트 색 구분, 발광 패널과 라이트를 사용한다.

## 검증과 재생성

공유 검증기는 미지원 schema/shape/회전/collider, NaN/Infinity, 비양수 size, 중복 ID, 누락 material, 범위 밖 구조물, 잘못된 팀, 부유·겹침 스폰을 거부한다. 스폰의 ramp 겹침 판정은 보수적인 bounding box 검사다. 맵 파일은 최대 2 MiB, 객체 수 4096개로 제한한다.

Editor는 맵 검증 후 새 Scene을 만들고 같은 경로의 Material/Mesh/MapConfiguration을 재사용한다. 동일 JSON에서 생성 객체·기하·스폰·재질과 자산 GUID 집합이 같은지 두 번 생성하여 검사한다. Unity가 내부 fileID를 직렬화하는 방식까지 바이트 단위 동일성을 보장하는 것은 아니다.

`Assets/Generated/TrainingArena`는 생성기 소유 디렉터리다. 여기에 수동 자산을 추가하지 않는다. 재생성은 현재 씬을 교체하므로 메뉴 실행 시 저장되지 않은 씬을 저장할 기회를 제공한다. 프로젝트를 복제한 뒤 메뉴에서 한 번 생성하거나 검증 스크립트를 실행한다.

맵을 수정할 때는 정본 JSON을 UTF-8/LF로 저장하고 .NET 테스트/Editor 검증을 모두 다시 실행한다. `.gitattributes`가 checkout의 줄바꿈을 LF로 고정한다. 해시는 원본 바이트를 사용하므로 공백/줄바꿈 변경도 다른 맵으로 취급한다. 생성된 자산은 Git에 넣지 않으며 소스 `.meta` 파일은 유지한다.
