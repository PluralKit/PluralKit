using System;
using System.Diagnostics.Tracing;
using System.Linq;

namespace PluralKit.Bot {
    class PKPerformanceEventListener: EventListener
    {
        public PKPerformanceEventListener()
        {
            foreach (var s in EventSource.GetSources()) 
                EnableEvents(s, EventLevel.Informational);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            base.OnEventWritten(eventData);
            Console.WriteLine($"{eventData.EventSource.Name}/{eventData.EventName}: {string.Join(", ", eventData.PayloadNames.Zip(eventData.Payload).Select(v => $"{v.First}={v.Second}" ))}");
        }
    }
}