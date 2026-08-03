using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ProcessStepAttribute : Attribute
    {
        public ProcessStepAttribute(string name, string description, string version)
        {
            Name = name;
            Description = description;
            Version = version;
        }
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }
    }
}
