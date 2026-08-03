using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Eventing;

//put this in kaleido abstractions
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent processEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent;
}

public interface IKaleidoEvent
{
}
