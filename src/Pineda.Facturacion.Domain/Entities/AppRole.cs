namespace Pineda.Facturacion.Domain.Entities;

public class AppRole
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<AppUserRole> UserRoles { get; set; } = [];
}
