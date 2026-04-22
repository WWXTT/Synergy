using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class RequiredAttribute : Attribute
{
}
