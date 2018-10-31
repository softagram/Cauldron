using Cauldron.Interception.Cecilator;
using Cauldron.Interception.Fody.HelperTypes;
using Fody;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Cauldron.Interception.Fody
{
    public sealed partial class ModuleWeaver : BaseModuleWeaver
    {
        #region From Ceceilator

        private Configuration configuration;

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static IEnumerable<TypeDefinition> AllTypes { get; internal set; }

        public BuilderOld Builder { get; private set; }

        /// <summary>
        /// Gets the project name based on the project directory path.
        /// </summary>
        public string ProjectName => this.ProjectDirectoryPath
            .With(x => x.Substring(x.LastIndexOf('\\', this.ProjectDirectoryPath.Length - 2) + 1))
            .Replace("\\", "");

        /// <exclude/>
        public override void AfterWeaving()
        {
            AllTypes = null;

            this.Builder = null;
            this.LogErrorPoint = null;
            this.LogError = null;
            this.LogInfo = null;
            this.LogWarningPoint = null;
            this.LogWarning = null;
        }

        /// <exclude/>
        public override void Cancel()
        {
            base.Cancel();
            Cecilator.Builder.Cancel();
        }

        /// <exclude/>
        public override void Execute()
        {
            this.configuration = new Configuration(this.Config);
            Cecilator.Builder.Initialize(new CecilatorParameters(this), new WeavingLogger(this), this.GetAssemblyDefinitions());

            try
            {
                if (bool.TryParse(this.Config.Attribute("Verbose")?.Value?.ToString() ?? "true", out bool result))
                    IsVerbose = result;
                else
                    IsVerbose = true;

                this.Initialize(this.LogInfo, this.LogWarning, this.LogWarningPoint, this.LogError, this.LogErrorPoint);

                this.Builder = this.CreateBuilder();
                this.OnExecute();
            }
            catch (TargetInvocationException e) when (e.GetBaseException().GetType() == typeof(OperationCanceledException))
            {
            }
            catch (TargetInvocationException e) when (e.GetBaseException().GetType() == typeof(ObjectDisposedException))
            {
                this.Log(LogTypes.Error, "An Error has occured.");
            }
            catch
            {
                throw;
            }
        }

        /// <exclude/>
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "netstandard";
            yield return "mscorlib";
            yield return "System";
            yield return "System.Runtime";
            yield return "System.Core";
        }

        /// <exclude/>
        protected abstract void OnExecute();

        private IEnumerable<AssemblyDefinition> GetAssemblyDefinitions()
        {
            if (this.configuration.ReferenceRecursive)
                foreach (var item in this.ModuleDefinition.AssemblyReferences.Resolve().GetAllReferencedAssemblies())
                    yield return item;
            else
                foreach (var item in this.ModuleDefinition.AssemblyReferences.Resolve())
                    yield return item;

            foreach (var item in this.References.Split(';').LoadAssemblies())
                yield return item;

            if (this.configuration.ReferenceCopyLocal)
                foreach (var item in Cecilator.Builder.ReferenceCopyLocal)
                    yield return item;
        }

        #region Implementation from CecilatorObject due to breaking changes in FOdy 3.0.0

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<string> logError;

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<string, SequencePoint> logErrorPoint;

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<string> logInfo;

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<string> logWarning;

        [EditorBrowsable(EditorBrowsableState.Never), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<string, SequencePoint> logWarningPoint;

        public void Log(LogTypes logTypes, Instruction instruction, MethodDefinition methodDefinition, object arg)
        {
            if (!IsVerbose && logTypes != LogTypes.Error)
                return;

            var next = instruction;
            while (next != null)
            {
                var result = methodDefinition.DebugInformation.GetSequencePoint(next);
                if (result != null)
                {
                    this.Log(logTypes, result, arg);
                    return;
                }

                next = next.Next;
            }

            var previous = instruction;
            while (previous != null)
            {
                var result = methodDefinition.DebugInformation.GetSequencePoint(previous);
                if (result != null)
                {
                    this.Log(logTypes, result, arg);
                    return;
                }

                previous = previous.Previous;
            }

            this.Log(logTypes, methodDefinition, arg);
        }

        public void Log(LogTypes logTypes, MethodDefinition method, object arg) => this.Log(logTypes, method.GetSequencePoint(), arg);

        public void Log(LogTypes logTypes, SequencePoint sequencePoint, object arg)
        {
            if (!IsVerbose && logTypes != LogTypes.Error)
                return;

            switch (logTypes)
            {
                case LogTypes.Error:
                    if (sequencePoint == null)
                        this.logError(arg as string ?? arg?.ToString() ?? "");
                    else
                        this.logErrorPoint(arg as string ?? arg?.ToString() ?? "", sequencePoint);

                    break;

                case LogTypes.Warning:
                    if (sequencePoint == null)
                        this.logWarning(arg as string ?? arg?.ToString() ?? "");
                    else
                        this.logWarningPoint(arg as string ?? arg?.ToString() ?? "", sequencePoint);

                    break;

                case LogTypes.Info:
                    this.logInfo(arg as string ?? arg?.ToString() ?? "");
                    break;
            }
        }

        protected void Initialize(
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string, SequencePoint> logWarningPoint,
            Action<string> logError,
            Action<string, SequencePoint> logErrorPoint)
        {
            this.logError = logError;
            this.logErrorPoint = logErrorPoint;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.logWarningPoint = logWarningPoint;
        }

        protected void Log(object arg) => this.logInfo(arg as string ?? arg?.ToString() ?? "");

        protected void Log(Exception e) => this.logError(e.GetStackTrace());

        protected void Log(Exception e, string message) => this.logError(e.GetStackTrace() + "\r\n" + message);

        #endregion Implementation from CecilatorObject due to breaking changes in FOdy 3.0.0

        #endregion From Ceceilator

        public string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            var result = path
                .Trim()
                .Replace("$(SolutionPath)", this.SolutionDirectoryPath.AddBackslash())
                .Replace("$(ProjectDir)", this.ProjectDirectoryPath.AddBackslash());

            if (result.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return result;

            try
            {
                return Path.GetFullPath(result);
            }
            catch
            {
                return result;
            }
        }

        protected override void OnExecute()
        {
            var versionAttribute = typeof(ModuleWeaver)
                .Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), true)
                .FirstOrDefault() as System.Reflection.AssemblyFileVersionAttribute;

            this.Log($"Cauldron Interception v" + versionAttribute.Version);

            this.CreateCauldronEntry(this.Builder);
            this.AddAssemblyWideAttributes(this.Builder);
            this.ExecuteInterceptionScripts(this.Builder);
            this.AddEntranceAssemblyHACK(this.Builder);
            this.ExecuteModuleAddition(this.Builder);
        }

        private void AddEntranceAssemblyHACK(BuilderOld builder)
        {
            var assembly = builder.GetType("System.Reflection.Assembly").Import().With(x => new { Type = x, Load = x.GetMethod("Load", 1).Import() });
            var cauldron = builder.GetType("CauldronInterceptionHelper", SearchContext.Module);
            var referencedAssembliesMethod = cauldron.CreateMethod(Modifiers.PublicStatic, builder.MakeArray(assembly.Type), "GetReferencedAssemblies");
            var voidMain = builder.GetMain();

            // Add the Entrance Assembly and referenced assemblies hack for UWP
            if (builder.TypeExists("Cauldron.Reflection.AssembliesCore"))
            {
                var assemblies = builder.GetType("Cauldron.Reflection.AssembliesCore").Import().With(y => new
                {
                    Type = y,
                    SetEntryAssembly = y.GetMethod("SetEntryAssembly", 1).Import(),
                    SetReferenceAssemblies = y.GetMethod("SetReferenceAssemblies", 1).Import()
                });

                if (voidMain != null && builder.IsUWP)
                {
                    voidMain.NewCoder().Context(context =>
                    {
                        return context.Call(assemblies.SetEntryAssembly, x => GetAssemblyWeaver.AddCode(x, voidMain.DeclaringType))
                            .End
                            .Call(assemblies.SetReferenceAssemblies, x => x.Call(referencedAssembliesMethod))
                            .End;
                    })
                    .Insert(InsertionPosition.Beginning);
                }
                else
                {
                    var module = builder.GetType("<Module>", SearchContext.Module);
                    module
                        .CreateStaticConstructor()
                        .NewCoder()
                        .Call(assemblies.SetEntryAssembly, x =>
                            GetAssemblyWeaver.AddCode(x, module))
                            .End
                        .Insert(InsertionPosition.Beginning);
                }
            }

            this.CreateAssemblyListingArray(builder, referencedAssembliesMethod, assembly.Type, builder.ReferencedAssemblies);
        }

        private void CreateAssemblyListingArray(BuilderOld builder, Method method, BuilderType assemblyType, IEnumerable<AssemblyDefinition> assembliesToList)
        {
            method.NewCoder().Context(context =>
            {
                var returnValue = context.GetOrCreateReturnVariable();
                var referencedTypes = this.FilterAssemblyList(assembliesToList.Distinct(new AssemblyDefinitionEqualityComparer())).ToArray();

                if (referencedTypes.Length > 0)
                {
                    context.SetValue(returnValue, x => x.Newarr(assemblyType, referencedTypes.Length));

                    for (int i = 0; i < referencedTypes.Length; i++)
                        context.Load(returnValue).StoreElement(GetAssemblyWeaver.AddCode(context.NewCoder(), referencedTypes[i].ToBuilderType().Import()), i);
                }

                return context.Load(returnValue).Return();
            }).Replace();
        }

        private void CreateCauldronEntry(BuilderOld builder)
        {
            BuilderType cauldron = null;

            if (builder.TypeExists("CauldronInterceptionHelper", SearchContext.Module))
                cauldron = builder.GetType("CauldronInterceptionHelper", SearchContext.Module);
            else
            {
                cauldron = builder.CreateType("", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, "CauldronInterceptionHelper");
                cauldron.CustomAttributes.AddCompilerGeneratedAttribute();
            }

            cauldron.CustomAttributes.AddDebuggerDisplayAttribute(cauldron.Assembly.Name.Name);
        }

        private int ExecuteExternalApplication(string exePath, string[] arguments, string workingDirectory)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Join(" ", arguments),
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.OutputDataReceived += (s, e) => Builder.Log(LogTypes.Info, e.Data);
            process.ErrorDataReceived += (s, e) => Builder.Log(LogTypes.Info, e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return process.ExitCode;
        }

        private void ExecuteModuleAddition(BuilderOld builder)
        {
            using (new StopwatchLog(this, "ModuleLoad"))
            {
                var assembly = builder.GetType(typeof(System.Reflection.Assembly));
                var cauldron = builder.GetType("CauldronInterceptionHelper", SearchContext.Module);
                var arrayType = assembly.MakeArray();
                var referencedAssembliesMethod = cauldron.GetMethod("GetReferencedAssemblies");

                // First find a type without namespace and with a static method called ModuleLoad
                var onLoadMethods = builder.FindMethodsByName(SearchContext.Module_NoGenerated, "ModuleLoad", 1)
                    .Where(x => x.IsStatic && x.ReturnType == BuilderTypes.Void && x.Parameters[0] == arrayType)
                    .Where(x => x != null);

                if (!onLoadMethods.Any())
                    return;

                if (onLoadMethods.Count() > 1)
                {
                    this.Log(LogTypes.Error, onLoadMethods.FirstOrDefault(), "There is more than one 'static ModuleLoad(Assembly[])' in the program.");
                    return;
                }

                var onLoadMethod = onLoadMethods.First();
                var module = builder.GetType("<Module>", SearchContext.Module);

                module
                    .CreateStaticConstructor().NewCoder()
                    .Call(onLoadMethod, x => x.Call(referencedAssembliesMethod))
                    .End
                    .Insert(InsertionPosition.End);
            }
        }

        private IEnumerable<TypeDefinition> FilterAssemblyList(IEnumerable<AssemblyDefinition> assemblies)
        {
            var excludeUs = this.GetAssemblyExclusionList().ToArray();
            var onlyIncludeUs = this.GetAssemblyOnlyInclusionList().ToArray();

            foreach (var item in assemblies)
            {
                if (item == null)
                    continue;

                if (item.FullName == null)
                    continue;

                if (item.Name.Name.StartsWith("Microsoft."))
                    continue;

                if (item.Name.Name.StartsWith("System."))
                    continue;

                if (item.Name.Name.StartsWith("Windows."))
                    continue;

                if (item.Name.Name == "testhost")
                    continue;

                if (item.Name.Name == "mscorlib")
                    continue;

                if (item.Name.Name == "System")
                    continue;

                if (item.Name.Name == "netstandard")
                    continue;

                if (item.Name.Name == "WindowsBase")
                    continue;

                if (onlyIncludeUs.Any(x => !item.Name.Name.StartsWith(x)))
                    continue;

                if (excludeUs.Any(x => item.Name.Name.StartsWith(x)))
                    continue;

                foreach (var type in item.MainModule.Types)
                {
                    if (!type.IsPublic)
                        continue;

                    if (type.IsGenericParameter)
                        continue;

                    if (type.ContainsGenericParameter)
                        continue;

                    if (type.IsEnum)
                        continue;

                    if (type.IsInterface)
                        continue;

                    if (type.FullName.IndexOf('.') < 0)
                        continue;

                    if (type.FullName.IndexOf('<') >= 0)
                        continue;

                    if (type.FullName.IndexOf('>') >= 0)
                        continue;

                    if (type.FullName.IndexOf('`') >= 0)
                        continue;

                    if (type.Namespace.StartsWith("System."))
                        continue;

                    yield return type;
                    break;
                }
            }
        }

        private IEnumerable<string> GetAssemblyExclusionList()
        {
            var element = this.Config.Element("ExcludeAssemblies");

            if (element == null)
                yield break;

            foreach (var item in element.Value.Split(new[] { "\r\n", "\n", ", ", " " }, StringSplitOptions.RemoveEmptyEntries))
                yield return item;
        }

        private IEnumerable<string> GetAssemblyOnlyInclusionList()
        {
            var element = this.Config.Element("OnlyIncludeAssemblies");

            if (element == null)
                yield break;

            foreach (var item in element.Value.Split(new[] { "\r\n", "\n", ", ", " " }, StringSplitOptions.RemoveEmptyEntries))
                yield return item;
        }
    }
}