using Microsoft.FSharp.Collections;
using Rulesage.Common.Types.Domain;

namespace Rulesage.Cli.Utils;

public static class ConsoleFormats
{
    private static void PrintParamType(ParamType pt)
    {
        switch (pt)
        {
            case ParamType.Node node:
                ConsoleHelper.WriteColored(ConsoleColor.Cyan, "▶ Node");
                Console.Write($"  ir='{node.nodeType.ir}'  (☰ see 'node show {node.nodeType.ir}')");
                break;
            default:
                ConsoleHelper.WriteColored(ConsoleColor.DarkGray, "● Leaf");
                break;
        }
    }

    public static void PrintParamsMap(FSharpMap<string, ParamType> map)
    {
        foreach (var kvp in map)
        {
            Console.Write($"    {kvp.Key,-5} : ");
            PrintParamType(kvp.Value);
            Console.WriteLine();
        }
    }
}