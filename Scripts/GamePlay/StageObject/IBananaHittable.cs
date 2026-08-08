using UnityEngine;

// 바나나가 충돌하면 반응하는 스테이지 오브젝트. BananaContactRelay가 이 인터페이스로 위임한다.
public interface IBananaHittable
{
    void OnBananaHit(Collision2D collision);
}
