# 광고·결제·저장 문제 대응

[README](../README.md) · [저장소 안내](05-repository.md)

문제와 적용 효과는 포트폴리오에 정리된 사례를 기준으로 작성했습니다. 코드 링크는 현재 공개 사본을 가리킵니다. 발생 가능한 저장 중단과 스토어 무응답에 대한 방어 설계도 포함하며, 모두 실사용자 제보로 발생한 장애라고 주장하지 않습니다.

## 1. 광고: 최초 로드 실패와 준비되지 않은 버튼

**문제:** 배너 최초 로드에 실패하면 세션 중 복구되지 않고, 리워드 광고가 준비되기 전에 버튼을 눌러도 반응이 없었습니다.

**원인:** 초기 로드를 한 번만 수행하고, 배너 객체의 존재와 실제 노출 상태를 동일하게 취급했습니다. 리워드 준비 상태도 버튼 노출과 연결되지 않았습니다.

**해결:**

- `LevelPlayProvider`에서 배너 요청·로드 중·로드 완료·노출 여부를 각각 추적합니다.
- `AdRetryTimer`를 사용해 실패 후 5·15·30·60초로 간격을 늘리며 재요청하고, 성공 시 재시도 상태를 초기화합니다.
- 광고 준비 상태 이벤트를 `AdsService`에서 전달하고 `UIInGameHUD`가 리워드 버튼 표시를 갱신합니다.
- 버튼 노출뿐 아니라 실제 클릭 처리에서도 광고 준비 상태를 확인합니다.

**효과:** 일시적 로드 실패 후 세션 안에서 재시도할 수 있고, 광고가 준비되지 않은 상태의 버튼 노출을 막습니다. 광고 서버의 응답 성공 자체를 보장하는 방식은 아닙니다.

읽을 코드: [LevelPlayProvider.cs](../Scripts/Core/Ad/LevelPlayProvider.cs), [AdRetryTimer.cs](../Scripts/Core/Ad/AdRetryTimer.cs), [AdsService.cs](../Scripts/Core/Ad/AdsService.cs), [UIInGameHUD.cs](../Scripts/UI/Scene/UIInGameHUD.cs)

## 2. 결제: 미확정 주문 복원

**상황:** 결제 승인 이후 구매 확정 전에 앱이 종료되면 주문 처리가 중단될 수 있습니다.

**해결:** `UnityIapProvider.FetchPurchasesAsync`에서 보유 주문을 조회합니다. `RecordOwned`는 확정 주문을 기록하고 미확정 주문에도 상품 기록과 `ConfirmPurchase`를 적용합니다. `PurchaseService`는 조회에 성공한 보유 목록을 이용해 게임 권한을 복원합니다.

조회 실패와 실제 미보유를 구분하고, 복원 도중 새 구매가 발생하면 이전 조회 결과로 새 권한을 회수하지 않도록 처리합니다.

**효과:** 다음 실행에서 주문 처리와 구매 권한 복원을 다시 시도할 수 있습니다. Unity IAP와 앱인토스는 지급·확정 계약이 달라 각각의 Provider에서 처리합니다.

읽을 코드: [UnityIapProvider.RecordOwned](../Scripts/Core/Purchase/UnityIapProvider.cs), [PurchaseService.RestoreCoreAsync](../Scripts/Core/Purchase/PurchaseService.cs), [AppsInTossIapProvider.cs](../Scripts/Core/Purchase/AppsInTossIapProvider.cs)

## 3. 결제: 콜백 무응답과 후속 구매 차단

**원인:** 콜백을 `UniTaskCompletionSource`로 감쌌을 때 응답이 없으면 대기가 끝나지 않습니다. 진행 중 구매 핸들이 남아 있으면 이후 구매가 차단됩니다.

| 대기 대상 | 상한 | 처리 |
|---|---|---|
| 스토어 연결 | 30초 | 오류 기록 후 반환 |
| 상품·보유 조회·복원 | 30초 | 임시 구독 정리 또는 응답 대기 종료 |
| 구매 응답 | 900초 | 진행 중 구매 핸들 해제, 실패 반환 |

구매 콜백에서는 처리 중 예외가 나더라도 `finally`에서 `ResolvePurchase`를 호출하도록 구성했습니다. 900초는 결제창의 인증·카드 등록 시간을 고려한 대기 상한이며, 결제를 취소하거나 환불하는 시간 기준이 아닙니다.

**효과:** 대기를 종료할 수 있는 경로를 만들고, 콜백 처리 예외나 응답 시간 초과 후 대기 핸들이 남아 상점이 잠기는 상황을 줄였습니다.

읽을 코드: [UnityIapProvider.PurchaseAsync / ResolvePurchase / OnPurchasePending](../Scripts/Core/Purchase/UnityIapProvider.cs)

## 4. 저장: 기존 파일을 직접 덮어쓰지 않는 기록

**상황:** 기존 저장 파일을 덮어쓰는 도중 중단되면 불완전한 파일이 남을 수 있습니다.

**해결:** `FileSaveStorage.SaveAsync`는 JSON을 임시 파일에 먼저 기록합니다. 기존 파일이 있으면 `File.Replace`, 최초 저장이면 `File.Move`로 반영합니다.

```csharp
File.WriteAllText(_tempPath, json);

if (File.Exists(_path))
    File.Replace(_tempPath, _path, null);
else
    File.Move(_tempPath, _path);
```

**효과:** 임시 파일 기록이 끝나기 전 기존 저장 파일을 직접 덮어쓰는 상황을 피합니다. 파일시스템·저장 장치의 모든 장애에서 영속성을 보장한다는 의미는 아닙니다. 이 방식은 로컬 파일 저장 구현이며 앱인토스 저장은 별도 Storage를 사용합니다.

읽을 코드: [FileSaveStorage.cs](../Scripts/Core/Save/FileSaveStorage.cs)

## 5. 저장: 같은 임시 파일에 대한 동시 쓰기

**원인:** 저장 요청이 겹치면 같은 임시 파일에 접근하거나 완료 순서가 뒤집힐 수 있습니다. 파일 교체만으로 여러 작성자 사이의 순서를 제어할 수는 없습니다.

**해결:** `SaveService.SaveAsync`에서 새 요청을 이전 저장 뒤에 연결합니다. `ChainSaveAsync`는 앞 저장의 완료를 기다린 뒤 실행 시점의 `Data`를 Storage에 전달합니다. 반환한 UniTask를 호출자와 다음 저장이 함께 기다리므로 `.Preserve()`를 사용합니다.

```csharp
_pending = ChainSaveAsync(_pending).Preserve();
return _pending;
```

**효과:** 동일 Service의 저장 쓰기를 순차 실행하고 실행 시점의 최신 데이터를 기록합니다. 모든 동시 작업을 제어하는 전역 잠금이 아니라 저장 요청 체인의 범위에 대한 설계입니다.

현재 코드에는 로드 실패 시 `IsReadOnly`를 유지해 해당 세션의 저장·삭제를 막는 보호도 포함됩니다.

읽을 코드: [SaveService.cs](../Scripts/Core/Save/SaveService.cs), [ISaveStorage.cs](../Scripts/Core/Save/ISaveStorage.cs)

## 검증 자료

- [포트폴리오 PDF](../media/portfolio.pdf): 광고 미준비·준비 화면, 결제와 저장 문제 대응 사례.
- [SaveProtectionTests.cs](../Scripts/Editor/SaveProtectionTests.cs): 로드 실패 시 쓰기·삭제 차단, 저장 직렬화, 실패한 저장 이후의 복구 등을 다루는 기존 테스트 코드.
- [PurchaseGrantTests.cs](../Scripts/Editor/PurchaseGrantTests.cs): 구매 권한 지급 흐름을 다루는 기존 테스트 코드.
- [실제 SaveService 실행 결과](06-media.md): 검증용 메모리 Storage에서 저장 요청 체인과 로드 실패 보호를 제한적으로 재현한 결과.

테스트 파일은 검증 의도와 시나리오를 보여주는 자료입니다. 공개 패키지 작성 과정에서는 Unity 테스트를 실행하지 않았으며, 이 소스 저장소 단독으로 테스트를 실행할 수는 없습니다.
