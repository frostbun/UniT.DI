#nullable enable
namespace Zenject
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using UniT.DI;
    using UnityEngine;
    using Zenject.Internal;

    public static class ZenjectExtensions
    {
        public static void BindDependencyContainer(this DiContainer container)
        {
            container.BindInterfacesTo<ZenjectWrapper>().AsSingle();
        }

        private sealed class ZenjectWrapper : IDependencyContainer
        {
            private readonly DiContainer container;

            [Preserve]
            public ZenjectWrapper(DiContainer container) => this.container = container;

            bool IDependencyContainer.TryResolve(Type type, [MaybeNullWhen(false)] out object instance)
            {
                if (this.container.TryResolve(type) is { } obj)
                {
                    instance = obj;
                    return true;
                }
                instance = null;
                return false;
            }

            bool IDependencyContainer.TryResolve<T>([MaybeNullWhen(false)] out T instance)
            {
                if (this.container.TryResolve(typeof(T)) is T obj)
                {
                    instance = obj;
                    return true;
                }
                instance = default;
                return false;
            }

            object IDependencyContainer.Resolve(Type type) => this.container.Resolve(type);

            T IDependencyContainer.Resolve<T>() => this.container.Resolve<T>();

            IEnumerable<object> IDependencyContainer.ResolveAll(Type type) => this.container.ResolveAll(type).Cast<object>();

            IEnumerable<T> IDependencyContainer.ResolveAll<T>() => this.container.ResolveAll<T>();

            object IDependencyContainer.Instantiate(Type type, params object?[] @params) => this.container.Instantiate(type, @params);

            T IDependencyContainer.Instantiate<T>(params object?[] @params) => this.container.Instantiate<T>(@params);

            GameObject IDependencyContainer.Instantiate(GameObject prefab) => this.container.InstantiatePrefab(prefab);

            void IDependencyContainer.Inject(object instance) => this.container.Inject(instance);

            void IDependencyContainer.Inject(GameObject instance) => this.container.InjectGameObject(instance);
        }
    }
}