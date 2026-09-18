namespace PluralKit.Bot;

public static class JointContextChecksExt
{
    public static JointContext CheckNoSystem(this JointContext ctx)
    {
        if (ctx.System != null)
            throw Errors.ExistingSystemError(ctx.DefaultPrefix);
        return ctx;
    }
}