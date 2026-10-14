using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경량 서비스 로케이터. 크로스커팅 서비스만 등록한다.
/// 등록은 ServiceBootstrap 한 곳에서 의존성 순서대로 수행한다.
/// </summary>
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
        Debug.Log($"[Services] 등록: {typeof(T).Name}");
    }

    public static T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
        throw new InvalidOperationException(
            $"[Services] 미등록 서비스 조회: {typeof(T).Name}. ServiceBootstrap 실행 여부를 확인하라.");
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var s))
        {
            service = (T)s;
            return true;
        }
        service = null;
        return false;
    }

    public static void Clear() => _services.Clear();
}
