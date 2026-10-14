using System;

// 앱인토스 결제 오류를 사용자 취소·승인 대기·실패로 나눈다.
// SDK 브릿지는 오류 코드를 버리고 메시지만 넘기므로, AppsInTossIapErrorCode.jslib 훅이 메시지 앞에 붙인 "[CODE] "에서 코드를 읽는다.
// 문자열만 받으므로 APPS_IN_TOSS 디파인이 없는 빌드 타깃에서도 테스트할 수 있다.
public static class AppsInTossPurchaseError
{
    private const string UserCanceledCode = "USER_CANCELED";
    private const string PaymentPendingCode = "PAYMENT_PENDING";

    // SDK가 ErrorCode를 채워 주면 그 값을, 아니면 메시지 접두사의 코드를 쓴다. 없으면 빈 문자열.
    public static string GetCode(string errorCode, string message)
    {
        if (!string.IsNullOrEmpty(errorCode)) return errorCode;
        if (string.IsNullOrEmpty(message) || message[0] != '[') return string.Empty;

        int end = message.IndexOf("] ", StringComparison.Ordinal);
        if (end < 2) return string.Empty;

        for (int i = 1; i < end; i++)
        {
            if (!IsCodeChar(message[i])) return string.Empty;
        }

        return message.Substring(1, end - 1);
    }

    // 코드가 있으면 코드로만 판정한다. 코드가 없으면(구버전 토스 앱, 훅 설치 실패) 메시지의 "cancel"로 판정한다.
    public static bool IsCancelled(string errorCode, string message)
    {
        string code = GetCode(errorCode, message);
        if (code.Length > 0) return code == UserCanceledCode;

        return message != null && message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // 승인 대기는 코드로만 판정한다. 코드가 없으면 실패로 보고 다음 복원의 미결 주문 조회에 맡긴다.
    public static bool IsPending(string errorCode, string message) => GetCode(errorCode, message) == PaymentPendingCode;

    private static bool IsCodeChar(char c) => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
}
