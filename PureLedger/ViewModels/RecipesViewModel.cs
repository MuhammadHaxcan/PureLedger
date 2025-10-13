using System.Collections.Generic;

namespace PureLedger.ViewModels
{
    public class RecipesViewModel
    {
        public int ProjectId { get; set; }
        public List<RecipeListItemViewModel> Recipes { get; set; } = new List<RecipeListItemViewModel>();
    }

    public class RecipeListItemViewModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
    }

    public class RecipeItemViewModel
    {
        public int RecipeItemId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }
}