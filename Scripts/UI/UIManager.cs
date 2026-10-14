using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] private Transform sceneRoot;
    [SerializeField] private Transform popupRoot;
    [SerializeField] private UIRegistrySO registry;

    [Header("Initial Loading")]
    [SerializeField] private UIInitialLoading initialLoading;

    private readonly Dictionary<Type, UIPanel> _panels = new();
    private readonly Stack<UIPopup> _popupStack = new();

    private UIScene _currentScene;

    public UIScene CurrentScene => _currentScene;
    public UIPopup CurrentPopup => _popupStack.Count > 0 ? _popupStack.Peek() : null;

    public async UniTask InitializeAsync()
    {
        var tasks = new List<UniTask>();

        foreach (var reference in registry.sceneUIs)
            tasks.Add(LoadAndRegisterPanel(reference, sceneRoot));

        foreach (var reference in registry.popupUIs)
            tasks.Add(LoadAndRegisterPanel(reference, popupRoot));

        await UniTask.WhenAll(tasks);
    }

    public async UniTask ShowInitialLoading()
    {
        if (initialLoading != null) await initialLoading.Show();
    }

    public async UniTask HideInitialLoading()
    {
        if (initialLoading != null) await initialLoading.Hide();
    }

    private async UniTask LoadAndRegisterPanel(AssetReferenceGameObject reference, Transform root)
    {
        var go = await reference.InstantiateAsync(root).ToUniTask();
        var panel = go.GetComponent<UIPanel>();

        if (panel == null)
        {
            Debug.LogError($"[UIManager] 프리팹에 UIPanel 컴포넌트가 없습니다: {go.name}");
            ReleaseInstance(go);
            return;
        }

        // Dictionary.Add는 키가 겹치면 예외를 던진다. 그대로 두면 InitializeAsync 전체가
        // 끊겨 나머지 패널이 등록되지 않고 부팅이 실패한다. 먼저 등록된 쪽을 남긴다.
        var type = panel.GetType();
        if (_panels.ContainsKey(type))
        {
            Debug.LogError($"[UIManager] 패널 타입이 중복 등록되었습니다: {type.Name}. UIRegistry를 확인하세요");
            ReleaseInstance(go);
            return;
        }

        panel.Initialize();
        _panels.Add(type, panel);
    }

    // Addressables로 만든 인스턴스는 핸들까지 반환해야 누수가 남지 않는다.
    private static void ReleaseInstance(GameObject go)
    {
        if (!Addressables.ReleaseInstance(go))
            Destroy(go);
    }

    public async UniTask<T> ShowScene<T>() where T : UIScene
    {
        if (!TryGetPanel<T>(out var target)) return null;

        if (_currentScene == target) return target;

        if (_currentScene != null)
        {
            await _currentScene.Hide();
            _currentScene.OnAfterHide();
        }

        _currentScene = target;
        _currentScene.OnBeforeShow();
        await _currentScene.Show();

        return target;
    }

    public async UniTask<T> ShowPopup<T>() where T : UIPopup
    {
        if (!TryGetPanel<T>(out var target)) return null;

        if (_popupStack.Contains(target)) return target;

        target.OnBeforeShow();
        _popupStack.Push(target);
        await target.Show();

        return target;
    }

    // 스택 중간에 깔려 있어도 자기 자신만 닫는다.
    public async UniTask HidePopup(UIPopup target, bool instant = false)
    {
        if (target == null) return;
        if (!target.gameObject.activeSelf) return;

        if (_popupStack.Count > 0 && _popupStack.Peek() == target)
        {
            await HideTopPopup(instant);
            return;
        }

        // 스택에 없는 상태여도 닫히긴 해야 한다. 안 그러면 닫기 버튼이 먹통이 된다.
        RemoveFromStack(target);

        await target.Hide(instant);
        target.OnAfterHide();
    }

    public async UniTask HideAllPopups(bool instant = true)
    {
        while (_popupStack.Count > 0)
            await HideTopPopup(instant);
    }

    public T GetPanel<T>() where T : UIPanel
    {
        _panels.TryGetValue(typeof(T), out var panel);
        return panel as T;
    }

    private async UniTask HideTopPopup(bool instant)
    {
        if (_popupStack.Count == 0) return;

        var popup = _popupStack.Pop();
        await popup.Hide(instant);
        popup.OnAfterHide();
    }

    // Stack은 임의 위치 제거가 없어 대상만 빼고 다시 쌓는다.
    private void RemoveFromStack(UIPopup target)
    {
        if (!_popupStack.Contains(target)) return;

        var remains = new List<UIPopup>(_popupStack);   // top → bottom 순서
        remains.Remove(target);
        _popupStack.Clear();

        for (int i = remains.Count - 1; i >= 0; i--)
            _popupStack.Push(remains[i]);
    }

    private bool TryGetPanel<T>(out T panel) where T : UIPanel
    {
        if (_panels.TryGetValue(typeof(T), out var found))
        {
            panel = found as T;
            return true;
        }

        Debug.LogError($"[UIManager] 패널을 찾을 수 없습니다: {typeof(T)}");
        panel = null;
        return false;
    }
}