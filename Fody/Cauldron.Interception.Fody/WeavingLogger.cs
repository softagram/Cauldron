using Cauldron.Interception.Cecilator;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace Cauldron.Interception.Fody
{
    internal class WeavingLogger : ICecilatorLogging
    {
        private readonly BaseModuleWeaver moduleWeaver;

        public WeavingLogger(BaseModuleWeaver moduleWeaver) => this.moduleWeaver = moduleWeaver;

        public void Log(object arg) => this.moduleWeaver.LogInfo(arg as string ?? arg?.ToString() ?? "");

        public void Log(LogTypes logTypes, Instruction instruction, MethodDefinition methodDefinition, object arg)
        {
            if (!Builder.Parameters.IsVerbose && logTypes != LogTypes.Error)
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

        public void Log(Exception e) => this.moduleWeaver.LogError(e.GetStackTrace());

        public void Log(LogTypes logTypes, MethodDefinition methodDefinition, object arg) => this.Log(logTypes, methodDefinition.GetSequencePoint(), arg);

        public void Log(LogTypes logTypes, SequencePoint sequencePoint, object arg)
        {
            if (!Builder.Parameters.IsVerbose && logTypes != LogTypes.Error)
                return;

            switch (logTypes)
            {
                case LogTypes.Error:
                    if (sequencePoint == null)
                        this.moduleWeaver.LogError(arg as string ?? arg?.ToString() ?? "");
                    else
                        this.moduleWeaver.LogErrorPoint(arg as string ?? arg?.ToString() ?? "", sequencePoint);

                    break;

                case LogTypes.Warning:
                    if (sequencePoint == null)
                        this.moduleWeaver.LogWarning(arg as string ?? arg?.ToString() ?? "");
                    else
                        this.moduleWeaver.LogWarningPoint(arg as string ?? arg?.ToString() ?? "", sequencePoint);

                    break;

                case LogTypes.Info:
                    this.moduleWeaver.LogInfo(arg as string ?? arg?.ToString() ?? "");
                    break;
            }
        }

        public void Log(Exception e, string message) => this.moduleWeaver.LogError(e.GetStackTrace() + "\r\n" + message);
    }
}