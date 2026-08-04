namespace minutesheet.Data
{
    // Central definition of the application's role names so pages, seeding and
    // [Authorize] attributes all reference the same strings.
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Employee = "Employee";

        public static readonly string[] All = { Admin, Employee };
    }
}
