using System.Collections.ObjectModel;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ImageMounter.Reflection
{
    [ComVisible(false)]
    public abstract class ExpressionSupport
    {
        private ExpressionSupport()
        {
        }

        private class ParameterCompatibilityComparer : IEqualityComparer<Type>
        {
            private ParameterCompatibilityComparer()
            {
            }

            private static readonly ParameterCompatibilityComparer Instance = new ParameterCompatibilityComparer();

            private new bool Equals(Type x, Type y)
            {
                return x.IsAssignableFrom(y);
            }

            private new int GetHashCode(Type obj)
            {
                return obj.GetHashCode();
            }

            public static bool Compatible(MethodInfo dest, Type sourceReturnType, Type[] sourceParameters)
            {
                return dest.ReturnType.IsAssignableFrom(sourceReturnType) && dest.GetParameters().Select(dparam => dparam.ParameterType).SequenceEqual(sourceParameters, Instance);
            }
        }

        public static Delegate CreateLocalFallbackFunction(string MethodName, Type[] GenericArguments, IEnumerable<Expression> MethodArguments, Type ReturnType, bool InvertResult, bool RuntimeMethodDetection)
        {
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

            Dim staticArgs =
                Aggregate arg In MethodArguments
                Select If(arg.NodeType = ExpressionType.Quote, DirectCast(arg, UnaryExpression).Operand, arg)
                Into ToArray()

 */
            var staticArgsTypes = Array.ConvertAll(staticArgs, arg => arg.Type);

            var newMethod = GetCompatibleMethod(typeof(Enumerable), true, MethodName, GenericArguments, ReturnType, staticArgsTypes);

            if (newMethod == null)
                throw new NotSupportedException("Expression calls unsupported method " + MethodName + ".");

            // ' Substitute first argument (extension method source object) with a parameter that
            // ' will be substituted with the result sequence when resulting lambda conversion
            // ' routine is called locally after data has been fetched from external data service.
            var sourceObject = Expression.Parameter(newMethod.GetParameters()(0).ParameterType, "source");

            staticArgs[0] = sourceObject;

            Expression newCall = Expression.Call(newMethod, staticArgs);
            if (InvertResult)
                newCall = Expression.Not(newCall);
            var enumerableStaticDelegate = Expression.Lambda(newCall, sourceObject).Compile();

            if (RuntimeMethodDetection)
            {
                var instanceArgs = staticArgs.Skip(1).ToArray();
                var instanceArgsTypes = staticArgsTypes.Skip(1).ToArray();

                var delegateType = enumerableStaticDelegate.GetType();
                var delegateInvokeMethod = delegateType.GetMethod("Invoke");

                Expression<Func<object, Delegate>> getDelegateInstanceOrDefault = obj => GetCompatibleMethodAsDelegate(obj.GetType(), false, InvertResult, MethodName, GenericArguments, ReturnType, instanceArgsTypes, sourceObject, instanceArgs) ?? enumerableStaticDelegate;

                var exprGetDelegateInstanceOrDefault = Expression.Invoke(getDelegateInstanceOrDefault, sourceObject);

                var exprCallDelegateInvokeMethod = Expression.Call(Expression.TypeAs(exprGetDelegateInstanceOrDefault, delegateType), delegateInvokeMethod, sourceObject);

                return Expression.Lambda(exprCallDelegateInvokeMethod, sourceObject).Compile();
            }
            else
                return enumerableStaticDelegate;
        }

        public static Delegate GetCompatibleMethodAsDelegate(Type TypeToSearch, bool FindStaticMethod, bool InvertResult, string MethodName, Type[] GenericArguments, Type ReturnType, Type[] AlternateArgsTypes, ParameterExpression Instance, Expression[] Args)
        {
            var dynMethod = GetCompatibleMethod(TypeToSearch, FindStaticMethod, MethodName, GenericArguments, ReturnType, AlternateArgsTypes);
            if (dynMethod == null)
                return null;
            Expression callExpr;
            if (FindStaticMethod)
                callExpr = Expression.Call(dynMethod, Args);
            else
                callExpr = Expression.Call(Expression.TypeAs(Instance, dynMethod.DeclaringType), dynMethod, Args);
            if (InvertResult)
                callExpr = Expression.Not(callExpr);
            return Expression.Lambda(callExpr, Instance).Compile();
        }

        public static MethodInfo GetCompatibleMethod(Type TypeToSearch, bool FindStaticMethod, string MethodName, Type[] GenericArguments, Type ReturnType, Type[] AlternateArgsTypes)
        {
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            Static methodCache As New Dictionary(Of String, MethodInfo)

 */
            MethodInfo newMethod = null;

            var key = string.Concat(ReturnType.ToString(), ":", MethodName, ":", string.Join(":", Array.ConvertAll(AlternateArgsTypes, argType => argType.ToString())));

            lock (methodCache)
            {
                if (!methodCache.TryGetValue(key, ref newMethod))
                {
                    var methodNames = new[] { MethodName, "get_" + MethodName };
                    ;/* Cannot convert AssignmentStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

                    newMethod =
                        Aggregate m In TypeToSearch.GetMethods(BindingFlags.Public Or If(FindStaticMethod, BindingFlags.Static, BindingFlags.Instance))
                        Where
                            methodNames.Contains(m.Name) AndAlso
                            m.GetParameters().Length = AlternateArgsTypes.Length AndAlso
                            m.IsGenericMethodDefinition AndAlso
                            m.GetGenericArguments().Length = GenericArguments.Length
                        Select m = m.MakeGenericMethod(GenericArguments)
                        Into FirstOrDefault(
                            ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes))

 */
                    if (newMethod == null && !FindStaticMethod)
                    {
                        foreach (var interf in from i in TypeToSearch.GetInterfaces()
                                               where i.IsGenericType() && i.GetGenericArguments().Length == GenericArguments.Length
                                               select i)
                        {
                            ;/* Cannot convert AssignmentStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

                            newMethod =
                                Aggregate m In interf.GetMethods(BindingFlags.Public Or BindingFlags.Instance)
                                Into FirstOrDefault(
                                    methodNames.Contains(m.Name) AndAlso
                                    ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes))

 */
                            if (newMethod != null)
                                break;
                        }
                    }

                    methodCache.Add(key, newMethod);
                }
            }

            return newMethod;
        }

        public static Type GetListItemsType(Type Type)
        {
            while (Type.HasElementType)
                Type = Type.GetElementType();
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            Static listItemsTypes As New Dictionary(Of Type, Type())

 */
            Type[] i = null;

            lock (listItemsTypes)
            {
                if (!listItemsTypes.TryGetValue(Type, ref i))
                {
                    ;/* Cannot convert AssignmentStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

                    i =
                        Aggregate ifc In Type.GetInterfaces()
                        Where ifc.IsGenericType AndAlso ifc.GetGenericTypeDefinition() Is GetType(IList(Of ))
                        Select ifc.GetGenericArguments()(0)
                        Into ToArray()

 */
                    listItemsTypes.Add(Type, i);
                }
            }

            if (i.Length == 0)
                return Type;
            else if (i.Length == 1)
                return i[0];

            throw new NotSupportedException("More than one element type detected for list type " + Type.ToString() + ".");
        }

        private sealed class ExpressionMemberEqualityComparer : IEqualityComparer<MemberInfo>
        {
            public new bool Equals(MemberInfo x, MemberInfo y)
            {
                if (Object.ReferenceEquals(x, y) || (x.DeclaringType == y.DeclaringType && x.MetadataToken == y.MetadataToken))
                    return true;
                else
                    return false;
            }

            public new int GetHashCode(MemberInfo obj)
            {
                return obj.DeclaringType.MetadataToken ^ obj.MetadataToken;
            }
        }

        public static SequenceEqualityComparer<MemberInfo> MemberSequenceEqualityComparer { get; } = new SequenceEqualityComparer<MemberInfo>(new ExpressionMemberEqualityComparer());

        public static Dictionary<IEnumerable<MemberInfo>, string> GetDataFieldMappings(Type ElementType)
        {
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            Static dataMappings As New Dictionary(Of Type, Dictionary(Of IEnumerable(Of MemberInfo), String))

 */
            Dictionary<IEnumerable<MemberInfo>, string> mappings = null;

            lock (dataMappings)
            {
                if (!dataMappings.TryGetValue(ElementType, ref mappings))
                {
                    ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains more than one element
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.ConvertSelectClauseSyntax(SelectClauseSyntax vbFromClause)
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

                    Dim _mappings =
                        From prop In ElementType.GetMembers(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.FlattenHierarchy)
                        Where
                            (prop.MemberType = MemberTypes.Property AndAlso
                                DirectCast(prop, PropertyInfo).GetIndexParameters().Length = 0 AndAlso
                                DirectCast(prop, PropertyInfo).CanRead AndAlso
                                DirectCast(prop, PropertyInfo).CanWrite) OrElse
                            (prop.MemberType = MemberTypes.Field AndAlso
                                Not DirectCast(prop, FieldInfo).IsInitOnly)
                        Select Props = DirectCast({prop}, IEnumerable(Of MemberInfo)), prop.Name

 */
                    mappings = _mappings.ToDictionary(m => m.Props, m => m.Name, MemberSequenceEqualityComparer);
                    ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains more than one element
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

                    Dim submappings =
                        From props In mappings.Keys.ToArray()
                        Let prop = props(0)
                        Where
                            (prop.MemberType = MemberTypes.Property AndAlso
                                DirectCast(prop, PropertyInfo).GetIndexParameters().Length = 0 AndAlso
                                DirectCast(prop, PropertyInfo).CanRead AndAlso
                                DirectCast(prop, PropertyInfo).CanWrite) OrElse
                            (prop.MemberType = MemberTypes.Field AndAlso
                                Not DirectCast(prop, FieldInfo).IsInitOnly)
                        Let
                            type = GetListItemsType(If(prop.MemberType = MemberTypes.Property,
                                                    DirectCast(prop, PropertyInfo).PropertyType,
                                                    DirectCast(prop, FieldInfo).FieldType))
                        Where
                            (Not type.IsPrimitive) AndAlso
                            type IsNot GetType(String)
                        From submapping In GetDataFieldMappings(type)
                        Select
                            Key = submapping.Key.Concat(props),
                            Value = $"{prop.Name}.{submapping.Value}"

 */
                    foreach (var submapping in submappings)
                        mappings.Add(submapping.Key, submapping.Value);

                    dataMappings.Add(ElementType, mappings);
                }
            }

            return mappings;
        }

        public static ReadOnlyCollection<string> GetPropertiesWithAttributes<TAttribute>(Type type) where TAttribute : Attribute
        {
            return AttributedMemberFinder<TAttribute>.GetPropertiesWithAttributes(type);
        }

        private class AttributedMemberFinder<TAttribute> where TAttribute : Attribute
        {
            public static ReadOnlyCollection<string> GetPropertiesWithAttributes(Type type)
            {
                ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

                Static cache As New Dictionary(Of Type, ReadOnlyCollection(Of String))

 */
                ReadOnlyCollection<string> prop = null;

                lock (cache)
                {
                    if (!cache.TryGetValue(type, ref prop))
                    {
                        ;/* Cannot convert AssignmentStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitSimpleArgument(SimpleArgumentSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitSimpleArgument(SimpleArgumentSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.<>c__DisplayClass83_0.<ConvertArguments>b__0(ArgumentSyntax a, Int32 i)
   at System.Linq.Enumerable.<SelectIterator>d__5`2.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList[TNode](IEnumerable`1 nodes)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitArgumentList(ArgumentListSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ArgumentListSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitArgumentList(ArgumentListSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ArgumentListSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitInvocationExpression(InvocationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitInvocationExpression(InvocationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 
                        prop = Array.AsReadOnly(
                            Aggregate p In type.GetMembers(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.FlattenHierarchy)
                                Where Attribute.IsDefined(p, GetType(TAttribute))
                                Select p.Name
                                Into ToArray())

 */
                        cache.Add(type, prop);
                    }
                }

                return prop;
            }
        }

        public static string GetDataTableName<TContext>(Type entityType)
        {
            return DataContextPropertyFinder<TContext>.GetDataTableName(entityType);
        }

        private class DataContextPropertyFinder<TContext>
        {
            public static string GetDataTableName(Type entityType)
            {
                ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

                Static properties As New Dictionary(Of Type, String)

 */
                string prop = null;

                lock (properties)
                {
                    if (!properties.TryGetValue(entityType, ref prop))
                    {
                        ;/* Cannot convert AssignmentStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ParenthesizedExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ParenthesizedExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.MemberAccessExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.MemberAccessExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 
                        prop =
                            (Aggregate attr In GetType(TContext).GetProperty("Items").GetCustomAttributes(True).OfType(Of XmlElementAttribute)()
                                Into [Single](attr.Type.IsAssignableFrom(entityType))).ElementName

 */
                        properties.Add(entityType, prop);
                    }
                }

                return prop;
            }
        }

        public static Expression GetLambdaBody(Expression expression)
        {
            return GetLambdaBody(expression, ref null);
        }

        public static Expression GetLambdaBody(Expression expression, out ReadOnlyCollection<ParameterExpression> parameters)
        {
            if (expression.NodeType != ExpressionType.Quote)
            {
                parameters = null;
                return expression;
            }

            var expr = (LambdaExpression)(UnaryExpression)expression.Operand;

            parameters = expr.Parameters;

            return expr.Body;
        }

        private class PropertiesAssigners<T>
        {
            public static Dictionary<string, Func<T, object>> Getters { get; }

            public static Dictionary<string, Action<T, object>> Setters { get; }

            public static Dictionary<string, Type> Types { get; }

            public static PropertiesAssigners()
            {
                var target = Expression.Parameter(typeof(T), "targetObject");
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

                Dim props =
                    Aggregate m In GetType(T).GetMembers(BindingFlags.Public Or BindingFlags.Instance)
                    Let p = TryCast(m, PropertyInfo)
                    Let f = TryCast(m, FieldInfo)
                    Where
                        (p IsNot Nothing AndAlso
                         p.CanRead AndAlso
                         p.CanWrite AndAlso
                         p.GetIndexParameters().Length = 0) OrElse
                        (f IsNot Nothing AndAlso
                         Not f.IsInitOnly)
                    Let proptype = If(p IsNot Nothing, p.PropertyType, f.FieldType)
                    Let name = m.Name
                    Let member = Expression.PropertyOrField(target, m.Name)
                        Into ToArray()

 */
                ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains more than one element
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.ConvertSelectClauseSyntax(SelectClauseSyntax vbFromClause)
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

                Dim getters =
                    From m In props
                    Select
                        m.name,
                        valueconverted = If(m.proptype.IsValueType,
                            Expression.Convert(m.member, GetType(Object)),
                            Expression.TypeAs(m.member, GetType(Object)))

 */
                Getters = getters.ToDictionary(m => m.name, m => Expression.Lambda<Func<T, object>>(m.valueconverted, target).Compile(), StringComparer.OrdinalIgnoreCase);
                ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains more than one element
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.ConvertSelectClauseSyntax(SelectClauseSyntax vbFromClause)
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

                Dim setters =
                    From m In props
                    Let value = Expression.Parameter(GetType(Object), "value")
                    Let valueconverted = If(m.proptype.IsValueType,
                        DirectCast(Expression.ConvertChecked(value, m.proptype), Expression),
                        DirectCast(Expression.Condition(
                        Expression.TypeIs(value, m.proptype),
                        Expression.TypeAs(value, m.proptype),
                        Expression.ConvertChecked(value, m.proptype)), Expression))
                    Select
                        m.name,
                        assign = Expression.Assign(m.member, valueconverted),
                        value

 */
                Setters = setters.ToDictionary(m => m.name, m => Expression.Lambda<Action<T, object>>(m.assign, target, m.value).Compile(), StringComparer.OrdinalIgnoreCase);

                Types = props.ToDictionary(m => m.name, m => m.proptype, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static Dictionary<string, Func<T, object>> GetPropertyGetters<T>() where T : new()
        {
            return new Dictionary<string, Func<T, object>>(PropertiesAssigners<T>.Getters, PropertiesAssigners<T>.Getters.Comparer);
        }

        public static Dictionary<string, Action<T, object>> GetPropertySetters<T>() where T : new()
        {
            return new Dictionary<string, Action<T, object>>(PropertiesAssigners<T>.Setters, PropertiesAssigners<T>.Setters.Comparer);
        }

        public static Dictionary<string, Type> GetPropertyTypes<T>() where T : new()
        {
            return new Dictionary<string, Type>(PropertiesAssigners<T>.Types, PropertiesAssigners<T>.Types.Comparer);
        }

        public static T RecordToEntityObject<T>(IDataRecord record) where T : new()
        {
            return RecordToEntityObject(record, new T());
        }

        public static T RecordToEntityObject<T>(IDataRecord record, T obj)
        {
            var props = PropertiesAssigners<T>.Setters;

            for (var i = 0; i <= record.FieldCount - 1; i++)
            {
                Action<T, object> prop = null;
                if (props.TryGetValue(record.GetName(i), out prop))
                    prop(obj, record[i] is DBNull ? null : record[i]);
            }

            return obj;
        }
    }
}
