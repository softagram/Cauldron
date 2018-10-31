using Cauldron.Interception.Cecilator;
using Fody;
using Mono.Cecil;
using System.Collections.Generic;

namespace Cauldron.Interception.Fody
{
    internal sealed class CecilatorParameters : ICecilatorParameters
    {
        public CecilatorParameters(BaseModuleWeaver moduleWeaver)
        {
        }

        public string AddinDirectoryPath { get; }
        public string AssemblyFilePath { get; }
        public List<string> DefineConstants { get; }
        public bool IsVerbose { get; set; }
        public ModuleDefinition ModuleDefinition { get; }
        public string ProjectDirectoryPath { get; }
        public string ProjectName { get; }
        public List<string> ReferenceCopyLocalPaths { get; }
        public string References { get; }
        public string SolutionDirectoryPath { get; }
    }
}