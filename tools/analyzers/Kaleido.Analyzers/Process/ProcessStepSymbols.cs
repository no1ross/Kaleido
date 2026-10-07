using Microsoft.CodeAnalysis;

namespace Kaleido.Analyzers.Process;

internal static class ProcessStepSymbols
{
    public const string StepInterfaceFullName = "Kaleido.Processor.IProcessStep";

    public const string StepAttributeFullName = "Kaleido.Processor.ProcessStepAttribute";

    public static bool ImplementsProcessStep(
        INamedTypeSymbol type,
        string stepInterfaceFullName = StepInterfaceFullName)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == stepInterfaceFullName)
            {
                return true;
            }
        }

        return false;
    }

    public static AttributeData? FindProcessStepAttribute(
        INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == StepAttributeFullName)
            {
                return attribute;
            }
        }

        return null;
    }
}
