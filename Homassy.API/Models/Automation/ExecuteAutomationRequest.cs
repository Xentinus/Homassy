using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Automation
{
    public class ExecuteAutomationRequest
    {
        [StringLength(512)]
        public string? Notes { get; set; }
    }
}
