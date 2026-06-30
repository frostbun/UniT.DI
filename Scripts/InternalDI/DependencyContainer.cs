#nullable enable
namespace UniT.DI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using UniT.Extensions;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class DependencyContainer : IDependencyContainer
    {
        #region Constructor

        private readonly Dictionary<Type, List<object>> cache = new();

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

        IReadOnlyList<object> IDependencyContainer.ResolveAll(Type type) => this.GetAll(type);

        IReadOnlyList<T> IDependencyContainer.ResolveAll<T>() => this.GetAll<T>();

        object IDependencyContainer.Instantiate(Type type, params object?[] @params) => this.Instantiate(type, @params);

        T IDependencyContainer.Instantiate<T>(params object?[] @params) => this.Instantiate<T>(@params);

        GameObject IDependencyContainer.Instantiate(GameObject prefab) => throw new NotSupportedException();

        void IDependencyContainer.Inject(object instance) => throw new NotSupportedException();

        void IDependencyContainer.Inject(GameObject instance) => throw new NotSupportedException();

        #endregion

        #region Manual Add

        public void Add(Type type, object instance)
        {
            this.cache.GetOrAdd(type).Add(instance);
        }

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
            return this.TryGet(type, out var instance) ? instance : throw new KeyNotFoundException($"No instance found for {type.Name}");
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

        public IReadOnlyList<object> GetAll(Type type)
        {
            return this.cache.GetOrDefault(type) ?? (IReadOnlyList<object>)Array.Empty<object>();
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
                ?? throw new ArgumentException($"Method {methodName} not found on {obj.GetType().Name}", nameof(methodName));
            return this.Invoke(obj, method, @params);
        }

        #endregion

        #region Resolve

        private object?[] ResolveParameters(ParameterInfo[] parameters, object?[] @params, string context)
        {
            return parameters.Select(static (parameter, state) =>
            {
                var (@this, @params, context, isParamUsed) = state;

                var parameterType = parameter.ParameterType;

                for (var i = 0; i < @params.Length; ++i)
                {
                    var param = @params[i];
                    if (isParamUsed[i] || (param is not null && !parameterType.IsInstanceOfType(param))) continue;
                    isParamUsed[i] = true;
                    return param;
                }

                if (parameterType.IsArray)
                {
                    return @this.ResolveArray(parameterType.GetElementType()!);
                }

                if (parameterType.IsGenericType)
                {
                    var type = parameterType.GetGenericTypeDefinition();
                    if (type == typeof(IEnumerable<>) || type == typeof(IReadOnlyList<>) || type == typeof(IReadOnlyCollection<>) || type == typeof(IList<>) || type == typeof(ICollection<>))
                    {
                        return @this.ResolveArray(parameterType.GetGenericArguments()[0]);
                    }
                    if (type == typeof(ReadOnlyCollection<>) || type == typeof(List<>) || type == typeof(HashSet<>) || type == typeof(Stack<>) || type == typeof(Queue<>) || type == typeof(Collection<>))
                    {
                        return Activator.CreateInstance(parameterType, @this.ResolveArray(parameterType.GetGenericArguments()[0]));
                    }
                }

                if (@this.TryGet(parameterType, out var instance)) return instance;

                if (parameter.HasDefaultValue) return parameter.DefaultValue;

                throw new InvalidOperationException($"Cannot resolve {parameterType.Name} for {parameter.Name} while {context}");
            }, (@this: this, @params, context, isParamUsed: new bool[@params.Length])).ToArray();
        }

        private Array ResolveArray(Type type)
        {
            var instances = this.GetAll(type).ToArray();
            var array     = Array.CreateInstance(type, instances.Length);
            instances.CopyTo(array, 0);
            return array;
        }

        #endregion

        #region Generic

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(T instance) where T : notnull => this.Add(typeof(T), instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(params object?[] @params) where T : notnull => this.Add(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfaces<T>(params object?[] @params) where T : notnull => this.AddInterfaces(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelf<T>(params object?[] @params) where T : notnull => this.AddInterfacesAndSelf(typeof(T), @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains<T>() where T : notnull => this.Contains(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>() where T : notnull => (T)this.Get(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>([MaybeNullWhen(false)] out T instance) where T : notnull
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
        public IReadOnlyList<T> GetAll<T>() where T : notnull => this.GetAll(typeof(T)).Cast<T>().ToArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Instantiate<T>(params object?[] @params) where T : notnull => (T)this.Instantiate(typeof(T), @params);

        #endregion

        #region Add From

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromResource<T>(string path) where T : Object
        {
            this.Add(LoadResource<T>(path));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromResource<T>(string path) where T : Object
        {
            this.AddInterfaces(LoadResource<T>(path));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromResource<T>(string path) where T : Object
        {
            this.AddInterfacesAndSelf(LoadResource<T>(path));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInNewPrefabResource<T>(string path) where T : Component
        {
            this.Add(Object.Instantiate(LoadResource<T>(path)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInNewPrefabResource<T>(string path) where T : Component
        {
            this.AddInterfaces(Object.Instantiate(LoadResource<T>(path)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInNewPrefabResource<T>(string path) where T : Component
        {
            this.AddInterfacesAndSelf(Object.Instantiate(LoadResource<T>(path)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInNewPrefab<T>(T prefab) where T : Component
        {
            this.Add(Object.Instantiate(prefab));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInNewPrefab<T>(T prefab) where T : Component
        {
            this.AddInterfaces(Object.Instantiate(prefab));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInNewPrefab<T>(T prefab) where T : Component
        {
            this.AddInterfacesAndSelf(Object.Instantiate(prefab));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFromComponentInHierarchy<T>() where T : Component
        {
            this.Add(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesFromComponentInHierarchy<T>() where T : Component
        {
            this.AddInterfaces(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInterfacesAndSelfFromComponentInHierarchy<T>() where T : Component
        {
            this.AddInterfacesAndSelf(Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllFromComponentInHierarchy<T>() where T : Component
        {
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.Add);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllInterfacesFromComponentInHierarchy<T>() where T : Component
        {
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.AddInterfaces);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAllInterfacesAndSelfFromComponentInHierarchy<T>() where T : Component
        {
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).ForEach(this.AddInterfacesAndSelf);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T LoadResource<T>(string path) where T : Object
        {
            return Resources.Load<T>(path).NullIfDestroyed() ?? throw new KeyNotFoundException($"{path} not found in Resources");
        }

        #endregion
    }
}