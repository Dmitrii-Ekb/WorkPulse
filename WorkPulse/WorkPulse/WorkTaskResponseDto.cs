namespace WorkPulse;

public class WorkTaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }

    public static WorkTaskResponseDto FromEntity(WorkTask task)
    {
        return new WorkTaskResponseDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            IsCompleted = task.IsCompleted
        };
    }
}