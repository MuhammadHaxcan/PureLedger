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

        // Static data keys
        private const string AllCategoriesKey = "Categories_All";
        private const string AllAccountsKey = "Accounts_All";
        private const string AllLocationsKey = "Locations_All";
        private const string AllBookersKey = "Bookers_All";
        private const string AllProjectsKey = "Projects_All";

        // Dynamic key generators
        public static string CategoriesByProject(int projectId) => $"{CategoriesByProjectPrefix}{projectId}";
        public static string AccountsByCategory(int categoryId) => $"{AccountsByCategoryPrefix}{categoryId}";

        // Static key accessors
        public static string AllCategories => AllCategoriesKey;
        public static string AllAccounts => AllAccountsKey;
        public static string AllLocations => AllLocationsKey;
        public static string AllBookers => AllBookersKey;
        public static string AllProjects => AllProjectsKey;
    }
}

