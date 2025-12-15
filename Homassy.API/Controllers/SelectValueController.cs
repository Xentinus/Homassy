using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class SelectValueController : ControllerBase
    {
        [HttpGet("{type}")]
        [MapToApiVersion(1.0)]
        public IActionResult GetSelectValues(SelectValueType type)
        {
            var selectValues = new SelectValueFunctions().GetSelectValues(type);
            return Ok(ApiResponse<List<SelectValue>>.SuccessResponse(selectValues));
        }
    }
}
