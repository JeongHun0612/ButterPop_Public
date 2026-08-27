using UnityEngine;

// 이 오브젝트 하위 SpriteRenderer를 엔트리의 coverSquish 적용에서 제외한다.
// 오브젝트 전체는 스퀴시를 가리되 일부(UFO 빔·글로우 등)만 스퀴시 뒤에 남겨야 할 때 프리팹에 붙인다.
[DisallowMultipleComponent]
public class IgnoreCoverSquish : MonoBehaviour { }
