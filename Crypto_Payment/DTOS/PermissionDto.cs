using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class PermissionDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Permission name is required.")]
    public string Name { get; set; }

    public int? TopPermissionId { get; set; }

    public string? TopPermissionName { get; set; }
}