using System;
using System.Linq;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    public static class JsonModelExt
    {
        public static JObject ToJson(this PKSystem system, LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", system.Hid);
            o.Add("name", system.Name);
            o.Add("description", system.DescriptionPrivacy.CanAccess(ctx) ? system.Description : null);
            o.Add("tag", system.Tag);
            o.Add("avatar_url", system.AvatarUrl);
            o.Add("created", DateTimeFormats.TimestampExportFormat.Format(system.Created));
            o.Add("tz", system.UiTz);
            return o;
        }

        public static void ApplyJson(this PKSystem system, JObject o)
        {
            if (o.ContainsKey("name")) system.Name = o.Value<string>("name").NullIfEmpty().BoundsCheckField(Limits.MaxSystemNameLength, "System name");
            if (o.ContainsKey("description")) system.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "System description");
            if (o.ContainsKey("tag")) system.Tag = o.Value<string>("tag").NullIfEmpty().BoundsCheckField(Limits.MaxSystemTagLength, "System tag");
            if (o.ContainsKey("avatar_url")) system.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
            if (o.ContainsKey("tz")) system.UiTz = o.Value<string>("tz") ?? "UTC";
        }
        
        public static JObject ToJson(this PKMember member, LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", member.Hid);
            o.Add("name", member.Name);
            o.Add("color", member.MemberPrivacy.CanAccess(ctx) ? member.Color : null);
            o.Add("display_name", member.DisplayName);
            o.Add("birthday", member.MemberPrivacy.CanAccess(ctx) && member.Birthday.HasValue ? DateTimeFormats.DateExportFormat.Format(member.Birthday.Value) : null);
            o.Add("pronouns", member.MemberPrivacy.CanAccess(ctx) ? member.Pronouns : null);
            o.Add("avatar_url", member.AvatarUrl);
            o.Add("description", member.MemberPrivacy.CanAccess(ctx) ? member.Description : null);
            
            var tagArray = new JArray();
            foreach (var tag in member.ProxyTags) 
                tagArray.Add(new JObject {{"prefix", tag.Prefix}, {"suffix", tag.Suffix}});
            o.Add("proxy_tags", tagArray);

            o.Add("keep_proxy", member.KeepProxy);
            o.Add("created", DateTimeFormats.TimestampExportFormat.Format(member.Created));

            if (member.ProxyTags.Count > 0)
            {
                // Legacy compatibility only, TODO: remove at some point
                o.Add("prefix", member.ProxyTags?.FirstOrDefault().Prefix);
                o.Add("suffix", member.ProxyTags?.FirstOrDefault().Suffix);
            }

            return o;
        }

        public static void ApplyJson(this PKMember member, JObject o)
        {
            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null) 
                throw new JsonModelParseError("Member name can not be set to null.");
            
            if (o.ContainsKey("name")) member.Name = o.Value<string>("name").BoundsCheckField(Limits.MaxMemberNameLength, "Member name");
            if (o.ContainsKey("color")) member.Color = o.Value<string>("color").NullIfEmpty();
            if (o.ContainsKey("display_name")) member.DisplayName = o.Value<string>("display_name").NullIfEmpty().BoundsCheckField(Limits.MaxMemberNameLength, "Member display name");
            if (o.ContainsKey("birthday"))
            {
                var str = o.Value<string>("birthday").NullIfEmpty();
                var res = DateTimeFormats.DateExportFormat.Parse(str);
                if (res.Success) member.Birthday = res.Value;
                else if (str == null) member.Birthday = null;
                else throw new JsonModelParseError("Could not parse member birthday.");
            }

            if (o.ContainsKey("pronouns")) member.Pronouns = o.Value<string>("pronouns").NullIfEmpty().BoundsCheckField(Limits.MaxPronounsLength, "Member pronouns");
            if (o.ContainsKey("description")) member.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "Member descriptoin");
            if (o.ContainsKey("keep_proxy")) member.KeepProxy = o.Value<bool>("keep_proxy");

            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                member.ProxyTags = new[] {new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix"))};
            else if (o.ContainsKey("proxy_tags"))
            {
                member.ProxyTags = o.Value<JArray>("proxy_tags")
                    .OfType<JObject>().Select(o => new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                    .ToList();
            }
        }

        private static string BoundsCheckField(this string input, int maxLength, string nameInError)
        {
            if (input != null && input.Length > maxLength)
                throw new JsonModelParseError($"{nameInError} too long ({input.Length} > {maxLength}).");
            return input;
        }
    }
    
    public class JsonModelParseError: Exception
    {
        public JsonModelParseError(string message): base(message) { }
    }
}