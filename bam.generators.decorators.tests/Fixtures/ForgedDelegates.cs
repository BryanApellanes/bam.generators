namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>
    /// An extension method on <see cref="object"/> binds to any target, including a typed handler wrapper's,
    /// which is how a test forges a delegate that shares a wrapper's target without being its Invoke.
    /// </summary>
    public static class ForgedDelegates
    {
        public static object? Quiet(this object target, DecoratorInvocationContext context)
        {
            return null;
        }
    }
}
