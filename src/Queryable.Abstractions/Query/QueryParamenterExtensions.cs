//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Kaleido.Queryable.Query
//{
//    public static class QueryParameterExtensions
//    {
//        public static T GetRequired<T>(this IReadOnlyDictionary<string, object?> parameters, string name)
//        {
//            if (!parameters.TryGetValue(name, out var value))
//            {
//                throw new InvalidOperationException(
//                    $"Missing parameter '{name}'.");
//            }

//            return (T)QueryValueConverter.ConvertTo(
//                value,
//                typeof(T))!;
//        }

//        public static T? GetOptional<T>(this IReadOnlyDictionary<string, object?>? parameters, string name)
//        {
//            if (parameters is null ||
//                !parameters.TryGetValue(name, out var value))
//            {
//                return default;
//            }

//            return (T?)QueryValueConverter.ConvertTo(
//                value,
//                typeof(T));
//        }
//    }
//}
