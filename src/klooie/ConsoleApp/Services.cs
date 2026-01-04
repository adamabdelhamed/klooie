using klooie;
using System.Linq.Expressions;
using System.Reflection;
namespace klooie;
public sealed class Services : Recyclable
{
    private Dictionary<Type, object> mutable = new();

    private static LazyPool<Services> pool = new LazyPool<Services>(() => new Services());
    private Services() { }
    public static Services Create()
    {
        var ret = pool.Value.Rent();
        return ret;
    }

    private static Services? _current;
    public static Services? Current => _current;

    public static void SetCurrent(Services instance)
    {
        if (_current != null) throw new InvalidOperationException("Services.Current already initialized");
        _current = instance;
    }

    public T RegisterService<T>(T instance) where T : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        mutable[typeof(T)] = instance;
        return instance;
    }

    public T GetRequiredService<T>() where T : class
    {
        if (mutable!.TryGetValue(typeof(T), out var obj)) return (T)obj;
        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    public bool TryGetService<T>(out T? instance) where T : class
    {
        if (mutable!.TryGetValue(typeof(T), out var obj))
        {
            instance = (T)obj;
            return true;
        }
        instance = null;
        return false;
    }

    public void RegisterRecyclableService<T>() where T : Recyclable
    {
        var factory = GetOrCreateFactory(typeof(T));
        var instance = factory(this);
        RegisterService((T)instance);
    }

    public T CreateRecyclableServiceInstance<T>() where T : Recyclable
    {
        var factory = GetOrCreateFactory(typeof(T));
        var instance = factory(this);
        return (T)instance;
    }

    public T CreateRecyclableServiceInstance<T>(Type specificType) where T : Recyclable
    {
        var factory = GetOrCreateFactory(specificType);
        var instance = factory(this);
        return (T)instance;
    }


    // ========== Factory Cache ==========

    private static readonly Dictionary<Type, Func<Services, object>> _factoryCache = new();
    private static readonly Dictionary<Type, MethodInfo> _createMethodCache = new();

    private static Func<Services, object> GetOrCreateFactory(Type type)
    {
        if (_factoryCache.TryGetValue(type, out var cached)) return cached;

        if (!_createMethodCache.TryGetValue(type, out var createMethod))
        {
            createMethod = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{type.Name} must have a public static Create method.");
            _createMethodCache[type] = createMethod;
        }

        var parameters = createMethod.GetParameters();
        var serviceParam = Expression.Parameter(typeof(Services), "services");
        var paramExprs = new List<Expression>();

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var tryGetMethod = typeof(Services).GetMethod(nameof(TryGetService))!.MakeGenericMethod(param.ParameterType);

            var tmpVar = Expression.Variable(param.ParameterType, $"resolved{i}");
            var outVar = Expression.Variable(param.ParameterType, $"out{i}");
            var tryCall = Expression.Call(serviceParam, tryGetMethod, outVar);

            var fallbackBlock = Expression.Block(
                new[] { tmpVar, outVar },
                Expression.IfThenElse(
                    tryCall,
                    Expression.Assign(tmpVar, outVar),
                    Expression.Assign(tmpVar, Expression.Default(param.ParameterType))
                ),
                tmpVar // return the value of the block
            );

            paramExprs.Add(fallbackBlock);
        }

        var call = Expression.Call(createMethod, paramExprs);
        var cast = Expression.Convert(call, typeof(object));
        var lambda = Expression.Lambda<Func<Services, object>>(cast, serviceParam);

        foreach (var param in parameters)
        {
            var paramType = param.ParameterType;
            bool canResolve = Current?.mutable?.ContainsKey(paramType) == true;

            if (!canResolve && param.HasDefaultValue == false && IsNullable(paramType) == false)
            {
                throw new InvalidOperationException($"Cannot resolve required parameter '{param.Name}' of type '{paramType.Name}' for {type.Name}.Create().");
            }
        }

        var compiled = lambda.Compile();

        _factoryCache[type] = compiled;
        return compiled;
    }

    private static bool IsNullable(Type t) => !t.IsValueType || Nullable.GetUnderlyingType(t) != null;

    // ========== Recyclable Hook ==========
    protected override void OnReturn()
    {
        base.OnReturn();
        mutable.Clear();
        _current = null;
    }
}
