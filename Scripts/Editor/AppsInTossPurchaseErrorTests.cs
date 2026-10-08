#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// 앱인토스 결제 오류의 취소·실패 판정표를 고정한다.
// 메시지의 "[CODE] " 접두사는 AppsInTossIapErrorCode.jslib 훅이 붙인다.
public class AppsInTossPurchaseErrorTests
{
    [TestCase("", "[USER_CANCELED] 결제를 취소했어요", ExpectedResult = true)]
    [TestCase("", "[USER_CANCELED] ", ExpectedResult = true)]
    [TestCase("USER_CANCELED", "결제를 취소했어요", ExpectedResult = true)]
    [TestCase("", "[PAYMENT_PENDING] 결제 승인을 기다리고 있어요", ExpectedResult = false)]
    [TestCase("", "[UNSUPPORTED_APP_VERSION] 토스앱 5.219.0 이상에서 사용할 수 있어요.", ExpectedResult = false)]
    [TestCase("", "[NETWORK_ERROR] request cancelled by network", ExpectedResult = false)]
    [TestCase("NETWORK_ERROR", "user cancelled", ExpectedResult = false)]
    public bool CodeDecidesWhenPresent(string errorCode, string message)
        => AppsInTossPurchaseError.IsCancelled(errorCode, message);

    [TestCase("", "User cancelled the order", ExpectedResult = true)]
    [TestCase("", "USER_CANCELED", ExpectedResult = true)]
    [TestCase("", "결제를 취소했어요", ExpectedResult = false)]
    [TestCase("", "[object Object]", ExpectedResult = false)]
    [TestCase("", "", ExpectedResult = false)]
    [TestCase(null, null, ExpectedResult = false)]
    public bool MessageDecidesWithoutCode(string errorCode, string message)
        => AppsInTossPurchaseError.IsCancelled(errorCode, message);

    [TestCase("", "[PAYMENT_PENDING] 결제 승인을 기다리고 있어요", ExpectedResult = true)]
    [TestCase("PAYMENT_PENDING", "", ExpectedResult = true)]
    [TestCase("", "[USER_CANCELED] 결제를 취소했어요", ExpectedResult = false)]
    [TestCase("", "payment pending", ExpectedResult = false)]
    [TestCase(null, null, ExpectedResult = false)]
    public bool PendingDecidedByCodeOnly(string errorCode, string message)
        => AppsInTossPurchaseError.IsPending(errorCode, message);

    [TestCase("", "[USER_CANCELED] 결제를 취소했어요", ExpectedResult = "USER_CANCELED")]
    [TestCase("ITEM_ALREADY_OWNED", "[USER_CANCELED] x", ExpectedResult = "ITEM_ALREADY_OWNED")]
    [TestCase("", "[object Object]", ExpectedResult = "")]
    [TestCase("", "[USER_CANCELED]결제", ExpectedResult = "")]
    [TestCase("", "[] 메시지", ExpectedResult = "")]
    [TestCase("", "결제 실패 [USER_CANCELED] ", ExpectedResult = "")]
    [TestCase(null, null, ExpectedResult = "")]
    public string ReadsCodePrefix(string errorCode, string message)
        => AppsInTossPurchaseError.GetCode(errorCode, message);
}
#endif
