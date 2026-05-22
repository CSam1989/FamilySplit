namespace FamilySplit.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string IconName { get; set; } = default!;
    public bool IsSystem { get; set; }
    /// <summary>Null = system-wide. Set = group-scoped custom category.</summary>
    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }
}
