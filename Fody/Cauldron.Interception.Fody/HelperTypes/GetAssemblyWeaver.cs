using Cauldron.Interception.Cecilator;
using Cauldron.Interception.Cecilator.Coders;

namespace Cauldron.Interception.Fody.HelperTypes
{
    internal class GetAssemblyWeaver
    {
        private static Method getAssembly;
        private static Method getTypeInfo;
        private static BuilderType introspectionExtensions;
        private static BuilderType type;
        private static BuilderType typeInfo;

        static GetAssemblyWeaver()
        {
            if (Builder.IsUWP)
            {
                introspectionExtensions = Builder.GetType("System.Reflection.IntrospectionExtensions")?.Import();
                typeInfo = Builder.GetType("System.Reflection.TypeInfo")?.Import();
                getTypeInfo = introspectionExtensions.GetMethod("GetTypeInfo", 1).Import();
                getAssembly = typeInfo.GetMethod("get_Assembly").Import();
            }
            else
            {
                type = Builder.GetType("System.Type")?.Import();
                getAssembly = type.GetMethod("get_Assembly").Import();
            }
        }

        public static CallCoder AddCode(Coder coder, ParametersCodeBlock parameter)
        {
            if (Builder.IsUWP)
                return coder.Call(getTypeInfo, parameter).Call(getAssembly);
            else
                return coder.Load(parameter).Call(getAssembly);
        }

        public static CallCoder AddCode(Coder coder, BuilderType builderType)
        {
            if (Builder.IsUWP)
                return coder.Call(getTypeInfo, builderType.Import()).Call(getAssembly);
            else
                return coder.Load(builderType).Call(getAssembly);
        }
    }
}