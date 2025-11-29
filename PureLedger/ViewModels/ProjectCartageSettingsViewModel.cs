namespace PureLedger.ViewModels
{
    public class ProjectCartageSettingsViewModel
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int? ForExpenseAccountId { get; set; }
        public int? CartageTransporterCategoryId { get; set; }
        public string? ProjectImageBase64 { get; set; }
        public string? ProjectImageContentType { get; set; }
    }
}


