using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant
{
    public class ParticipantOptions
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        internal Func<Type, bool>? TypeFilter { get; set; }
    }
}
