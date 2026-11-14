namespace WorkshopHub.Web.Components.Pages;

public partial class AdminCourses
{
    private sealed class CourseForm : System.ComponentModel.DataAnnotations.IValidatableObject
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(80)] public string Title { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(300)] public string ShortDescription { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal Price { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int MaximumParticipants { get; set; } = 12;
        public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(System.ComponentModel.DataAnnotations.ValidationContext validationContext)
        {
            if (decimal.Round(Price, 2) != Price) { yield return new("Use at most two decimal places.", [nameof(Price)]); }
        }
    }
}
