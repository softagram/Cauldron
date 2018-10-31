using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace Cauldron.Interception.Cecilator
{
    /// <summary>
    /// Represents the logging methods for Cecilator
    /// </summary>
    public interface ICecilatorLogging
    {
        /// <summary>
        /// Logs a string as <see cref="LogTypes.Info"/>.
        /// </summary>
        /// <param name="arg">The information to log.</param>
        void Log(object arg);

        /// <summary>
        /// Logs a method using <see cref="Instruction"/> information.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="instruction">The instruction in the <paramref name="methodDefinition"/>'s body.</param>
        /// <param name="methodDefinition">The <see cref="MethodDefinition"/> to log.</param>
        /// <param name="arg">The message to log.</param>
        void Log(LogTypes logTypes, Instruction instruction, MethodDefinition methodDefinition, object arg);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        void Log(Exception e);

        /// <summary>
        /// Logs a method.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="methodDefinition">The <see cref="MethodDefinition"/> to log.</param>
        /// <param name="arg">The message to log.</param>
        void Log(LogTypes logTypes, MethodDefinition methodDefinition, object arg);

        /// <summary>
        /// Logs using the <see cref="SequencePoint"/>'s information.
        /// </summary>
        /// <param name="logTypes">Any value of <see cref="LogTypes"/>.</param>
        /// <param name="sequencePoint">The sequencepoint to log.</param>
        /// <param name="arg">The message to log.</param>
        void Log(LogTypes logTypes, SequencePoint sequencePoint, object arg);

        /// <summary>
        /// Logs an exception with an additional error message.
        /// </summary>
        /// <param name="e">The exception to log.</param>
        /// <param name="message">The message to log.</param>
        void Log(Exception e, string message);
    }
}