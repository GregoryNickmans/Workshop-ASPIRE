namespace WorkshopHub.Web.Components.Pages;

public partial class BookCourse
{
    private sealed class ParticipantForm
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter the participant's name.")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter an email address.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [System.ComponentModel.DataAnnotations.StringLength(254)]
        public string Email { get; set; } = "";
    }
}
