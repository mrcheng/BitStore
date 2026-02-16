using System.ComponentModel.DataAnnotations;

namespace BitStoreWeb.Net9.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username (your email)")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
