using ContentMagican.Database;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Models
{
    public class TasksViewModel
    {
        public List<_Task> Tasks { get; set; }
    }

    public class Task
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public DateTime Created { get; set; }
        public long Id { get; set; }
    }
}
