#nullable enable
namespace UniT.DI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using UnityEngine;

    public interface IDependencyContainer
    {
        public bool TryResolve(Type type, [MaybeNullWhen(false)] out object instance);

        public bool TryResolve<T>([MaybeNullWhen(false)] out T instance);

        public object Resolve(Type type);

        public T Resolve<T>();

        public IEnumerable<object> ResolveAll(Type type);

        public IEnumerable<T> ResolveAll<T>();

        public object Instantiate(Type type, params object?[] @params);

        public T Instantiate<T>(params object?[] @params);

        public GameObject Instantiate(GameObject prefab);

        public void Inject(object instance);

        public void Inject(GameObject instance);
    }
}