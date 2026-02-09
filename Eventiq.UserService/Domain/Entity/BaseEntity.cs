using System.ComponentModel.DataAnnotations;

namespace Eventiq.UserService.Domain.Entity;

public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public bool IsDeleted { get; set; } = false;

    protected void updateAction(){
        UpdatedAt = DateTime.UtcNow;

    }
    protected bool deleteAction(){
        if(!IsDeleted){
            DeletedAt = DateTime.UtcNow;
            IsDeleted = true;
        }
        return IsDeleted;
    }
}