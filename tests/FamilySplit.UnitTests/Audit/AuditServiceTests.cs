using FamilySplit.Application.Audit;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Audit;

public class AuditServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<AuditService>> _loggerMock = new();
    private readonly AuditService _sut;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new AuditService(_db, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Assert
        _sut.Should().NotBeNull();
    }

    [Fact]
    public void Queue_WithMetadata_AddsAuditLogToDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var metadata = new { amount = 42.5m, title = "Test" };

        // Act
        _sut.Queue(userId, "Expense", entityId, "Created", metadata);

        // Assert
        var entry = _db.ChangeTracker.Entries<AuditLog>().Single().Entity;
        entry.UserId.Should().Be(userId);
        entry.EntityType.Should().Be("Expense");
        entry.EntityId.Should().Be(entityId);
        entry.Action.Should().Be("Created");
        entry.Metadata.Should().Contain("42.5");
        entry.Metadata.Should().Contain("Test");
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Queue_WithNullMetadata_SetsMetadataToNull()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        _sut.Queue(Guid.NewGuid(), "Settlement", entityId, "Deleted");

        // Assert
        var entry = _db.ChangeTracker.Entries<AuditLog>().Single().Entity;
        entry.Metadata.Should().BeNull();
    }

    [Fact]
    public void Queue_WithNullUserId_SetsUserIdToNull()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        _sut.Queue(null, "Expense", entityId, "Created");

        // Assert
        var entry = _db.ChangeTracker.Entries<AuditLog>().Single().Entity;
        entry.UserId.Should().BeNull();
    }

    [Fact]
    public void Queue_MultipleCalls_AddsMultipleEntries()
    {
        // Act
        _sut.Queue(Guid.NewGuid(), "Expense", Guid.NewGuid(), "Created");
        _sut.Queue(Guid.NewGuid(), "Settlement", Guid.NewGuid(), "Deleted");

        // Assert
        _db.ChangeTracker.Entries<AuditLog>().Count().Should().Be(2);
    }
}
