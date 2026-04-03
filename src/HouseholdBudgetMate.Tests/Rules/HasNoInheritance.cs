using Mono.Cecil;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Rules;

public class HasNoInheritance : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
    {
        return type.BaseType.Name == "System.Object";
    }
}