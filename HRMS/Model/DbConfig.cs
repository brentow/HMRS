namespace HRMS.Model
{
    /// <summary>
    /// Central place for connection-related settings so ViewModels share the same string.
    /// </summary>
    public static class DbConfig
    {
        // Update the password/user as needed for your local MySQL instance.
        public const string ConnectionString =
            "Server=127.0.0.1;Port=3306;Database=hrms_db;Uid=root;Pwd=;SslMode=Disabled;AllowPublicKeyRetrieval=True;";
    }
}
