using uniffi.commands;

namespace PluralKit.Bot;

public class ParametersFFI
{
    private string _cb { get; init; }
    private List<string> _args { get; init; }
    public int _ptr = -1;
    private Dictionary<string, string?> _flags { get; init; }

    // just used for errors, temporarily
    public string FullCommand { get; init; }

    public ParametersFFI(string cmd)
    {
        FullCommand = cmd;
        var result = CommandsMethods.ParseCommand(cmd);
        if (result is CommandResult.Ok)
        {
            var command = ((CommandResult.Ok)result).@command;
            _cb = command.@commandRef;
            _args = command.@args;
            _flags = command.@flags;
        }
        else
        {
            throw new PKError(((CommandResult.Err)result).@error);
        }
    }

    public string Pop()
    {
        if (_args.Count > _ptr + 1) Console.WriteLine($"pop: {_ptr + 1}, {_args[_ptr + 1]}");
        else Console.WriteLine("pop: no more arguments");
        if (_args.Count() == _ptr + 1) return "";
        _ptr++;
        return _args[_ptr];
    }

    public string Peek()
    {
        if (_args.Count > _ptr + 1) Console.WriteLine($"peek: {_ptr + 1}, {_args[_ptr + 1]}");
        else Console.WriteLine("peek: no more arguments");
        if (_args.Count() == _ptr + 1) return "";
        return _args[_ptr + 1];
    }

    // this might not work quite right
    public string PeekWithPtr(ref int ptr)
    {
        return _args[ptr];
    }

    public ISet<string> Flags()
    {
        return new HashSet<string>(_flags.Keys);
    }

    // parsed differently in new commands, does this work right?
    // note: skipFlags here does nothing
    public string Remainder(bool skipFlags = false)
    {
        return Pop();
    }
}