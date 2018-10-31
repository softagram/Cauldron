using Cauldron.Interception.Cecilator;
using System.Xml.Linq;

namespace Cauldron.Interception.Fody
{
    internal class Configuration
    {
        private readonly XElement config;

        public Configuration(XElement config) => this.config = config;

        public bool ReferenceCopyLocal => this.config.Attribute("ReferenceCopyLocal").With(y => y == null ? true : bool.Parse(y.Value));
        public bool ReferenceRecursive => this.config.Attribute("ReferenceRecursive").With(y => y == null ? true : bool.Parse(y.Value));
        public bool Verbose => this.config.Attribute("Verbose").With(y => y == null ? true : bool.Parse(y.Value));
    }
}