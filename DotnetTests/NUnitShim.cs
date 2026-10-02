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
        public static void AreEqual(float e, float a, float tol, string m) { if (Math.Abs(e - a) > tol) Fail(m + $" (expected {e} ± {tol} but was {a})"); }
        public static void AreEqual(object e, object a, string m) { if (!Equals(e, a) && !(e is IConvertible && a is IConvertible && !(e is string) && !(a is string) && Convert.ToDouble(e) == Convert.ToDouble(a))) Fail(m + $" (expected {e} but was {a})"); }
        public static T Throws<T>(Action a) where T : Exception
        {
            try { a(); } catch (T e) { return e; } catch (Exception e) { Fail($"Expected {typeof(T).Name} but got {e.GetType().Name}"); }
            Fail($"Expected {typeof(T).Name}"); return null;
        }
        public static void GreaterOrEqual(float a, float b, string m = null) { if (!(a >= b)) Fail((m ?? "") + $" expected {a} >= {b}"); }
        public static void IsNull(object o, string m = null) { if (o != null) Fail(m ?? "Expected null"); }
        public static void IsNotNull(object o, string m = null) { if (o == null) Fail(m ?? "Expected not null"); }
        public static void AreEqual(long e, long a) { if (e != a) Fail($"Expected {e} but was {a}"); }
        public static void AreEqual(object e, object a)
        {
            if (e is IConvertible && a is IConvertible && !(e is string) && !(e is Enum) && !(a is Enum))
            { if (Convert.ToDouble(e) != Convert.ToDouble(a)) Fail($"Expected {e} but was {a}"); return; }
            if (!Equals(e, a)) Fail($"Expected {e} but was {a}");
        }
        public static void Greater(float a, float b) { if (!(a > b)) Fail($"Expected {a} > {b}"); }
        public static void Greater(float a, float b, string m) { if (!(a > b)) Fail(m + $" (expected {a} > {b})"); }
        public static void Less(float a, float b, string m = null) { if (!(a < b)) Fail((m ?? "") + $" (expected {a} < {b})"); }
        public static void LessOrEqual(float a, float b, string m = null) { if (!(a <= b)) Fail((m ?? "") + $" (expected {a} <= {b})"); }
        public static void AreNotEqual(object e, object a, string m) { if (Equals(e, a)) Fail(m + $" (expected not {e})"); }
        public static void AreNotEqual(object e, object a) { if (Equals(e, a)) Fail($"Expected not {e}"); }
    }
}
