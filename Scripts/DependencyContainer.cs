#nullable enable
namespace UniT.DI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using UniT.Extensions;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class DependencyContainer : IDependencyContainer
    {
        #region Constructor

        private readonly Dictionary<Type, HashSet<object>> cache = new();

        public DependencyContainer()
        {
            this.AddInterfaces(this);
        }

        #endregion

        #region IDependencyContainer

        bool IDependencyContainer.TryResolve(Type type, [MaybeNullWhen(false)] out object instance) => this.TryGet(type, out instance);

        bool IDependencyContainer.TryResolve<T>([MaybeNullWhen(false)] out T instance) => this.TryGet(out instance);

        object IDependencyContainer.Resolve(Type type) => this.Get(type);

        T IDependencyContainer.Resolve<T>() => this.Get<T>();

        object[] IDependencyContainer.ResolveAll(Type type) => this.GetAll(type);

        T[] IDependencyContainer.ResolveAll<T>() => this.GetAll<T>();

        object IDependencyContainer.Instantiate(Type type, params object?[] @params) => this.Instantiate(type, @params);

        T IDependencyContainer.Instantiate<T>(params object?[] @params) => this.Instantiate<T>(@params);

        GameObject IDependencyContainer.Instantiate(GameObject prefab) => throw new NotSupportedException();

        void IDependencyContainer.Inject(object instance) => throw new NotSupportedException();

        void IDependencyContainer.Inject(GameObject instance) => throw new NotSupportedException();

        #endregion

        public void Add(Type type, object instance)
        {
            this.cache.GetOrAdd(type).Add(instance);
        }

        #region Manual Add

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(object instance)
        {
            this.Add(instance.GetType(), instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfaces(object instance)
        {
            foreach (var @interface in instance.GetType().GetInterfaces())
            {
                this.Add(@interface, instance);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelf(object instance)
        {
            foreach (var @interface in instance.GetType().GetInterfaces().Prepend(instance.GetType()))
            {
                this.Add(@interface, instance);
            }
        }

        #endregion

        #region Auto Add

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Type type, params object?[] @params)
        {
            this.Add(type, this.Instantiate(type, @params));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfaces(Type type, params object?[] @params)
        {
            this.AddInterfaces(this.Instantiate(type, @params));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelf(Type type, params object?[] @params)
        {
            this.AddInterfacesAndSelf(this.Instantiate(type, @params));
        }

        #endregion

        #region Get

        public bool Contains(Type type)
        {
            return this.cache.ContainsKey(type);
        }

        public object Get(Type type)
        {
            return this.TryGet(type, out var instance) ? instance : throw new ArgumentOutOfRangeException(nameof(type), type, $"No instance found for {type.Name}");
        }

        public bool TryGet(Type type, [MaybeNullWhen(false)] out object instance)
        {
            if (this.cache.GetOrDefault(type)?.SingleOrDefault() is { } obj)
            {
                instance = obj;
                return true;
            }
            instance = null;
            return false;
        }

        public object[] GetAll(Type type)
        {
            return this.cache.GetOrDefault(type)?.ToArray() ?? Array.Empty<object>();
        }

        #endregion

        #region Instantiate

        public object Instantiate(Type type, params object?[] @params)
        {
            if (type.IsAbstract) throw new InvalidOperationException($"Cannot instantiate abstract type {type.Name}");
            if (type.ContainsGenericParameters) throw new InvalidOperationException($"Cannot instantiate generic type {type.Name}");
            var constructor = type.GetSingleConstructor();
            return constructor.Invoke(this.ResolveParameters(constructor.GetParameters(), @params, $"instantiating {type.Name}"));
        }

        public object Invoke(object obj, MethodInfo method, params object[] @params)
        {
            return method.Invoke(obj, this.ResolveParameters(method.GetParameters(), @params, $"invoking {method.Name} on {obj.GetType().Name}"));
        }

        public object Invoke(object obj, string methodName, params object[] @params)
        {
            var method = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new ArgumentOutOfRangeException(nameof(methodName), methodName, $"Method {methodName} not found on {obj.GetType().Name}");
            return this.Invoke(obj, method, @params);
        }

        #endregion

        #region Resolve

        private static readonly HashSet<Type> SupportedInterfaces = new() { typeof(IEnumerable<>), typeof(ICollection<>), typeof(IList<>), typeof(IReadOnlyCollection<>), typeof(IReadOnlyList<>) };

        private static readonly HashSet<Type> SupportedConcreteTypes = new() { typeof(Collection<>), typeof(List<>), typeof(ReadOnlyCollection<>) };

        private object?[] ResolveParameters(ParameterInfo[] parameters, object?[] @params, string context)
        {
            return parameters.Select(parameter =>
            {
                var parameterType = parameter.ParameterType;
                var paramIndex    = @params.FirstIndexOrDefault(parameterType.IsInstanceOfType);
                if (paramIndex >= 0)
                {
                    var param = @params[paramIndex];
                    @params[paramIndex] = null;
                    return param;
                }
                switch (parameterType)
                {
                    case { IsGenericType: true, IsInterface: true } when SupportedInterfaces.Contains(parameterType.GetGenericTypeDefinition()):
                    {
                        return GetArray(parameterType.GetGenericArguments()[0]);
                    }
                    case { IsGenericType: true } when SupportedConcreteTypes.Contains(parameterType.GetGenericTypeDefinition()):
                    {
                        return Activator.CreateInstance(parameterType, GetArray(parameterType.GetGenericArguments()[0]));
                    }
                    case { IsArray: true }:
                    {
                        return GetArray(parameterType.GetElementType()!);
                    }
                    default:
                    {
                        if (this.TryGet(parameterType, out var instance)) return instance;
                        if (parameter.HasDefaultValue) return parameter.DefaultValue;
                        throw new($"Cannot resolve {parameterType.Name} for {parameter.Name} while {context}");
                    }
                }
            }).ToArray();

            Array GetArray(Type type)
            {
                var instances = this.GetAll(type);
                var array     = Array.CreateInstance(type, instances.Length);
                instances.CopyTo(array, 0);
                return array;
            }
        }

        #endregion

        #region Generic

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(T instance) where T : notnull => this.Add(typeof(T), instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(params object?[] @params) => this.Add(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfaces<T>(params object?[] @params) => this.AddInterfaces(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelf<T>(params object?[] @params) => this.AddInterfacesAndSelf(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<T>() => this.Contains(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>() => (T)this.Get(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>([MaybeNullWhen(false)] out T instance)
        {
            if (this.TryGet(typeof(T), out var obj))
            {
                instance = (T)obj;
                return true;
            }
            instance = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] GetAll<T>() => this.GetAll(typeof(T)).Cast<T>().ToArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Instantiate<T>(params object?[] @params) => (T)this.Instantiate(typeof(T), @params);

        #endregion

        #region Add From

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromResource<T>(string path) where T : Object => this.Add(LoadResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromResource<T>(string path) where T : Object => this.AddInterfaces(LoadResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromResource<T>(string path) where T : Object => this.AddInterfacesAndSelf(LoadResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInNewPrefabResource<T>(string path) where T : Component => this.Add(InstantiatePrefabResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInNewPrefabResource<T>(string path) where T : Component => this.AddInterfaces(InstantiatePrefabResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInNewPrefabResource<T>(string path) where T : Component => this.AddInterfacesAndSelf(InstantiatePrefabResource<T>(path));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInNewPrefab<T>(T prefab) where T : Component => this.Add(InstantiatePrefab(prefab));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInNewPrefab<T>(T prefab) where T : Component => this.AddInterfaces(InstantiatePrefab(prefab));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInNewPrefab<T>(T prefab) where T : Component => this.AddInterfacesAndSelf(InstantiatePrefab(prefab));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInHierarchy<T>() where T : Object => this.Add(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInHierarchy<T>() where T : Object => this.AddInterfaces(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInHierarchy<T>() where T : Object => this.AddInterfacesAndSelf(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllFromComponentInHierarchy<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.Add);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllInterfacesFromComponentInHierarchy<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.AddInterfaces);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllInterfacesAndSelfFromComponentInHierarchy<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.AddInterfacesAndSelf);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T LoadResource<T>(string path) where T : Object => Resources.Load<T>(path) ?? throw new ArgumentOutOfRangeException(nameof(path), path, $"Failed to load {path}");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T InstantiatePrefab<T>(T prefab) where T : Component => Object.Instantiate(prefab).DontDestroyOnLoad();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T InstantiatePrefabResource<T>(string path) where T : Component => InstantiatePrefab(LoadResource<GameObject>(path).GetComponentOrThrow<T>());

        #endregion
    }
}