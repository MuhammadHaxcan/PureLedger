using System.Collections.Generic;

namespace PureLedger.ViewModels
{
    public class AgingReportPageViewModel
    {
        public List<ProjectViewModel> Projects { get; set; } = new();
        public List<CategoryViewModel> Categories { get; set; } = new();
    }

    public class ProjectViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class CategoryViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}