using System;
using System.Linq.Expressions;
using YoloAbstractions.Exceptions;

namespace YoloAbstractions.Extensions;

public static class ConfigExtensions
{
    public static TConfig Ensure<TConfig, TValue>(
        this TConfig config,
        Expression<Func<TConfig, TValue>> selector)
    {
        if (selector.Compile()(config) != null)
        {
            return config;
        }

        var expression = (MemberExpression)selector.Body;
        var memberName = expression.Member.Name;

        throw new ConfigException($"Missing or null configuration for {memberName}");
    }
}