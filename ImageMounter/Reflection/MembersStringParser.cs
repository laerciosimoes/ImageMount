using System.Linq.Expressions;
using System.Reflection;

namespace ImageMounter.Reflection
{
    internal abstract class MembersStringParser<T>
    {
        private MembersStringParser()
        {
        }

        public new static string ToString(T obj)
        {
            var values = from accessor in _accessors
                         select $"{accessor.Key} = {TryCall(accessor.Value, obj, ex => $"{{{ex.GetType()}: {ex.Message}}}")}";

            return $"{{{string.Join(", ", values)}}}";
        }

        private static readonly KeyValuePair<string, Func<T, string>>[] _accessors = GetAccessors();

        private static string TryCall(Func<T, string> method, T param, Func<Exception, string> handler)
        {
            try
            {
                return method(param);
            }
            catch (Exception ex)
            {
                if (handler == null)
                    return null;
                else
                    return handler(ex);
            }
        }

        private static KeyValuePair<string, Func<T, string>>[] GetAccessors()
        {
            var param_this = Expression.Parameter(typeof(T), "this");

            var ObjectToStringMethod = typeof(object).GetMethod("ToString");

            var fields = from member in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)
                         select new KeyValuePair<string, MemberExpression>(member.Name, Expression.Field(param_this, member));

            var props = from member in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        where member.GetIndexParameters().Length == 0 && member.CanRead
                        select new KeyValuePair<string, MemberExpression>(member.Name, Expression.Property(param_this, member));
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertInitializer(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarations(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            Dim Accessors =
                Aggregate field In fields.Concat(props)
                Let isnull = Expression.ReferenceEqual(Expression.TypeAs(field.Value, GetType(Object)), Expression.Constant(Nothing))
                Let fieldstring = Expression.Call(field.Value, ObjectToStringMethod)
                Let valueornull = Expression.Condition(isnull, Expression.Constant("(null)"), fieldstring)
                Let lambda = Expression.Lambda(Of Func(Of T, String))(valueornull, param_this)
                Select accessor = New KeyValuePair(Of String, Func(Of T, String))(field.Key, lambda.Compile())
                Order By accessor.Key
                Into ToArray()

 */
            // Dim fieldgetters =
            // From field In GetType(T).GetFields(BindingFlags.Instance Or BindingFlags.Public)
            // Select New KeyValuePair(Of String, Func(Of T, String))(field.Name, Function(obj As T) field.GetValue(obj).ToString())

            // Dim propsgetters =
            // From prop In GetType(T).GetProperties(BindingFlags.Instance Or BindingFlags.Public)
            // Where prop.GetIndexParameters().Length = 0
            // Let getmethod = prop.GetGetMethod()
            // Where getmethod IsNot Nothing
            // Select New KeyValuePair(Of String, Func(Of T, String))(prop.Name, Function(obj As T) prop.GetValue(obj, Nothing).ToString())

            return Accessors;
        }
    }
}
