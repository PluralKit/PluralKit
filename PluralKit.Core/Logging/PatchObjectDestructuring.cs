using System.Collections.Generic;

using Serilog.Core;
using Serilog.Events;

namespace PluralKit.Core
{
    public class PatchObjectDestructuring: IDestructuringPolicy
    {
        public bool TryDestructure(object value, ILogEventPropertyValueFactory factory,
                                   out LogEventPropertyValue result)
        {
            result = null;
            if (!(value is PatchObject po)) return false;
            
            var propList = new List<LogEventProperty>();
            foreach (var props in po.GetType().GetProperties())
            {
                var propValue = props.GetValue(po);
                if (propValue is IPartial p && p.IsPresent)
                    propList.Add(new LogEventProperty(props.Name, factory.CreatePropertyValue(p.RawValue, true)));
                else if (!(propValue is IPartial))
                    propList.Add(new LogEventProperty(props.Name, factory.CreatePropertyValue(propValue, true)));
            }

            result = new StructureValue(propList);
            return true;
        }
    }
}