using Mono.Cecil;
using System.Collections.Generic;

namespace Cauldron.Interception.Cecilator
{
    /// <summary>
    /// Represents the parameters required to initialize Cecilator
    /// </summary>
    public interface ICecilatorParameters
    {
        /// <summary>
        /// The full directory path of the current weaver.
        /// </summary>
        string AddinDirectoryPath { get; }

        /// <summary>
        /// The full path of the target assembly.
        /// </summary>
        string AssemblyFilePath { get; }

        /// <summary>
        /// A list of all the msbuild constants.
        /// </summary>
        List<string> DefineConstants { get; }

        /// <summary>
        /// Gets or sets a value that indicates if the weaver should be logging everything or not.
        /// </summary>
        bool IsVerbose { get; set; }

        /// <summary>
        /// An instance of <see cref="ModuleDefinition"/> for processing.
        /// </summary>
        ModuleDefinition ModuleDefinition { get; }

        /// <summary>
        /// The full directory path of the target project.
        /// </summary>
        string ProjectDirectoryPath { get; }

        /// <summary>
        /// Gets the project name.
        /// </summary>
        string ProjectName { get; }

        /// <summary>
        /// A list of all the references marked as copy-local.
        /// </summary>
        List<string> ReferenceCopyLocalPaths { get; }

        /// <summary>
        /// A semicolon delimited string that contains all the references for the target project.
        /// </summary>
        string References { get; }

        /// <summary>
        /// The full directory path of the current solution.
        /// </summary>
        string SolutionDirectoryPath { get; }
    }
}