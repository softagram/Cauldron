using System;
using System.ComponentModel;

namespace Cauldron.Activator
{
    /// <exclude/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class FactoryContainer : IServiceProvider
    {
        /// <exclude/>
        public object GetService(Type serviceType) => Factory.Create(serviceType);
    }
}