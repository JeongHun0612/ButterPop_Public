using System.Threading;
using Cysharp.Threading.Tasks;

// 클리어 직후, 결과 화면 캡처 전에 끝까지 재생되는 스테이지 고유 연출
public interface IStageClearEffect
{
    UniTask PlayClearEffectAsync(CancellationToken token);
}
