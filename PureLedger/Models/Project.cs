namespace PureLedger.Models
{
    public class Project
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }

        public ICollection<Category> Categories { get; set; }
    }
}
