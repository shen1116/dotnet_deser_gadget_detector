using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    internal class SourceDetector
    {
        public static bool IsSourceMethod(IMethodSymbol symbol)
        {
            return symbol.Parameters.Length < 10 || IsDeserializationConstructor(symbol) || IsDeserializationEventMethod(symbol) || IsSetMethod(symbol);
        }

        // 判断构造函数是否为反序列化构造函数
        private static bool IsDeserializationConstructor(IMethodSymbol method)
        {
            // 一般反序列化构造函数会接受一个 SerializationInfo 类型的参数
            return method.Parameters.Any(p => p.Type.Name.Contains("SerializationInfo"));
        }

        // 判断是否是 OnDeserializing / OnDeserialized 方法，检查是否有对应的特性标记
        private static bool IsDeserializationEventMethod(IMethodSymbol method)
        {
            var attributes = method.GetAttributes();
            return attributes.Any(attr => attr.AttributeClass?.Name == "OnDeserializedAttribute" ||
                                           attr.AttributeClass?.Name == "OnDeserializingAttribute");
        }

        // 判断方法是否是 set 方法
        private static bool IsSetMethod(IMethodSymbol method)
        {
            return method.MethodKind == MethodKind.PropertySet;
        }
    }
}
