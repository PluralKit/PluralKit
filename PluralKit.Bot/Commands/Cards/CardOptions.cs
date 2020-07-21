using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot
{
    public class CardOptions
    {

        public PrivacyLevel? PrivacyFilter { get; set; } = PrivacyLevel.Public;

        public string createFooter() {
            var str = new StringBuilder();
            str.Append(PrivacyFilter switch
            {
                null => "including private feilds",
                PrivacyLevel.Public => "", // (default, no extra line needed)
                _ => new ArgumentOutOfRangeException($"Couldn't find readable string for privacy filter {PrivacyFilter}")
            });
            return(str.ToString());
        }
    }
}