using System.ComponentModel.DataAnnotations;

namespace Eventiq.EventService.Domain.Entity;

public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    protected void UpdateAction()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    protected bool DeleteAction()
    {
        if (!IsDeleted)
        {
            DeletedAt = DateTime.UtcNow;
            IsDeleted = true;
        }
        return IsDeleted;
    }
}
