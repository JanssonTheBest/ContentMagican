using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Models
{
    public class TasksViewModel
    {
        public TasksViewModel()
        {
            Tasks = GenerateTestData();
        }
        
        public List<Task> Tasks { get; set; }

        public List<Task> GenerateTestData()
        {
            return new List<Task>
        {
            new Task { Name = "Task 1",Type = "Reddit Story Video Automation", Status = "Active", Created = DateTime.Now.AddDays(-5),Id = 33 },
            new Task { Name = "Task 2", Type = "Youtube automation", Status = "Active", Created = DateTime.Now.AddDays(-3), Id = 22 },
        };

        }
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
