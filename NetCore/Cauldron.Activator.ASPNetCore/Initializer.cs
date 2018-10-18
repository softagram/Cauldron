using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Cauldron.Activator.ASPNetCore
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Initializer
    {
        public static IServiceProvider BuildServiceProvider(IServiceCollection services)
        {
            var typeInfos = new List<IFactoryTypeInfo>();
            foreach (var item in services)
            {
                var constructor = item.ImplementationType.GetConstructors();

                if (item.ImplementationInstance == null && item.ImplementationType == null)
                    typeInfos.Add(new FactoryTypeInfoInternal(item, () => null));
                else
                    typeInfos.Add(new FactoryTypeInfoInternal(item, () =>
                    {
                    }));
            }
            Factory.AddTypes(typeInfos);
            services.Clear();

            return Factory.CreateServiceProvider();
        }

        internal static FactoryCreationPolicy ToCreationPolicy(this ServiceLifetime serviceLifetime)
        {
            switch (serviceLifetime)
            {
                case ServiceLifetime.Singleton:
                    return FactoryCreationPolicy.Singleton;

                case ServiceLifetime.Scoped:
                    throw new NotImplementedException();
                case ServiceLifetime.Transient:
                    return FactoryCreationPolicy.Instanced;
            }

            throw new NotImplementedException();
        }
    }
}