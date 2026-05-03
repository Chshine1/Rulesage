namespace Rulesage.Cli.Utils;

public static class ConsoleHelper
{
    private static void WithColor(ConsoleColor color, Action action)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        action();
        Console.ForegroundColor = prev;
    }

    public static void WriteColored(ConsoleColor color, string text) =>
        WithColor(color, () => Console.Write(text));

    public static void WriteLineColored(ConsoleColor color, string text) =>
        WithColor(color, () => Console.WriteLine(text));
}