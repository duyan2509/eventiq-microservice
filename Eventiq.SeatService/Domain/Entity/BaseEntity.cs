using System.ComponentModel.DataAnnotations;

namespace Eventiq.SeatService.Domain.Entity;

public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    public bool MarkDeleted()
    {
        if (!IsDeleted)
        {
            DeletedAt = DateTime.UtcNow;
            IsDeleted = true;
        }
        return IsDeleted;
    }
}
