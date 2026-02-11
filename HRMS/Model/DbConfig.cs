namespace HRMS.Model
{
    /// <summary>
    /// Central place for connection-related settings so ViewModels share the same string.
    /// </summary>
    public static class DbConfig
    {
        public const string ConnectionString =
            "Server=127.0.0.1;Port=3306;Database=Human_Resources_Management_System;Uid=root;Pwd=15248130;SslMode=Disabled;AllowPublicKeyRetrieval=True;";
    }
}
