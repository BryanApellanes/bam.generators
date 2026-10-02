namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The shape of a decorated method's return, which selects how the generated member intercepts it.
    /// </summary>
    public enum DecoratorMethodKind
    {
        /// <summary>The method returns nothing.</summary>
        Void,

        /// <summary>The method returns a value synchronously.</summary>
        Value,

        /// <summary>The method returns a <see cref="System.Threading.Tasks.Task"/>.</summary>
        Task,

        /// <summary>The method returns a <see cref="System.Threading.Tasks.Task{TResult}"/>.</summary>
        TaskOfResult,

        /// <summary>The method returns a <see cref="System.Threading.Tasks.ValueTask"/>.</summary>
        ValueTask,

        /// <summary>The method returns a <see cref="System.Threading.Tasks.ValueTask{TResult}"/>.</summary>
        ValueTaskOfResult
    }
}
