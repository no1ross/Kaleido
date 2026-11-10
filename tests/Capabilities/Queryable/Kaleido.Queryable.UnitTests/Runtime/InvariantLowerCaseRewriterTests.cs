using System.Globalization;
using System.Linq.Expressions;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.Runtime.UnitTests;

public sealed class InvariantLowerCaseRewriterTests
    : SutFixture
{
    private static readonly ParameterExpression Name = Expression.Parameter(typeof(string), "name");

    private static System.Reflection.MethodInfo StringMethod(string name, params Type[] parameters) =>
        typeof(string).GetMethod(name, parameters) ?? throw new InvalidOperationException($"string.{name} not found.");

    private static Func<string, bool> Compile(Expression body) =>
        Expression.Lambda<Func<string, bool>>(body, Name).Compile();

    // name.ToLower() == "info", the shape the applier builds for a case-insensitive equals.
    private static BinaryExpression EqualsInfo() =>
        Expression.Equal(
            Expression.Call(Name, StringMethod(nameof(string.ToLower))),
            Expression.Constant("info"));

    [Fact]
    public void Rewrite_MakesEveryToLowerInvariant_SoTheCurrentCultureCannotChangeAMatch()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // Under tr-TR, "I".ToLower() is a dotless ı: the expression as built misses "INFO".
            Assert.False(Compile(EqualsInfo())("INFO"));
            Assert.True(Compile(InvariantLowerCaseRewriter.Rewrite(EqualsInfo()))("INFO"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Rewrite_ReplacesToLower_AndLeavesEverythingElseAsItWas()
    {
        var contains = Expression.Call(
            Expression.Call(Name, StringMethod(nameof(string.ToLower))),
            StringMethod(nameof(string.Contains), typeof(string)),
            Expression.Constant("nf"));

        var rewritten = Assert.IsAssignableFrom<MethodCallExpression>(InvariantLowerCaseRewriter.Rewrite(contains));

        Assert.Equal(nameof(string.Contains), rewritten.Method.Name);
        Assert.Equal(nameof(string.ToLowerInvariant), Assert.IsAssignableFrom<MethodCallExpression>(rewritten.Object).Method.Name);
        Assert.Same(Name, Assert.IsAssignableFrom<MethodCallExpression>(rewritten.Object).Object);
    }
}
