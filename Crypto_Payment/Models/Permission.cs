namespace Crypto_Payment.Models;

public class Permission
{
    public int Id { get; set; }    
    public string Name { get; set; } = "";    
    public string NormalizedName { get; set; } = "";    
    public int? TopPermissionId { get; set; }
    
    // Navigation property - üst yetki ilişkisi
    public Permission? TopPermission { get; set; }
}
