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

        public bool TryResolve<T>([MaybeNullWhen(false)] out T instance) where T : notnull;

        public object Resolve(Type type);

        public T Resolve<T>() where T : notnull;

        public IReadOnlyList<object> ResolveAll(Type type);

        public IReadOnlyList<T> ResolveAll<T>() where T : notnull;

        public object Instantiate(Type type, params object?[] @params);

        public T Instantiate<T>(params object?[] @params) where T : notnull;

        public GameObject Instantiate(GameObject prefab);

        public void Inject(object instance);

        public void Inject(GameObject instance);
    }
}