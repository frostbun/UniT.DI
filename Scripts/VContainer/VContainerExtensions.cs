#nullable enable
namespace VContainer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using UniT.DI;
    using UnityEngine;
    using VContainer.Unity;

    public static class VContainerExtensions
    {
        public static void RegisterDependencyContainer(this IContainerBuilder builder)
        {
            builder.Register<VContainerWrapper>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        private sealed class VContainerWrapper : IDependencyContainer
        {
            private readonly IObjectResolver container;

            [Preserve]
            public VContainerWrapper(IObjectResolver container) => this.container = container;

            bool IDependencyContainer.TryResolve(Type type, [MaybeNullWhen(false)] out object instance) => this.container.TryResolve(type, out instance);

            bool IDependencyContainer.TryResolve<T>([MaybeNullWhen(false)] out T instance) => this.container.TryResolve(out instance);

            object IDependencyContainer.Resolve(Type type) => this.container.Resolve(type);

            T IDependencyContainer.Resolve<T>() => this.container.Resolve<T>();

            IEnumerable<object> IDependencyContainer.ResolveAll(Type type) => ((IEnumerable)this.container.Resolve(typeof(IEnumerable<>).MakeGenericType(type))).Cast<object>();

            IEnumerable<T> IDependencyContainer.ResolveAll<T>() => this.container.Resolve<IEnumerable<T>>();

            object IDependencyContainer.Instantiate(Type type, params object?[] @params) => this.container.Instantiate(type, @params);

            T IDependencyContainer.Instantiate<T>(params object?[] @params) => this.container.Instantiate<T>(@params);

            GameObject IDependencyContainer.Instantiate(GameObject prefab) => this.container.Instantiate(prefab);

            void IDependencyContainer.Inject(object instance) => this.container.Inject(instance);

            void IDependencyContainer.Inject(GameObject instance) => this.container.InjectGameObject(instance);
        }
    }
}