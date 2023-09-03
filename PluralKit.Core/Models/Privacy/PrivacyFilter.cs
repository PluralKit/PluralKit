namespace PluralKit.Core;

[Flags]
public enum PrivacyFilter
{
    Public = 1 << 0,
    Private = 1 << 1,
    Trusted = 1 << 2,
}