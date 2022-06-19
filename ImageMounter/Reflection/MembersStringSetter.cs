using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace ImageMounter.Reflection
{
    public abstract class MembersStringSetter
    {
        private MembersStringSetter()
        {
        }

        internal static readonly MethodInfo _EnumParse = typeof(Enum).GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type), typeof(string) }, null);

        public static Action<T, string> GenerateReferenceTypeMemberSetter<T>(string member_name)
        {
            return MembersStringSetter<T>.GenerateReferenceTypeMemberSetter(member_name);
        }

        private abstract class MembersStringSetter<T>
        {
            private MembersStringSetter()
            {
            }

            /// <summary>Generate a specific member setter for a specific reference type</summary>
            ///         <param name="member_name">The member's name as defined in <typeparamref name="T"/></param>
            ///         <returns>A compiled lambda which can access (set> the member</returns>
            internal static Action<T, string> GenerateReferenceTypeMemberSetter(string member_name)
            {
                var param_this = Expression.Parameter(typeof(T), "this");

                var param_value = Expression.Parameter(typeof(string), "value");             // ' the member's new value

                var member_info = typeof(T).GetMember(member_name, BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m is PropertyInfo || m is FieldInfo);

                Expression member;

                if (member_info is FieldInfo)
                {
                    var field_info = (FieldInfo)member_info;
                    if (field_info.IsInitOnly || field_info.IsLiteral)
                        return null;
                    member = Expression.Field(param_this, field_info);
                }
                else if (member_info is PropertyInfo)
                {
                    var property_info = (PropertyInfo)member_info;
                    if (!property_info.CanWrite)
                        return null;
                    member = Expression.Property(param_this, property_info);
                }
                else
                    return null;

                Expression assign_value;
                if (member.Type == typeof(string))
                    assign_value = param_value;
                else if (member.Type.IsEnum)
                    assign_value = Expression.Convert(Expression.Call(_EnumParse, Expression.Constant(member.Type), param_value), member.Type);
                else
                {
                    var method = member.Type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (method != null)
                        assign_value = Expression.Call(method, param_value);
                    else
                    {
                        var can_convert_from_string = TypeDescriptor.GetConverter(member.Type)?.CanConvertFrom(typeof(string));
                        if (!can_convert_from_string.GetValueOrDefault())
                            return null;

                        assign_value = Expression.Convert(param_value, member.Type);
                    }
                }
                assign_value = Expression.Condition(Expression.ReferenceEqual(param_value, Expression.Constant(null)), Expression.Default(member.Type), assign_value);

                var assign = Expression.Assign(member, assign_value);                // ' i.e., 'this.member_name = value'

                var lambda = Expression.Lambda<Action<T, string>>(assign, param_this, param_value);

                return lambda.Compile();
            }
        }
    }
}
