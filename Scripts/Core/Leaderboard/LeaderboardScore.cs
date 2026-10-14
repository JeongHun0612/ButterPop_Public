// 플랫폼 서비스가 같은 기준으로 제출 값을 만들도록 랭킹 점수 규칙을 모은다.
public static class LeaderboardScore
{
    // 스테이지 목록 안의 인덱스(0부터 시작)만 유저에게 보이는 스테이지 번호로 바꾼다.
    public static bool TryFromStageIndex(int stageIndex, int stageCount, out long score)
    {
        bool inRange = stageIndex >= 0 && stageIndex < stageCount;
        score = inRange ? (long)stageIndex + 1 : -1;
        return inRange;
    }
}
