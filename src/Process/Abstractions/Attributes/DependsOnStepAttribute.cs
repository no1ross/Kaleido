using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DependsOnStepAttribute : Attribute
    {
        public DependsOnStepAttribute(Type dependsOnStep) 
        {
            DependsOnStep = dependsOnStep;
        }

        public Type DependsOnStep { get; }
    }
}
