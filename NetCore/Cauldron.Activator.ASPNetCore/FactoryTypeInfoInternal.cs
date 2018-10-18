using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace Cauldron.Activator.ASPNetCore
{
    /// <exclude/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class FactoryTypeInfoInternal : IFactoryTypeInfo
    {
        private readonly Func<object> createInstance;
        private readonly object instanceObject = new object();
        private readonly bool unkillable;

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal FactoryTypeInfoInternal(ServiceDescriptor serviceDescriptor, Func<object> createInstance)
        {
            var type = serviceDescriptor.ImplementationType ?? serviceDescriptor.ImplementationInstance?.GetType();

            this.ContractName = type?.GetCustomAttribute<ProviderAliasAttribute>()?.Alias ?? serviceDescriptor.ServiceType.FullName;
            this.ContractType = serviceDescriptor.ServiceType;
            this.CreationPolicy = serviceDescriptor.Lifetime.ToCreationPolicy();
            this.Type = type;
            this.createInstance = createInstance;
            this.IsEnumerable = serviceDescriptor.ImplementationType.IsArray || serviceDescriptor.ImplementationType.ImplementsInterface(typeof(IEnumerable));
            this.ChildType = serviceDescriptor.ImplementationType.GetChildrenType();
            this.Instance = serviceDescriptor.ImplementationInstance;
        }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Type ChildType { get; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ContractName { get; private set; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Type ContractType { get; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public FactoryCreationPolicy CreationPolicy { get; private set; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public object Instance { get; set; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEnumerable { get; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public uint Priority { get; private set; } = 0;

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Type Type { get; private set; }

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public object CreateInstance(params object[] arguments) => this.CreateInstance();

        /// <exclude/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public object CreateInstance()
        {
            if (this.CreationPolicy == FactoryCreationPolicy.Instanced)
                return this.createInstance();

            if (this.Instance == null)
                lock (this.instanceObject)
                {
                    this.Instance = this.createInstance();
                }

            return this.Instance;
        }
    }
}