#if APPS_IN_TOSS
using System;
using UnityEditor;

// 앱인토스 빌드에서 SDK의 화면 내 디버그 콘솔(vConsole)을 뺀다.
// 토스 앱은 미니앱 페이지에 자체 vConsole("웹 디버그 툴즈")을 로딩 때부터 띄워 둔다. SDK 콘솔은 그 위에 만들어지다
// 예외를 던지고, Unity 로더가 그 예외를 로드 실패로 처리해 SDK 템플릿이 "게임 로드 실패" 화면으로 게임을 덮는다.
// SDK가 콘솔 포함 여부를 AIT_DEBUG_CONSOLE 환경 변수로 덮어쓰게 해 두었으므로 에디터 프로세스에만 그 값을 넣는다.
// 기기 로그는 토스 앱의 "웹 디버그 툴즈"로 본다.
[InitializeOnLoad]
public static class AppsInTossDebugConsoleOverride
{
    private const string VariableName = "AIT_DEBUG_CONSOLE";

    static AppsInTossDebugConsoleOverride()
    {
        // Unity를 띄우기 전에 직접 지정한 값이 있으면 그 값을 따른다.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableName))) return;

        Environment.SetEnvironmentVariable(VariableName, "false");
    }
}
#endif
