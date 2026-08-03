using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;

namespace FamilySplit.UnitTests.Architecture;

/// <summary>
/// NetArchTest custom rule that fails any type whose fields, local variables, or method bodies
/// reference an EF Core <c>DbContext</c> (e.g. <c>AppDbContext</c>, <c>DbContextOptions</c>) or call
/// <c>UseInMemoryDatabase</c>. Applied to <c>*CommandHandlerTests</c> types to enforce ADR-001
/// Rule 11: business-logic (command-handler) tests must mock <c>I{Slice}Data</c> and never spin up a
/// database / InMemory provider.
/// </summary>
internal sealed class DoesNotReferenceDbContextRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
    {
        foreach (var field in type.Fields)
            if (IsDbContextType(field.FieldType))
                return false;

        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            foreach (var variable in method.Body.Variables)
                if (IsDbContextType(variable.VariableType))
                    return false;

            foreach (var instruction in method.Body.Instructions)
            {
                switch (instruction.Operand)
                {
                    case MethodReference mr:
                        if (mr.Name.Contains("UseInMemoryDatabase", StringComparison.Ordinal)) return false;
                        if (IsDbContextType(mr.DeclaringType) || IsDbContextType(mr.ReturnType)) return false;
                        break;
                    case FieldReference fr:
                        if (IsDbContextType(fr.FieldType)) return false;
                        break;
                    case TypeReference tr:
                        if (IsDbContextType(tr)) return false;
                        break;
                }
            }
        }

        return true;
    }

    private static bool IsDbContextType(TypeReference? type) =>
        type is not null && type.Name.Contains("DbContext", StringComparison.Ordinal);
}
