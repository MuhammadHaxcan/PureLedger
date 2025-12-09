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
        private const string AccountsByProjectPrefix = "Accounts_Project_";
        private const string ProjectsForUserPrefix = "Projects_User_";

        // Static data keys
        private const string AllCategoriesKey = "Categories_All";
        private const string AllAccountsKey = "Accounts_All";
        private const string AllLocationsKey = "Locations_All";
        private const string AllAccountTypesKey = "AccountTypes_All";
        private const string AllAccountGroupsKey = "AccountGroups_All";
        private const string AllBookersKey = "Bookers_All";
        private const string AllProjectsKey = "Projects_All";

        // Dynamic key generators
        public static string CategoriesByProject(int projectId) => $"{CategoriesByProjectPrefix}{projectId}";
        public static string AccountsByCategory(int categoryId) => $"{AccountsByCategoryPrefix}{categoryId}";
        public static string AccountsByProject(int projectId) => $"{AccountsByProjectPrefix}{projectId}";
        public static string ProjectsForUser(int userId) => $"{ProjectsForUserPrefix}{userId}";

        // Static key accessors
        public static string AllCategories => AllCategoriesKey;
        public static string AllAccounts => AllAccountsKey;
        public static string AllLocations => AllLocationsKey;
        public static string AllAccountTypes => AllAccountTypesKey;
        public static string AllAccountGroups => AllAccountGroupsKey;
        public static string AllBookers => AllBookersKey;
        public static string AllProjects => AllProjectsKey;

        // Pattern accessors for cache invalidation
        public static string CategoriesByProjectPattern => CategoriesByProjectPrefix;
        public static string AccountsByCategoryPattern => AccountsByCategoryPrefix;
        public static string AccountsByProjectPattern => AccountsByProjectPrefix;
        public static string ProjectsForUserPattern => ProjectsForUserPrefix;
    }
}

