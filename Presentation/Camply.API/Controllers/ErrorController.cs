using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : ControllerBase
    {
        [Route("/error")]
        public IActionResult Error([FromServices] IWebHostEnvironment webHostEnvironment)
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
            var isDevelopment = webHostEnvironment.IsDevelopment();

            return Problem(
                detail: isDevelopment ? exception?.StackTrace : null,
                title: exception?.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
