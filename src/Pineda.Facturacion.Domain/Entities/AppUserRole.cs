namespace Pineda.Facturacion.Domain.Entities;

public class AppUserRole
{
    public long UserId { get; set; }

    public long RoleId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public AppUser? User { get; set; }

    public AppRole? Role { get; set; }
}
