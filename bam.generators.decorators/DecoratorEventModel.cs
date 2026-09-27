using System.Reflection;
using System.Text;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for one interface event. Subscriptions are forwarded straight to the decorated
    /// instance, so subscribers hear the events the wrapped service raises.
    /// </summary>
    public class DecoratorEventModel : DecoratorMemberModel
    {
        /// <summary>Initializes the model by reflecting over <paramref name="eventInfo"/>.</summary>
        /// <param name="declaringInterface">The interface declaring the event.</param>
        /// <param name="eventInfo">The reflected event.</param>
        /// <param name="isExplicit">Whether to render an explicit interface implementation.</param>
        public DecoratorEventModel(Type declaringInterface, EventInfo eventInfo, bool isExplicit)
            : base(declaringInterface, eventInfo, isExplicit)
        {
            Event = eventInfo;
        }

        /// <summary>Gets the reflected event.</summary>
        public EventInfo Event { get; }

        /// <inheritdoc />
        public override IEnumerable<Type> ReferencedTypes
        {
            get
            {
                yield return Event.EventHandlerType ?? typeof(EventHandler);
            }
        }

        /// <inheritdoc />
        public override string RenderedMember
        {
            get
            {
                StringBuilder source = new StringBuilder();
                source.AppendLine($"{Indent}/// <inheritdoc />");
                source.AppendLine($"{Indent}{Modifier}event {CSharpTypeName.Of(Event)} {Qualify(Event.Name)}");
                source.AppendLine($"{Indent}{{");
                source.AppendLine($"{Indent}    add => {Target}.{Event.Name} += value;");
                source.AppendLine($"{Indent}    remove => {Target}.{Event.Name} -= value;");
                source.AppendLine($"{Indent}}}");
                return source.ToString();
            }
        }
    }
}
