using System;
using System.IO;

using Serilog.Formatting.Elasticsearch;
using Serilog.Formatting.Json;

namespace PluralKit.Core
{
    public class ScalarFormatting
    {
        private static bool Write(object value, TextWriter output)
        {
            if (value is SystemId si)
                output.Write(si.Value);
            else if (value is MemberId mi)
                output.Write(mi.Value);
            else if (value is GroupId gi)
                output.Write(gi.Value);
            else if (value is SwitchId swi)
                output.Write(swi.Value);
            else
                return false;
            return true;
        }

        private static void WriteV(object value, TextWriter output) => Write(value, output);

        public class Elasticsearch: ElasticsearchJsonFormatter
        {
            public Elasticsearch(bool omitEnclosingObject = false, string closingDelimiter = null,
                                 bool renderMessage = true, IFormatProvider formatProvider = null,
                                 ISerializer serializer = null, bool inlineFields = false,
                                 bool renderMessageTemplate = true, bool formatStackTraceAsArray = false): base(
                omitEnclosingObject, closingDelimiter, renderMessage, formatProvider, serializer, inlineFields,
                renderMessageTemplate, formatStackTraceAsArray)
            {
                AddLiteralWriter(typeof(SystemId), WriteV);
                AddLiteralWriter(typeof(MemberId), WriteV);
                AddLiteralWriter(typeof(GroupId), WriteV);
                AddLiteralWriter(typeof(SwitchId), WriteV);
            }
        }

        public class JsonValue: JsonValueFormatter
        {
            protected override void FormatLiteralValue(object value, TextWriter output)
            {
                if (Write(value, output))
                    return;
                base.FormatLiteralValue(value, output);
            }
        }
    }
}