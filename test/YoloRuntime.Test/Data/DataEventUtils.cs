using System.Reflection;
using CryptoExchange.Net.Sockets;

namespace YoloRuntime.Test.Data;

public static class DataEventUtils
{
    public static DataEvent<T> ToDataEvent<T>(this TestDataEvent<T> e) =>
        CreateInstance<DataEvent<T>>(e.Data, e.Topic, e.Timestamp);

    private static T? CreateInstance<T>(params object[] args)
    {
        var type = typeof(T);

        var instance = (T?) type.Assembly.CreateInstance(
            type.FullName,
            false,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            args,
            null,
            null);
        
        return instance;
    }
}