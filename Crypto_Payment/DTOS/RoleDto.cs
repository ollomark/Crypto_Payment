using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class RoleDto
{
    public string? Id { get; set; }
    
    [Required(ErrorMessage = "Rol adı boş geçilemez")]
    public string Name { get; set; } = "";
    public string? NormalizedName { get; set; }
}