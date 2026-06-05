namespace Pineda.Facturacion.Application.Security;

public static class AuthorizationPolicyNames
{
    public const string Authenticated = "Authenticated";
    public const string AdminOnly = "AdminOnly";
    public const string SupervisorOrAdmin = "SupervisorOrAdmin";
    public const string OperatorOrAbove = "OperatorOrAbove";
    public const string AuditRead = "AuditRead";
    public const string PosCreditRead = "PosCreditRead";
    public const string PosCreditReadPermission = "pos.credit.read";
}
