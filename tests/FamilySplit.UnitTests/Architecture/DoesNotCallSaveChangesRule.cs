using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;

namespace FamilySplit.UnitTests.Architecture;

/// <summary>
/// NetArchTest custom rule that fails any type whose compiled method bodies
/// contain a call instruction targeting a method whose name begins with
/// "SaveChanges" (i.e. <c>DbContext.SaveChanges</c> or <c>SaveChangesAsync</c>).
/// Applied to *QueryHandler types to enforce that the read side never mutates
/// the database.
/// </summary>
internal sealed class DoesNotCallSaveChangesRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Call &&
                    instruction.OpCode != OpCodes.Callvirt)
                    continue;

                if (instruction.Operand is MethodReference mr &&
                    mr.Name.StartsWith("SaveChanges", StringComparison.Ordinal))
                    return false;
            }
        }
        return true;
    }
}
