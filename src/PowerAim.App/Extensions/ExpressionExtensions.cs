using System.Linq.Expressions;

namespace PowerAim.Extensions;

internal static class ExpressionExtensions
{
    internal static T GetOwnerAs<T>(this MemberExpression memberExpression) where T : class =>
        memberExpression.Expression switch
        {
            ConstantExpression constant => constant.Value as T,
            MemberExpression innerMember => Expression.Lambda(innerMember).Compile().DynamicInvoke() as T,
            _ => throw new ArgumentException("Invalid expression")
        };

    internal static MemberExpression GetMemberExpression<T>(this Expression<Func<T>> expression) =>
        expression.Body switch
        {
            MemberExpression member => member,
            UnaryExpression { Operand: MemberExpression operand } => operand,
            _ => throw new ArgumentException("Invalid expression")
        };
}