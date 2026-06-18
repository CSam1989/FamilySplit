using FamilySplit.Features.Activities.Shared;

namespace FamilySplit.UnitTests.Features.Activities.Shared;

/// <summary>
/// Pure tests for <see cref="ParticipantSeeder"/> — the id→entity mapping only. The DB reads that
/// decide <em>which</em> members participate (active group members, parent's participants) live in
/// <c>ActivityData</c> and are Testcontainers-tested in <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public class ParticipantSeederTests
{
    [Fact]
    public void SeedForActivity_NoMembers_ReturnsEmpty()
    {
        var result = ParticipantSeeder.SeedForActivity(Guid.NewGuid(), []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SeedForActivity_BuildsOneParticipantPerMember_WithActivityId()
    {
        var activityId = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        var result = ParticipantSeeder.SeedForActivity(activityId, [m1, m2]);

        result.Should().HaveCount(2);
        result.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([m1, m2]);
        result.Should().AllSatisfy(p =>
        {
            p.ActivityId.Should().Be(activityId);
            p.Id.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void SeedForActivity_GeneratesUniqueParticipantIds()
    {
        var result = ParticipantSeeder.SeedForActivity(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

        result.Select(p => p.Id).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void SeedForSubActivity_NoParentMembers_ReturnsEmpty()
    {
        var result = ParticipantSeeder.SeedForSubActivity(Guid.NewGuid(), []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SeedForSubActivity_CopiesParentMembers_WithSubActivityId()
    {
        var subId = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        var result = ParticipantSeeder.SeedForSubActivity(subId, [m1, m2]);

        result.Should().HaveCount(2);
        result.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([m1, m2]);
        result.Should().AllSatisfy(p => p.ActivityId.Should().Be(subId));
    }
}
