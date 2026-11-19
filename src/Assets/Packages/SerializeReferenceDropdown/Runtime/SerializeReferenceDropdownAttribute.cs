using System;
using UnityEngine;

public class SerializeReferenceDropdownAttribute : PropertyAttribute
{
}

[AttributeUsage( AttributeTargets.Class, Inherited = false )]
public sealed class SerializeReferenceDropdownExcludeAttribute : Attribute
{
}