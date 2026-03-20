namespace Pineda.Facturacion.Application.Security;

public static class AppRoleNames
{
    public const string Admin = "Admin";
    public const string FiscalSupervisor = "FiscalSupervisor";
    public const string FiscalOperator = "FiscalOperator";
    public const string Auditor = "Auditor";

    public static readonly string[] All =
    [
        Admin,
        FiscalSupervisor,
        FiscalOperator,
        Auditor
    ];
}
