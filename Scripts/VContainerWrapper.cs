#if UNIT_VCONTAINER
#nullable enable
namespace VContainer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UniT.DI;
    using UnityEngine;
    using VContainer.Internal;
    using VContainer.Unity;
    using Object = UnityEngine.Object;

    public sealed class VContainerWrapper : IDependencyContainer
    {
        private readonly IObjectResolver container;

        [Preserve]
        public VContainerWrapper(IObjectResolver container) => this.container = container;

        bool IDependencyContainer.TryResolve(Type type, [MaybeNullWhen(false)] out object instance) => this.container.TryResolve(type, out instance);

        bool IDependencyContainer.TryResolve<T>([MaybeNullWhen(false)] out T instance) => this.container.TryResolve(out instance);

        object IDependencyContainer.Resolve(Type type) => this.container.Resolve(type);

        T IDependencyContainer.Resolve<T>() => this.container.Resolve<T>();

        object[] IDependencyContainer.ResolveAll(Type type) => ((IEnumerable)this.container.Resolve(typeof(IEnumerable<>).MakeGenericType(type))).Cast<object>().ToArray();

        T[] IDependencyContainer.ResolveAll<T>() => this.container.Resolve<IEnumerable<T>>().ToArray();

        object IDependencyContainer.Instantiate(Type type, params object?[] @params) => this.container.Instantiate(type, @params);

        T IDependencyContainer.Instantiate<T>(params object?[] @params) => this.container.Instantiate<T>(@params);

        GameObject IDependencyContainer.Instantiate(GameObject prefab) => this.container.Instantiate(prefab);

        void IDependencyContainer.Inject(object instance) => this.container.Inject(instance);

        void IDependencyContainer.Inject(GameObject instance) => this.container.InjectGameObject(instance);
    }

    public static class VContainerExtensions
    {
        public static void RegisterDependencyContainer(this IContainerBuilder builder)
        {
            if (builder.Exists(typeof(IDependencyContainer), true)) return;
            builder.Register<VContainerWrapper>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RegistrationBuilder RegisterResource<T>(this IContainerBuilder builder, string path, Lifetime lifetime) where T : Object
        {
            return builder.Register(_ => Resources.Load<T>(path) ?? throw new ArgumentOutOfRangeException(nameof(path), path, $"Failed to load {path}"), lifetime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentRegistrationBuilder RegisterComponentInNewPrefabResource<T>(this IContainerBuilder builder, string path, Lifetime lifetime) where T : Component
        {
            return builder.RegisterComponentInNewPrefab(_ => Resources.Load<T>(path) ?? throw new ArgumentOutOfRangeException(nameof(path), path, $"Failed to load {path}"), lifetime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RegistrationBuilder AsInterfacesAndSelf(this RegistrationBuilder registrationBuilder)
        {
            return registrationBuilder.AsImplementedInterfaces().AsSelf();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AutoResolve(this IContainerBuilder builder, Type type)
        {
            builder.RegisterBuildCallback(container => container.Resolve(type));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AutoResolve<T>(this IContainerBuilder builder)
        {
            builder.AutoResolve(typeof(T));
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Instantiate(this IObjectResolver container, Type type, IReadOnlyList<IInjectParameter> parameters)
        {
            return InjectorCache.GetOrBuild(type).CreateInstance(container, parameters);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Instantiate<T>(this IObjectResolver container, IReadOnlyList<IInjectParameter> parameters)
        {
            return (T)container.Instantiate(typeof(T), parameters);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Instantiate(this IObjectResolver container, Type type, params object?[] @params)
        {
            return container.Instantiate(type, (IReadOnlyList<IInjectParameter>)@params.Select(param => new Parameter(param)).ToArray());
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Instantiate<T>(this IObjectResolver container, params object?[] @params)
        {
            return (T)container.Instantiate(typeof(T), @params);
        }
    }

    public sealed class Parameter : IInjectParameter
    {
        private readonly object? value;

        public Parameter(object? value) => this.value = value;

        bool IInjectParameter.Match(Type parameterType, string _) => parameterType.IsInstanceOfType(this.value);

        object? IInjectParameter.GetValue(IObjectResolver _) => this.value;
    }
}
#endif