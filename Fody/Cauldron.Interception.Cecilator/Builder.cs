using Cauldron.Collections;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cauldron.Interception.Cecilator
{
    /// <summary>
    /// Provides properties and methods for weaving.
    /// </summary>
    public static partial class Builder
    {
        internal static FastDictionary<string, TypeDefinition> allTypes;
        internal static ICecilatorLogging logging;
        internal static FastDictionary<string, TypeDefinition> moduleTypes;
        internal static FastDictionary<string, TypeDefinition> moduleTypesNoGenerated;
        internal static FastDictionary<string, AssemblyDefinition> referencedAssemblies;

        private static ICecilatorParameters parameters;

        /// <summary>
        /// Gets a collection of all types
        /// </summary>
        public static IEnumerable<TypeDefinition> AllTypes => allTypes;

        /// <summary>
        /// Gets the custom attributes of the module.
        /// </summary>
        public static BuilderCustomAttributeCollection CustomAttributes => new BuilderCustomAttributeCollection(parameters.ModuleDefinition);

        /// <summary>
        /// Gets a value that indicates if the weaved assembly is an UWP assembly or not.
        /// </summary>
        public static bool IsUWP => IsReferenced("Windows.Foundation.UniversalApiContract");

        /// <summary>
        /// Gets the parameters of the weaver
        /// </summary>
        public static ICecilatorParameters Parameters => parameters;

        /// <summary>
        /// Gets an array of all the references marked as copy-local.
        /// </summary>
        public static AssemblyDefinition[] ReferenceCopyLocal { get; private set; }

        /// <summary>
        /// Gets an collection of referenced assemblies including the assemblies referenced by it's references.
        /// </summary>
        public static IEnumerable<AssemblyDefinition> ReferencedAssemblies => referencedAssemblies;

        /// <summary>
        /// Gets a list of names of all resources embedded in the assembly.
        /// </summary>
        public static string[] ResourceNames { get; private set; }

        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        public static string Name => parameters.ModuleDefinition.Name;

        /// <summary>
        /// Cancels all operations in the weaver.
        /// </summary>
        public static void Cancel() => CecilatorCancellationToken.Current.Cancel();

        /// <summary>
        /// Returns all assemblies that is referenced by the defined assemblies <paramref name="assemblyDefinitions"/> and its reference assemblies recursively.
        /// </summary>
        /// <returns>A collection of <see cref="AssemblyDefinition"/>.</returns>
        public static IEnumerable<AssemblyDefinition> GetAllReferencedAssemblies(this IEnumerable<AssemblyDefinition> assemblyDefinitions)
        {
            foreach (var item in assemblyDefinitions)
                foreach (var result in item.GetAllReferencedAssemblies())
                    yield return result;
        }

        /// <summary>
        /// Returns all assemblies that is referenced by the defined assembly <paramref name="assemblyDefinition"/> and its reference assemblies recursively.
        /// </summary>
        /// <returns>A collection of <see cref="AssemblyDefinition"/>.</returns>
        public static IEnumerable<AssemblyDefinition> GetAllReferencedAssemblies(this AssemblyDefinition assemblyDefinition)
        {
            var result = new Collection<AssemblyDefinition>();

            void getAssemblyDefinition(IEnumerable<AssemblyNameReference> assemblyNameReferences)
            {
                if (assemblyNameReferences == null)
                    return;

                foreach (var assemblyNameReference in assemblyNameReferences)
                    addAssemblyDefinition(assemblyNameReference);
            }

            void addAssemblyDefinition(AssemblyNameReference assemblyNameReference)
            {
                var resolvedAssembly = Resolve(assemblyNameReference);
                if (resolvedAssembly != null && !result.Contains(resolvedAssembly, new AssemblyDefinitionEqualityComparer()))
                {
                    result.Add(resolvedAssembly);

                    if (resolvedAssembly.MainModule != null)
                        getAssemblyDefinition(resolvedAssembly.MainModule.AssemblyReferences);
                }
            }

            if (assemblyDefinition != null && !result.Contains(assemblyDefinition, new AssemblyDefinitionEqualityComparer()))
            {
                result.Add(assemblyDefinition);

                if (assemblyDefinition.MainModule != null)
                    getAssemblyDefinition(assemblyDefinition.MainModule.AssemblyReferences);
            }

            return result;
        }

        /// <summary>
        /// Tries to get the child type of an array of IEnumerable.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>
        /// If the child type was successfully extracted, then <see cref="Tuple{T1, T2}.Item2"/> is true; otherwise false.
        /// <see cref="Tuple{T1, T2}.Item1"/> contains the child type; otherwise always <see cref="Object"/>
        /// </returns>
        public static (TypeReference childType, bool isSuccessful) GetChildrenType(this TypeReference type) => parameters.ModuleDefinition.GetChildrenType(type);

        /// <summary>
        /// Returns the void Main method of a program. Returns null if there is non.
        /// </summary>
        /// <returns>Retruns an instance of <see cref="Method"/> representing the static void Main of the program; otherwise null</returns>
        public static Method GetMain() =>
           Builder.FindMethodsByName(SearchContext.Module_NoGenerated, "Main", 1)
                    .FirstOrDefault(x => x.ReturnType == BuilderTypes.Void && x.Parameters[0].ChildType == BuilderTypes.String);

        /// <summary>
        /// Intializes the Cecilator
        /// </summary>
        /// <param name="parameters">Parameters for the weaver.</param>
        /// <param name="logging">Logging implementation for the weaver.</param>
        /// <param name="referencedAssemblies">A collection of referenced assemblies. If null; the default referenced assemblies are loaded.</param>
        public static void Initialize(ICecilatorParameters parameters, ICecilatorLogging logging, IEnumerable<AssemblyDefinition> referencedAssemblies = null)
        {
            Builder.parameters = parameters;
            Builder.logging = logging;

            Builder.ReferenceCopyLocal = parameters.ReferenceCopyLocalPaths
                .Where(x => x.EndsWith(".dll"))
                .Select(x => LoadAssembly(x))
                .Where(x => x != null)
                .ToArray();

            if (referencedAssemblies == null)
            {
                referencedAssemblies = parameters.ModuleDefinition.AssemblyReferences
                    .Resolve()
                    .Concat(parameters.References.Split(';').Select(x => LoadAssembly(x)))
                    .Where(x => x != null).ToArray() as IEnumerable<AssemblyDefinition>;

                referencedAssemblies = referencedAssemblies.Concat(ReferenceCopyLocal);
            }

            Builder.referencedAssemblies = referencedAssemblies
                .Distinct(new AssemblyDefinitionEqualityComparer())
                .ToFastDictionary(x => x.FullName, x => x);

            logging.Log("-----------------------------------------------------------------------------");

            foreach (var item in ReferencedAssemblies)
                logging.Log("<<Assembly>> " + item.Name);

            var resourceNames = new List<string>();
            foreach (var item in parameters.ModuleDefinition.Resources)
            {
                logging.Log("<<Resource>> " + item.Name + " " + item.ResourceType);
                if (item.ResourceType == ResourceType.Embedded)
                {
                    var embeddedResource = item as EmbeddedResource;
                    using (var stream = embeddedResource.GetResourceStream())
                    {
                        var bytes = new byte[stream.Length];
                        stream.Read(bytes, 0, bytes.Length);
                        if (bytes[0] == 0xce && bytes[1] == 0xca && bytes[2] == 0xef && bytes[3] == 0xbe)
                        {
                            var resourceCount = BitConverter.ToInt16(bytes.GetBytes(160, 2).Reverse().ToArray(), 0);

                            if (resourceCount > 0)
                            {
                                var startPoint = resourceCount * 8 + 180;

                                for (int i = 0; i < resourceCount; i++)
                                {
                                    var length = (int)bytes[startPoint];
                                    var data = Encoding.Unicode.GetString(bytes, startPoint + 1, length).Trim();
                                    startPoint += length + 5;
                                    resourceNames.Add(data);
                                    logging.Log("             " + data);
                                }
                            }
                        }
                    }
                }
            }

            ResourceNames = resourceNames.ToArray();

            allTypes = ReferencedAssemblies
                .SelectMany(x => x.Modules)
                .Where(x => x != null)
                .SelectMany(x => x.Types)
                .Where(x => x != null)
                .Concat(parameters.ModuleDefinition.Types)
                .Where(x => x.Module != null && x.Module.Assembly != null)
                .Distinct(new TypeDefinitionEqualityComparer())
                .ToFastDictionary(x => x.CreateKey(), x => x);

            moduleTypes = parameters.ModuleDefinition.Types
                .ToFastDictionary(x => x.CreateKey(), x => x);

            moduleTypesNoGenerated = parameters.ModuleDefinition.Types
                .Where(x => x.FullName?.IndexOf('<') < 0 && x.FullName?.IndexOf('>') < 0)
                .ToFastDictionary(x => x.CreateKey(), x => x);

            logging.Log("-----------------------------------------------------------------------------");
        }

        /// <summary>
        /// Checks if the assembly described by <paramref name="assemblyName"/> is referenced or not.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to check.</param>
        /// <returns>Return true if the assembly is referenced; otherwise false.</returns>
        public static bool IsReferenced(string assemblyName) => referencedAssemblies.ContainsKey(assemblyName);

        /// <summary>
        /// Tries to load a list of assemblies using <see cref="AssemblyDefinition.ReadAssembly(string)"/>.
        /// </summary>
        /// <param name="assemblyPaths">A list of assembly paths.</param>
        /// <returns>A collection of <see cref="AssemblyDefinition"/>s that was successfuly loaded.</returns>
        public static IEnumerable<AssemblyDefinition> LoadAssemblies(this IEnumerable<string> assemblyPaths)
        {
            foreach (var item in assemblyPaths)
                yield return LoadAssembly(item);
        }

        /// <summary>
        /// Loads the assembly using the Mono.Cecil default resolver.
        /// </summary>
        /// <param name="assemblyNameReferences">The assembly names of the assemblies to be resolved</param>
        /// <returns>The <see cref="AssemblyDefinition"/> of the assembly.</returns>
        public static IEnumerable<AssemblyDefinition> Resolve(this IEnumerable<AssemblyNameReference> assemblyNameReferences)
        {
            foreach (var item in assemblyNameReferences)
            {
                var result = item.Resolve();
                if (result != null)
                    yield return result;
            }
        }

        /// <summary>
        /// Loads the assembly using the Mono.Cecil default resolver.
        /// </summary>
        /// <param name="assemblyNameReference">The assembly name of the assembly to be resolved</param>
        /// <returns>The <see cref="AssemblyDefinition"/> of the assembly.</returns>
        public static AssemblyDefinition Resolve(this AssemblyNameReference assemblyNameReference)
        {
            try
            {
                return Builder.parameters.ModuleDefinition.AssemblyResolver.Resolve(assemblyNameReference);
            }
            catch
            {
                return null;
            }
        }

        internal static string CreateKey(this TypeDefinition typeDefinition) => typeDefinition.FullName;

        private static AssemblyDefinition LoadAssembly(string path)
        {
            try
            {
                return AssemblyDefinition.ReadAssembly(path);
            }
            catch (BadImageFormatException)
            {
                logging.Log($"Info: a BadImageFormatException has occured while trying to retrieve information from '{path}'");
                return null;
            }
            catch (Exception e)
            {
                logging.Log(e);
                return null;
            }
        }

        #region Type Finders

        public static IEnumerable<BuilderType> FindTypes(string regexPattern) => FindTypes(SearchContext.Module, regexPattern);

        public static IEnumerable<BuilderType> FindTypes(SearchContext searchContext, string regexPattern) =>
            GetTypesInternal(searchContext)
                .Where(x => Regex.IsMatch(x.FullName, regexPattern, RegexOptions.Singleline))
                .Select(x => new BuilderType(x));

        public static IEnumerable<AttributedType> FindTypesByAttribute(BuilderType attributeType) => FindTypesByAttribute(SearchContext.Module, attributeType);

        public static IEnumerable<AttributedType> FindTypesByAttribute(SearchContext searchContext, BuilderType attributeType)
        {
            var result = new ConcurrentBag<AttributedType>();

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                for (int i = 0; i < type.typeDefinition.CustomAttributes.Count; i++)
                {
                    var name = type.typeDefinition.CustomAttributes[i].AttributeType.Resolve().FullName;
                    if (attributeType.Fullname.GetHashCode() == name.GetHashCode() && attributeType.Fullname == name)
                        result.Add(new AttributedType(type, type.typeDefinition.CustomAttributes[i]));
                }

                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<AttributedType> FindTypesByAttributes(IEnumerable<BuilderType> types) => FindTypesByAttributes(SearchContext.Module, types);

        public static IEnumerable<AttributedType> FindTypesByAttributes(SearchContext searchContext, IEnumerable<BuilderType> types)
        {
            var result = new ConcurrentBag<AttributedType>();
            var attributes = types.Select(x => x.Fullname).ToList();

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                for (int i = 0; i < type.typeDefinition.CustomAttributes.Count; i++)
                {
                    if (attributes.Contains(type.typeDefinition.CustomAttributes[i].AttributeType.Resolve().FullName))
                        result.Add(new AttributedType(type, type.typeDefinition.CustomAttributes[i]));
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<BuilderType> FindTypesByBaseClass(string baseClassName) => FindTypesByBaseClass(SearchContext.Module, baseClassName);

        public static IEnumerable<BuilderType> FindTypesByBaseClass(SearchContext searchContext, string baseClassName) => GetTypes(searchContext).Where(x => x.Inherits(baseClassName));

        public static IEnumerable<BuilderType> FindTypesByBaseClass(Type baseClassType) => FindTypesByBaseClass(SearchContext.Module, baseClassType);

        public static IEnumerable<BuilderType> FindTypesByBaseClass(SearchContext searchContext, Type baseClassType)
        {
            if (!baseClassType.IsInterface)
                throw new ArgumentException("Argument 'interfaceType' is not an interface");

            return FindTypesByBaseClass(searchContext, baseClassType.FullName);
        }

        public static IEnumerable<BuilderType> FindTypesByInterface(string interfaceName) => FindTypesByInterface(SearchContext.Module, interfaceName);

        public static IEnumerable<BuilderType> FindTypesByInterface(SearchContext searchContext, string interfaceName) => GetTypes(searchContext).Where(x => x.Implements(interfaceName));

        public static IEnumerable<BuilderType> FindTypesByInterface(Type interfaceType) => FindTypesByInterface(SearchContext.Module, interfaceType);

        public static IEnumerable<BuilderType> FindTypesByInterface(SearchContext searchContext, Type interfaceType)
        {
            if (!interfaceType.IsInterface)
                throw new ArgumentException("Argument 'interfaceType' is not an interface");

            return FindTypesByInterface(searchContext, interfaceType.FullName);
        }

        public static IEnumerable<BuilderType> FindTypesByInterfaces(params string[] interfaceNames) => FindTypesByInterfaces(SearchContext.Module, interfaceNames);

        public static IEnumerable<BuilderType> FindTypesByInterfaces(SearchContext searchContext, params string[] interfaceNames) => GetTypes(searchContext).Where(x => interfaceNames.Any(y => x.Implements(y)));

        public static IEnumerable<BuilderType> FindTypesByInterfaces(params Type[] interfaceTypes) => FindTypesByInterfaces(SearchContext.Module, interfaceTypes);

        public static IEnumerable<BuilderType> FindTypesByInterfaces(SearchContext searchContext, params Type[] interfaceTypes) => FindTypesByInterfaces(searchContext, interfaceTypes.Select(x => x.FullName).ToArray());

        #endregion Type Finders

        #region Field Finders

        public static IEnumerable<Field> FindFields(string regexPattern) => FindFields(SearchContext.Module, regexPattern);

        public static IEnumerable<Field> FindFields(SearchContext searchContext, string regexPattern) => GetTypes(searchContext).SelectMany(x => x.Fields).Where(x => Regex.IsMatch(x.Name, regexPattern, RegexOptions.Singleline));

        public static IEnumerable<AttributedField> FindFieldsByAttribute(Type attributeType) => FindFieldsByAttribute(SearchContext.Module, attributeType);

        public static IEnumerable<AttributedField> FindFieldsByAttribute(SearchContext searchContext, Type attributeType) => FindFieldsByAttribute(searchContext, attributeType.FullName);

        public static IEnumerable<AttributedField> FindFieldsByAttribute(string attributeName) => FindFieldsByAttribute(SearchContext.Module, attributeName);

        public static IEnumerable<AttributedField> FindFieldsByAttribute(SearchContext searchContext, string attributeName)
        {
            var result = new ConcurrentBag<AttributedField>();

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                foreach (var field in type.Fields.Where(x => x.fieldDef.HasCustomAttributes))
                {
                    for (int i = 0; i < field.fieldDef.CustomAttributes.Count; i++)
                    {
                        var fullname = field.fieldDef.CustomAttributes[i].AttributeType.Resolve().FullName;
                        if (fullname.GetHashCode() == attributeName.GetHashCode() && fullname == attributeName)
                            result.Add(new AttributedField(field, field.fieldDef.CustomAttributes[i]));
                    }
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<AttributedField> FindFieldsByAttributes(IEnumerable<BuilderType> types) => FindFieldsByAttributes(SearchContext.Module, types);

        public static IEnumerable<AttributedField> FindFieldsByAttributes(SearchContext searchContext, IEnumerable<BuilderType> types)
        {
            var result = new ConcurrentBag<AttributedField>();
            var attributes = types.Select(x => x.Fullname).ToList();

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                foreach (var field in type.Fields.Where(x => x.fieldDef.HasCustomAttributes))
                {
                    for (int i = 0; i < field.fieldDef.CustomAttributes.Count; i++)
                    {
                        if (attributes.Contains(field.fieldDef.CustomAttributes[i].AttributeType.Resolve().FullName))
                            result.Add(new AttributedField(field, field.fieldDef.CustomAttributes[i]));
                    }
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<Field> FindFieldsByName(string fieldName) => FindFieldsByName(SearchContext.Module, fieldName);

        public static IEnumerable<Field> FindFieldsByName(SearchContext searchContext, string fieldName) => GetTypes(searchContext).SelectMany(x => x.Fields).Where(x => x.Name == fieldName);

        #endregion Field Finders

        #region Property Finders

        public static IEnumerable<AttributedProperty> FindPropertiesByAttributes(IEnumerable<BuilderType> types) => FindPropertiesByAttributes(SearchContext.Module, types);

        public static IEnumerable<AttributedProperty> FindPropertiesByAttributes(SearchContext searchContext, IEnumerable<BuilderType> types)
        {
            var result = new ConcurrentBag<AttributedProperty>();
            var attributes = types.Select(x => x.Fullname).ToList();
            IEnumerable<AttributedProperty> getRelevantAttributes(Property property, Property owningProperty)
            {
                var propertyDefinition = property.propertyDefinition;
                for (int i = 0; i < propertyDefinition.CustomAttributes.Count; i++)
                {
                    var customAttribute = propertyDefinition.CustomAttributes[i];
                    if (attributes.Contains((customAttribute.AttributeType.Resolve() ?? customAttribute.AttributeType).FullName))
                        yield return new AttributedProperty(owningProperty, customAttribute);
                }
            }

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                var abstractProperties = GetAbtractPropertiesWithCustomAttributes(type).ToArray();

                foreach (var property in type.Properties)
                {
                    if (property.IsOverride)
                    {
                        var abstractProperty = abstractProperties.FirstOrDefault(x => x.Name == property.Name);

                        if (abstractProperty != null)
                            foreach (var item in getRelevantAttributes(abstractProperty, property))
                                result.Add(item);
                    }

                    if (!property.propertyDefinition.HasCustomAttributes)
                        continue;

                    foreach (var item in getRelevantAttributes(property, property))
                        result.Add(item);
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        private static IEnumerable<Property> GetAbtractPropertiesWithCustomAttributes(BuilderType builderType)
        {
            foreach (var baseClass in builderType.BaseClasses)
                foreach (var item in baseClass.typeDefinition.Properties)
                    if ((item.GetMethod ?? item.SetMethod).With(x => x.IsAbstract && x.IsVirtual) && item.HasCustomAttributes)
                        yield return new Property(baseClass, item);
        }

        #endregion Property Finders

        #region Method Finders

        public static IEnumerable<Method> FindMethods(string regexPattern) => FindMethods(SearchContext.Module, regexPattern);

        public static IEnumerable<Method> FindMethods(SearchContext searchContext, string regexPattern) => GetTypes(searchContext).SelectMany(x => x.Methods).Where(x => Regex.IsMatch(x.Name, regexPattern, RegexOptions.Singleline));

        public static IEnumerable<AttributedMethod> FindMethodsByAttribute(Type attributeType) => FindMethodsByAttribute(SearchContext.Module, attributeType);

        public static IEnumerable<AttributedMethod> FindMethodsByAttribute(SearchContext searchContext, Type attributeType) => FindMethodsByAttribute(searchContext, attributeType.FullName);

        public static IEnumerable<AttributedMethod> FindMethodsByAttribute(string attributeName) => FindMethodsByAttribute(SearchContext.Module, attributeName);

        public static IEnumerable<AttributedMethod> FindMethodsByAttribute(SearchContext searchContext, string attributeName)
        {
            var result = new ConcurrentBag<AttributedMethod>();

            IEnumerable<AttributedMethod> getRelevantAttributes(Method method, Method owningMethod)
            {
                var asyncResult = method.GetAsyncMethod();
                for (int i = 0; i < method.methodDefinition.CustomAttributes.Count; i++)
                {
                    var fullname = method.methodDefinition.CustomAttributes[i].AttributeType.Resolve().FullName;
                    if (attributeName.GetHashCode() == fullname.GetHashCode() && fullname == attributeName)
                    {
                        if (asyncResult == null)
                            yield return new AttributedMethod(owningMethod, method.methodDefinition.CustomAttributes[i], asyncResult);
                        else
                            yield return new AttributedMethod(owningMethod, method.methodDefinition.CustomAttributes[i], asyncResult);
                    }
                }
            }

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                var abstractMethods = GetAbtractMethodsWithCustomAttributes(type).ToArray();
                foreach (var method in type.Methods)
                {
                    if (method.IsOverride)
                    {
                        var parameters = method.Parameters;
                        var abstractMethod = abstractMethods.FirstOrDefault(x => x.Name == method.Name && method.Parameters.SequenceEqual(parameters));

                        if (abstractMethod != null)
                            foreach (var item in getRelevantAttributes(abstractMethod, method))
                                result.Add(item);
                    }

                    if (!method.methodDefinition.HasCustomAttributes)
                        continue;

                    foreach (var item in getRelevantAttributes(method, method))
                        result.Add(item);
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<AttributedMethod> FindMethodsByAttributes(IEnumerable<BuilderType> types) => FindMethodsByAttributes(SearchContext.Module, types);

        public static IEnumerable<AttributedMethod> FindMethodsByAttributes(SearchContext searchContext, IEnumerable<BuilderType> types)
        {
            var result = new ConcurrentBag<AttributedMethod>();
            var attributes = types.Select(x => x.Fullname).ToList();

            IEnumerable<AttributedMethod> getRelevantAttributes(Method method, Method owningMethod)
            {
                var asyncResult = owningMethod.GetAsyncMethod();
                for (int i = 0; i < method.methodDefinition.CustomAttributes.Count; i++)
                {
                    if (attributes.Contains(method.methodDefinition.CustomAttributes[i].AttributeType.Resolve().FullName))
                    {
                        if (asyncResult == null)
                            yield return new AttributedMethod(owningMethod, method.methodDefinition.CustomAttributes[i], asyncResult);
                        else
                            yield return new AttributedMethod(owningMethod, method.methodDefinition.CustomAttributes[i], asyncResult);
                    }
                }
            }

            Parallel.ForEach(GetTypes(searchContext), CecilatorCancellationToken.Current, type =>
            {
                var abstractMethods = GetAbtractMethodsWithCustomAttributes(type).ToArray();

                foreach (var method in type.Methods)
                {
                    if (method.IsOverride)
                    {
                        var parameters = method.Parameters;
                        var abstractMethod = abstractMethods.FirstOrDefault(x => x.Name == method.Name && method.Parameters.SequenceEqual(parameters));

                        if (abstractMethod != null)
                            foreach (var item in getRelevantAttributes(abstractMethod, method))
                                result.Add(item);
                    }

                    if (!method.methodDefinition.HasCustomAttributes)
                        continue;

                    foreach (var item in getRelevantAttributes(method, method))
                        result.Add(item);
                }
                CecilatorCancellationToken.Current.ThrowIfCancellationRequested();
            });

            return result;
        }

        public static IEnumerable<Method> FindMethodsByName(string methodName, int parameterCount) => FindMethodsByName(SearchContext.Module, methodName, parameterCount);

        public static IEnumerable<Method> FindMethodsByName(SearchContext searchContext, string methodName, int parameterCount) => GetTypes(searchContext).SelectMany(x => x.GetMethods(methodName, parameterCount, false));

        public static IEnumerable<Method> FindMethodsByName(string methodName) => FindMethodsByName(SearchContext.Module, methodName);

        public static IEnumerable<Method> FindMethodsByName(SearchContext searchContext, string methodName) => GetTypes(searchContext).SelectMany(x => x.GetMethods(methodName, 0));

        private static IEnumerable<Method> GetAbtractMethodsWithCustomAttributes(BuilderType builderType)
        {
            foreach (var baseClass in builderType.BaseClasses)
                foreach (var item in baseClass.typeDefinition.Methods)
                    if (item.IsAbstract && item.IsVirtual && item.HasCustomAttributes && item.Body == null)
                        yield return new Method(baseClass, item);
        }

        #endregion Method Finders

        #region Attribute Finders

        private static IEnumerable<BuilderType> findAttributesInModuleCache;

        public static IEnumerable<BuilderType> FindAttributesByBaseClass(string baseClassName) => FindAttributesInModule().Where(x => x.BaseClasses.Any(y => y.Fullname.GetHashCode() == baseClassName.GetHashCode() && y.Fullname == baseClassName));

        public static IEnumerable<BuilderType> FindAttributesByInterfaces(Type[] interfaceTypes) => FindAttributesByInterfaces(interfaceTypes.Select(x => x.FullName).ToArray());

        public static IEnumerable<BuilderType> FindAttributesByInterfaces(IEnumerable<BuilderType> interfaceTypes) => FindAttributesInModule().Where(x => interfaceTypes.Any(y => x.Implements(y)));

        public static IEnumerable<BuilderType> FindAttributesByInterfaces(params string[] interfaceName) => FindAttributesInModule().Where(x => x != null && interfaceName.Any(y => x.Implements(y)));

        public static IEnumerable<BuilderType> FindAttributesInModule()
        {
            if (findAttributesInModuleCache == null)
            {
                var stopwatch = Stopwatch.StartNew();

                findAttributesInModuleCache = GetTypesInternal(SearchContext.Module)
                   .SelectMany(x =>
                   {
                       var type = x.Resolve();
                       return type
                           .CustomAttributes
                               .Concat(type.Methods.SelectMany(y => y.CustomAttributes))
                               .Concat(type.Fields.SelectMany(y => y.CustomAttributes))
                               .Concat(type.Properties.SelectMany(y => y.CustomAttributes));
                   })
                   .Concat(parameters.ModuleDefinition.CustomAttributes)
                   .Concat(parameters.ModuleDefinition.Assembly.CustomAttributes)
                   .Distinct(new CustomAttributeEqualityComparer())
                   .Select(x => new BuilderType(x.AttributeType));

                stopwatch.Stop();

                logging.Log($"Finding attributes took {stopwatch.Elapsed.TotalMilliseconds}ms");

                findAttributesInModuleCache = findAttributesInModuleCache.OrderBy(x => x.ToString()).Distinct(new BuilderTypeEqualityComparer()).ToArray();
            }

            return findAttributesInModuleCache;
        }

        #endregion Attribute Finders

        #region Getting types

        public static BuilderType GetType(Type type, bool throwExceptionIfNotFound = true)
        {
            if (type.IsArray)
            {
                var child = type.GetElementType();
                var bt = GetType(child.FullName, throwExceptionIfNotFound);
                return new BuilderType(new ArrayType(parameters.ModuleDefinition.ImportReference(bt.typeReference)));
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() != type)
            {
                var definition = type.GetGenericTypeDefinition();
                var typeDefinition = GetType(definition.FullName, throwExceptionIfNotFound);

                return typeDefinition.MakeGeneric(type.GetGenericArguments().Select(x => x.ToBuilderType()).ToArray());
            }

            return GetType(type.FullName, throwExceptionIfNotFound);
        }

        /// <summary>
        /// Gets a type from its name. Default search context is <see cref="SearchContext.AllReferencedModules"/>
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="throwExceptionIfNotFound"></param>
        /// <returns></returns>
        public static BuilderType GetType(string typeName, bool throwExceptionIfNotFound = true) => GetType(typeName, SearchContext.AllReferencedModules, throwExceptionIfNotFound);

        public static BuilderType GetType(string typeName, SearchContext searchContext, bool throwExceptionIfNotFound = true)
        {
            var dictionary =  GetTypeSource(searchContext);

            if (dictionary.TryGetValue(typeName, out TypeDefinition type))
                return new BuilderType(type);
            else
            {
                var result = GetTypesInternal(searchContext).FirstOrDefault(x=> x.FullName == typeName);
                if (result != null)
                    return new BuilderType(result);
            }

            if (throwExceptionIfNotFound)
                throw new TypeNotFoundException($"The type '{typeName}' does not exist in any of the referenced assemblies.");

            return null;
        }

        public static IEnumerable<BuilderType> GetTypes() => allTypes.Values.Select(x => new BuilderType(x));

        public static IEnumerable<BuilderType> GetTypes(SearchContext searchContext) => GetTypeSource(searchContext).Values.Select(x => new BuilderType(x));

        public static IEnumerable<BuilderType> GetTypesInNamespace(string namespaceName) => GetTypesInNamespace(SearchContext.Module, namespaceName);

        public static IEnumerable<BuilderType> GetTypesInNamespace(SearchContext searchContext, string namespaceName) => GetTypes(searchContext).Where(x => x.Namespace == namespaceName);

        public static BuilderType MakeArray(BuilderType type) => new BuilderType(new ArrayType(parameters.ModuleDefinition.ImportReference(type.typeReference)));

        public static bool TypeExists(string typeName) => allTypes.ContainsKey(typeName);

        public static bool TypeExists(string typeName, SearchContext searchContext) => GetTypeSource(searchContext).ContainsKey(typeName);

        internal static IEnumerable<TypeReference> GetTypesInternal() => GetTypesInternal(SearchContext.Module);

        internal static IEnumerable<TypeReference> GetTypesInternal(SearchContext searchContext)
        {
            foreach (var item in GetTypeSource(searchContext))
                yield return item.Value;
        }

        private static FastDictionary<string, TypeDefinition> GetTypeSource(SearchContext searchContext)
        {
            switch (searchContext)
            {
                case SearchContext.Module: return moduleTypes;

                case SearchContext.Module_NoGenerated: return moduleTypesNoGenerated;

                case SearchContext.AllReferencedModules: return allTypes;
            }

            throw new InvalidOperationException("Unknown value of " + nameof(searchContext));
        }

        #endregion Getting types

        #region Create Type

        public static BuilderType CreateType(string namespaceName, string typeName) => CreateType(namespaceName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.Serializable, typeName, parameters.ModuleDefinition.TypeSystem.Object);

        public static BuilderType CreateType(string namespaceName, TypeAttributes attributes, string typeName) => CreateType(namespaceName, attributes, typeName, parameters.ModuleDefinition.TypeSystem.Object);

        public static BuilderType CreateType(string namespaceName, TypeAttributes attributes, string typeName, BuilderType baseType) => CreateType(namespaceName, attributes, typeName, baseType.typeReference);

        private static BuilderType CreateType(string namespaceName, TypeAttributes attributes, string typeName, TypeReference baseType)
        {
            var newType = new TypeDefinition(namespaceName, typeName, attributes, parameters.ModuleDefinition.ImportReference(baseType));
            parameters.ModuleDefinition.Types.Add(newType);

            return new BuilderType(newType);
        }

        #endregion Create Type

        #region Imports

        public static Method Import(System.Reflection.MethodBase value)
        {
            var result = parameters.ModuleDefinition.ImportReference(value);
            return new Method(new BuilderType(result.DeclaringType), result, result.Resolve());
        }

        public static MethodReference Import(MethodReference methodReference) => parameters.ModuleDefinition.ImportReference(methodReference);

        public static FieldReference Import(FieldReference fieldReference) => parameters.ModuleDefinition.ImportReference(fieldReference);

        public static MethodReference Import(MethodReference method, IGenericParameterProvider context) => parameters.ModuleDefinition.ImportReference(method, context);

        public static TypeReference Import(Type type) => parameters.ModuleDefinition.ImportReference(type);

        public static TypeReference Import(TypeReference typeReference) => parameters.ModuleDefinition.ImportReference(typeReference);

        public static Method Import(Method method)
        {
            var result = parameters.ModuleDefinition.ImportReference(method.methodReference);
            return new Method(new BuilderType(result.DeclaringType), result, result.Resolve());
        }

        #endregion Imports
    }
}