using System.ComponentModel.DataAnnotations;

namespace Capableza.Web.ViewModels
{
    public class AddEmployeeViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Employee Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = "password123";
    }
}
