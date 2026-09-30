using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class Runner
{
    public static int Main()
    {
        int pass = 0, fail = 0;
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "RetroSk8.Tests");
        foreach (var type in types.OrderBy(t => t.Name))
        foreach (var m in type.GetMethods().OrderBy(m => m.Name))
        {
            var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
            if (m.GetCustomAttribute<TestAttribute>() != null) cases.Add(Array.Empty<object>());
            foreach (var args in cases)
            {
                string name = $"{type.Name}.{m.Name}({string.Join(", ", args)})";
                try
                {
                    var p = m.GetParameters();
                    var conv = args.Select((a, i) => Convert.ChangeType(a, p[i].ParameterType)).ToArray();
                    m.Invoke(Activator.CreateInstance(type), conv);
                    pass++;
                    Console.WriteLine("  PASS " + name);
                }
                catch (TargetInvocationException e)
                {
                    fail++;
                    Console.WriteLine("  FAIL " + name + " :: " + e.InnerException?.Message);
                }
            }
        }
        Console.WriteLine($"\n{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
