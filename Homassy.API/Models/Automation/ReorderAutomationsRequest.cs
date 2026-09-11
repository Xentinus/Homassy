using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Automation
{
    /// <summary>
    /// The manual order of the caller's automation rules, as the ids in the order they should appear.
    /// </summary>
    public class ReorderAutomationsRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one automation is required")]
        public List<Guid> AutomationPublicIds { get; set; } = [];
    }
}
