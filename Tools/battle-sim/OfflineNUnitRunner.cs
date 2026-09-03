using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Minimal reflection-based NUnit runner so the EditMode tests can be executed with the Unity
/// Editor closed. Supports [TestFixture], [Test], [TestCase], [SetUp] and [TearDown].
/// </summary>
public static class OfflineNUnitRunner
{
    public static int Main(string[] args)
    {
        Assembly assembly = typeof(OfflineNUnitRunner).Assembly;
        int passed = 0;
        int failed = 0;
        List<string> failures = new List<string>();

        Type[] types = assembly.GetTypes();
        Array.Sort(types, (a, b) => string.CompareOrdinal(a.FullName, b.FullName));

        foreach (Type type in types)
        {
            if (!HasAttribute(type, "TestFixtureAttribute") || type.IsAbstract)
            {
                continue;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Array.Sort(methods, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            MethodInfo setUp = FindMethodWith(methods, "SetUpAttribute");
            MethodInfo tearDown = FindMethodWith(methods, "TearDownAttribute");

            foreach (MethodInfo method in methods)
            {
                List<object[]> invocations = new List<object[]>();

                if (HasAttribute(method, "TestAttribute"))
                {
                    invocations.Add(new object[0]);
                }

                foreach (Attribute attribute in method.GetCustomAttributes(false))
                {
                    if (attribute.GetType().Name != "TestCaseAttribute")
                    {
                        continue;
                    }

                    PropertyInfo argumentsProperty = attribute.GetType().GetProperty("Arguments");
                    object[] arguments = argumentsProperty != null
                        ? (object[])argumentsProperty.GetValue(attribute, null)
                        : new object[0];
                    invocations.Add(arguments);
                }

                if (invocations.Count == 0)
                {
                    continue;
                }

                foreach (object[] arguments in invocations)
                {
                    string label = type.FullName + "." + method.Name + Describe(arguments);
                    try
                    {
                        object instance = Activator.CreateInstance(type);
                        if (setUp != null)
                        {
                            setUp.Invoke(instance, null);
                        }

                        try
                        {
                            method.Invoke(instance, Coerce(method, arguments));
                        }
                        finally
                        {
                            if (tearDown != null)
                            {
                                tearDown.Invoke(instance, null);
                            }
                        }

                        passed++;
                    }
                    catch (TargetInvocationException error)
                    {
                        failed++;
                        failures.Add(label + "\n    " + Flatten(error.InnerException));
                    }
                    catch (Exception error)
                    {
                        failed++;
                        failures.Add(label + "\n    " + Flatten(error));
                    }
                }
            }
        }

        foreach (string failure in failures)
        {
            Console.WriteLine("FAIL " + failure);
        }

        Console.WriteLine("=====================================");
        Console.WriteLine("PASSED: " + passed + "   FAILED: " + failed + "   TOTAL: " + (passed + failed));
        return failed == 0 ? 0 : 1;
    }

    private static object[] Coerce(MethodInfo method, object[] arguments)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return null;
        }

        object[] result = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            object value = i < arguments.Length ? arguments[i] : null;
            if (value != null && !parameters[i].ParameterType.IsInstanceOfType(value))
            {
                Type target = parameters[i].ParameterType;
                if (target.IsEnum)
                {
                    value = Enum.ToObject(target, value);
                }
                else
                {
                    value = Convert.ChangeType(value, target);
                }
            }

            result[i] = value;
        }

        return result;
    }

    private static string Describe(object[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
        {
            return string.Empty;
        }

        string text = "(";
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                text += ", ";
            }

            text += arguments[i] == null ? "null" : arguments[i].ToString();
        }

        return text + ")";
    }

    private static string Flatten(Exception error)
    {
        if (error == null)
        {
            return "(no exception detail)";
        }

        return error.GetType().Name + ": " + error.Message.Replace("\n", "\n    ");
    }

    private static bool HasAttribute(MemberInfo member, string attributeName)
    {
        foreach (Attribute attribute in member.GetCustomAttributes(true))
        {
            if (attribute.GetType().Name == attributeName)
            {
                return true;
            }
        }

        return false;
    }

    private static MethodInfo FindMethodWith(MethodInfo[] methods, string attributeName)
    {
        foreach (MethodInfo method in methods)
        {
            if (HasAttribute(method, attributeName))
            {
                return method;
            }
        }

        return null;
    }
}
