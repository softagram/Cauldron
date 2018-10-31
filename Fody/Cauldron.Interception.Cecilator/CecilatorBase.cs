using Cauldron.Interception.Cecilator.Coders;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace Cauldron.Interception.Cecilator
{
    /// <exclude/>
    public abstract class CecilatorBase : ICecilatorLogging
    {
        private readonly ICecilatorLogging logging;

        internal CecilatorBase()
        {
            this.logging = Builder.logging;
            this.ModuleDefinition = Builder.Parameters.ModuleDefinition;
            this.Identification = CodeBlocks.GenerateName();
        }

        /// <summary>
        /// Gets a unique identification string for this object.
        /// </summary>
        public virtual string Identification { get; private set; }

        /// <summary>
        /// An instance of <see cref="ModuleDefinition"/> for processing.
        /// </summary>
        public ModuleDefinition ModuleDefinition { get; }

        #region logging

        /// <summary>
        /// Logs a string as <see cref="LogTypes.Info"/>.
        /// </summary>
        /// <param name="arg">The information to log.</param>
        public void Log(object arg) => this.logging.Log(arg);

        /// <summary>
        /// Logs a method using <see cref="Instruction"/> information.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="instruction">The instruction in the <paramref name="methodDefinition"/>'s body.</param>
        /// <param name="methodDefinition">The <see cref="MethodDefinition"/> to log.</param>
        /// <param name="arg">The message to log.</param>
        public void Log(LogTypes logTypes, Instruction instruction, MethodDefinition methodDefinition, object arg) =>
            this.logging.Log(logTypes, instruction, methodDefinition, arg);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        public void Log(Exception e) => this.logging.Log(e);

        /// <summary>
        /// Logs a method.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="methodDefinition">The <see cref="MethodDefinition"/> to log.</param>
        /// <param name="arg">The message to log.</param>
        public void Log(LogTypes logTypes, MethodDefinition methodDefinition, object arg) =>
            this.logging.Log(logTypes, methodDefinition, arg);

        /// <summary>
        /// Logs using the <see cref="SequencePoint"/>'s information.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="sequencePoint">The sequencepoint to log.</param>
        /// <param name="arg">The message to log.</param>
        public void Log(LogTypes logTypes, SequencePoint sequencePoint, object arg) =>
            this.logging.Log(logTypes, sequencePoint, arg);

        /// <summary>
        /// Logs an exception with an additional error message.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        /// <param name="message">The message to log.</param>
        public void Log(Exception e, string message) => this.logging.Log(e, message);

        #endregion logging
    }
}