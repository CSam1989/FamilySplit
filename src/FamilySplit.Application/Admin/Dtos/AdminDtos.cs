namespace FamilySplit.Application.Admin.Dtos;

public record CreateFamilyRequest(string Name);

public record AdminAddFamilyToGroupRequest(Guid FamilyId);
