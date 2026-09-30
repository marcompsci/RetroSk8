// Minimal subset of NUnit's API used by the Retro Sk8 EditMode tests. Only compiled by DotnetTests.
using System;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public object[] Arguments { get; }
        public TestCaseAttribute(params object[] args) { Arguments = args; }
    }

    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        static void Fail(string m) => throw new AssertionException(m);
        public static void IsTrue(bool c, string m = null) { if (!c) Fail(m ?? "Expected true"); }
        public static void IsFalse(bool c, string m = null) { if (c) Fail(m ?? "Expected false"); }
        public static void AreEqual(float e, float a, float tol) { if (Math.Abs(e - a) > tol) Fail($"Expected {e} ± {tol} but was {a}"); }
        public static void AreEqual(long e, long a) { if (e != a) Fail($"Expected {e} but was {a}"); }
        public static void AreEqual(object e, object a)
        {
            if (e is IConvertible && a is IConvertible && !(e is string) && !(e is Enum) && !(a is Enum))
            { if (Convert.ToDouble(e) != Convert.ToDouble(a)) Fail($"Expected {e} but was {a}"); return; }
            if (!Equals(e, a)) Fail($"Expected {e} but was {a}");
        }
        public static void Greater(float a, float b) { if (!(a > b)) Fail($"Expected {a} > {b}"); }
        public static void AreNotEqual(object e, object a) { if (Equals(e, a)) Fail($"Expected not {e}"); }
    }
}
