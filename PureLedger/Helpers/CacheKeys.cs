namespace PureLedger.Helpers
{
    /// <summary>
    /// Cache key constants and helper methods for consistent cache key management
    /// </summary>
    public static class CacheKeys
    {
        // Cache key prefixes
        private const string CategoriesByProjectPrefix = "Categories_Project_";
        private const string AccountsByCategoryPrefix = "Accounts_Category_";
        private const string AllCategoriesKey = "Categories_All";
        private const string AllAccountsKey = "Accounts_All";
        public static string CategoriesByProject(int projectId) => $"{CategoriesByProjectPrefix}{projectId}";
        public static string AccountsByCategory(int categoryId) => $"{AccountsByCategoryPrefix}{categoryId}";
        public static string AllCategories => AllCategoriesKey;
        public static string AllAccounts => AllAccountsKey;
        public static string CategoriesByProjectPattern => CategoriesByProjectPrefix;
        public static string AccountsByCategoryPattern => AccountsByCategoryPrefix;
    }
}

