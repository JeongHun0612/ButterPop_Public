using UnityEngine;

// 스퀴시 오브젝트가 충돌하면 반응하는 스테이지 오브젝트. SquishContactSensor가 이 인터페이스로 위임한다.
public interface ISquishHittable
{
    void OnSquishHit(Collision2D collision);
}
