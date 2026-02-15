namespace Eventiq.EventService.Domain.Entity;

public class Event : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string? OranizationAvatar { get; set; }
    public string? EventBanner { get; set; }

    public string Name { get; set; }
    public string? Description { get; set; }
    public string? DetailAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? CommuneCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CommuneName { get; set; }
    public EventStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ICollection<Legend> Legends { get; set; }=new List<Legend>();
    public ICollection<Session> Sessions { get; set; }=new List<Session>();
    public ICollection<Chart> Charts { get; set; }=new List<Chart>();
    public ICollection<Submission> Submissions { get; set; }=new List<Submission>();
    public void AddSession(Session session)
    {
        ValidateSessionTime(session);
        Sessions.Add(session);
    }

    private void ValidateSessionTime(Session session)
    {
        session.ValidateSessionTime(session);

        if (session.StartTime < StartTime || session.EndTime > EndTime)
            throw new BusinessException("Session must be within Event time range.");

        if (IsOverlapping(session))
            throw new BusinessException("Session time overlaps with another session.");
    }
    private bool IsOverlapping(Session session)
    {
        return Sessions.Any(s =>
            s.Id != session.Id &&
            session.StartTime < s.EndTime &&
            session.EndTime > s.StartTime);
    }

}

public enum EventStatus
{
    Draft,
    Pending,
    Approved,
    Rejected,
    Published 
}





