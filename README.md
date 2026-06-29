# Slide Block

개인 개발 Android 퍼즐 게임. Unity 6 기반 솔로 인디 프로젝트.

---

## 개요

7×7 격자판에서 블록을 슬라이딩하여 같은 색 블록을 3개 이상 이어붙이는 퍼즐 게임.  
Google Play 출시 목표로 개발 중.

- **개발자:** OortCloud98
- **플랫폼:** Android (Google Play)
- **엔진:** Unity 6+
- **언어:** C# / SCSS (GitHub Pages)

---

## 기술 스택

| 분류 | 패키지 |
|---|---|
| 비동기 | UniTask |
| 트윈 | DOTween |
| 에셋 관리 | Addressables |
| 백엔드 | Firebase (Auth, Firestore, Analytics, Crashlytics, Messaging) |
| 광고 | AdMob (Banner + Interstitial) |
| 로그인 | Google Play Games Services |
| 렌더 파이프라인 | URP |

---

## 프로젝트 구조

```
Assets/Scripts/
├── Bootstrap.cs               # 리플렉션 기반 매니저 자동 초기화
├── Core/                      # 핵심 매니저 클래스
│   ├── GameManager.cs
│   ├── FirebaseManager.cs     # Auth + Firestore + Analytics + Crashlytics + Messaging 통합
│   ├── AdmobManager.cs
│   ├── SoundManager.cs
│   ├── InputManager.cs
│   ├── PrefabManager.cs
│   ├── ReferenceManager.cs
│   ├── Attribute/             # [ManagerOrder] 어트리뷰트
│   ├── Core_Resource/         # ScriptableObject 리소스
│   ├── Input/
│   ├── Interface/             # IManager
│   └── ScriptableObject/
│
├── Game/                      # 게임 로직
│   ├── Board.cs               # 7×7 보드 상태 관리
│   ├── Brick.cs               # 개별 블록
│   ├── RoundManager.cs        # 라운드 흐름 제어
│   ├── RoundObject.cs
│   ├── GameCamera.cs
│   └── Data/
│       └── BoardArea.cs
│
├── UI/                        # UI 레이어
│   ├── BaseUI.cs
│   ├── MenuUI.cs
│   ├── InGameUI.cs
│   ├── GameLobbyUI.cs
│   ├── GameOverUI.cs
│   ├── IngameScoreUI.cs
│   ├── IngameScoreObject.cs
│   ├── HighScoreObject.cs
│   ├── ComboObject.cs
│   ├── ToastCombo.cs
│   ├── PopupQuestionUI.cs
│   ├── LegalUI.cs             # 인앱 법적 문서 뷰어
│   ├── SafeAreaFitter.cs
│   └── Particle/
│       ├── BaseParticle.cs
│       ├── BaseParticlePlayer.cs
│       └── BrickParticle.cs   # Update 기반 Tick 패턴, O(1) swap-back 풀링
│
├── Share/                     # 어셈블리 공유 타입
│   ├── SingletonInstance.cs   # 제네릭 싱글톤 베이스
│   ├── UserData.cs            # 유저 데이터 모델
│   ├── SaveFieldData.cs
│   ├── Enum/
│   │   ├── BrickType.cs
│   │   ├── SoundData.cs
│   │   ├── PrefabData.cs
│   │   ├── SaveFieldType.cs
│   │   ├── ContainLabel.cs
│   │   └── InputType.cs
│   └── Interface/
│
├── Utility/                   # 순수 정적 유틸리티
│   ├── Colors.cs              # 색상 팔레트 (Sets[])
│   ├── ResolutionScreen.cs    # PlayerLoop 기반 해상도 감지 (TK 네임스페이스)
│   ├── Timer.cs
│   ├── LLogger.cs             # 조건부 로그 래퍼
│   ├── Utility.cs
│   ├── EnumConverter.cs
│   └── VibrateData.cs         # 햅틱 (Android Java interop)
│
├── ToolKit/                   # 인하우스 SDK
│   ├── Component/
│   │   └── SlideToggle.cs
│   └── SDK/
│       └── BrickColorEditor/  # 에디터 색상 편집 도구
│
└── Editor/                    # 에디터 전용
    ├── BuildProcessor.cs      # AndroidBuilder - AAB 빌드, 시맨틱 버저닝, 스크립팅 심볼
    ├── SaveFieldDataGenerator.cs
    ├── PlayerPrefsEditor.cs
    ├── AssetReferenceBaseEditor.cs
    ├── UIPrefabPostprocessor.cs
    └── ChangeMaterial.cs
```

---

## 주요 시스템

### Bootstrap / 싱글톤
- `SingletonInstance<T>` 베이스 클래스
- `Bootstrap.cs`에서 리플렉션으로 `[ManagerOrder(n)]` 어트리뷰트를 읽어 초기화 순서 보장
- 씬 진입 시 자동 인스턴스화

### Firebase
- `FirebaseManager` 단일 클래스에 Auth / Firestore / Analytics / Crashlytics / Messaging 통합
- Firestore 2-컬렉션 구조
  - `users/{userId}` — private (본인만 읽기/쓰기)
  - `leaderboard/{userId}` — public-read / owner-write

### AdMob
- Banner: 로비 하단 고정
- Interstitial: 게임오버 시 노출

### 빌드 (AndroidBuilder)
- `BuildProcessor.cs` 커스텀 에디터 윈도우
- Major.Minor.Patch 시맨틱 버저닝, Patch 자동 증가
- Bundle Version Code 자동 증가
- 스크립팅 심볼 프리셋: `ENABLE_LOG` / `DISABLE_ADS` / `CHEAT_MODE`

### 파티클 풀
- `BaseParticle.Tick(float dt): bool` — Update 루프에서 호출, `false` 반환 시 종료
- O(1) swap-back 제거로 GC 최소화

### 햅틱
- `VibrateData.cs` + Android Java interop
- `PlayHaptic(HapticType)` 래퍼로 HapticFeedbackConstants 매핑

### 해상도 감지
- `ResolutionScreen` 정적 클래스 (TK 네임스페이스)
- Unity PlayerLoop `PostLateUpdate`에 등록, Subscribe/Unsubscribe 패턴

---

## 어셈블리 구조

```
Share       ← 공유 타입 (Enum, Interface, UserData)
    ↑
Utility     ← 순수 정적 유틸
Core        ← 매니저 (Share, Utility 참조)
Game        ← 게임 로직 (Core 참조)
UI          ← UI (Game, Core 참조)
TK          ← 인하우스 SDK
SDK         ← 에디터 도구 (TK 하위)
```

---

## 법적 문서

GitHub Pages (`https://cloud1202.github.io/`)에 Jekyll로 호스팅.

- `privacy-policy.html`
- `terms-of-service.html` (UniTask MIT 라이선스 포함)

인게임 로비 좌상단 "Legal" 텍스트 + 버전 표시로 접근 가능.

---

## 씬

| 씬 | 용도 |
|---|---|
| `Color_Brick` | 메인 게임 씬 |
| `EditBrickColor` | 브릭 색상 편집 도구 |
| `Test` | 기능 테스트용 |

---

## 개발 환경

- Unity 6+
- Rider / Visual Studio
- Android SDK / NDK
- `slideBlock.keystore` (로컬 보관)
